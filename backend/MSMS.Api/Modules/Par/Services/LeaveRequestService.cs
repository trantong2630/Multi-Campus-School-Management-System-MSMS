using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Par.Dtos;

namespace MSMS.Api.Modules.Par.Services;

public sealed class LeaveRequestService(MsmsDbContext db, IClock clock, CurrentUser currentUser)
{
    public async Task<LeaveRequest> SubmitAsync(CreateLeaveRequestDto dto)
    {
        var classId = await RequireEnrolledClassIdAsync(dto.StudentId);

        // BR-19: FromDate >= clock.Today (không cho xin nghỉ ngày đã qua)
        if (dto.FromDate < clock.Today)
            throw ApiException.BadRequest("Ngày bắt đầu không được ở quá khứ.");

        // Validate ToDate >= FromDate
        if (dto.ToDate < dto.FromDate)
            throw ApiException.BadRequest("Ngày kết thúc phải >= ngày bắt đầu.");

        var slotIds = ValidateDay(new LeaveDayItemDto(dto.FromDate, dto.SessionOfDay, dto.SlotIds));

        var existingLeaves = await LoadActiveLeavesAsync(dto.StudentId);
        if (LeaveRequestRules.HasConflict(dto.FromDate, dto.ToDate, dto.SessionOfDay, slotIds, existingLeaves))
            throw ApiException.BadRequest("Có xung đột với đơn xin nghỉ đã tồn tại của học sinh này.");

        var leaveRequest = new LeaveRequest
        {
            StudentId = dto.StudentId,
            ParentId = currentUser.PersonId,
            ClassId = classId,
            FromDate = dto.FromDate,
            ToDate = dto.ToDate,
            SessionOfDay = dto.SessionOfDay,
            SlotIds = slotIds,
            Reason = dto.Reason,
            Status = "Pending",
            CreatedAt = clock.Now,
        };

        db.LeaveRequest.Add(leaveRequest);
        await db.SaveChangesAsync();

        return leaveRequest;
    }

    /// <summary>
    /// PAR-01: mỗi ngày (hoặc tiết lẻ) trong đơn thành một LeaveRequest riêng (FromDate = ToDate).
    /// Mọi dòng được kiểm tra xung đột với đơn đã có lẫn với các dòng khác trong cùng đơn trước khi lưu.
    /// </summary>
    public async Task<IReadOnlyList<LeaveRequest>> SubmitBulkAsync(BulkCreateLeaveRequestDto dto)
    {
        var classId = await RequireEnrolledClassIdAsync(dto.StudentId);

        if (dto.Days.Count == 0)
            throw ApiException.BadRequest("Chọn ít nhất một ngày nghỉ.");

        var taken = await LoadActiveLeavesAsync(dto.StudentId);
        var reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();
        var now = clock.Now;
        var leaves = new List<LeaveRequest>();

        foreach (var day in dto.Days)
        {
            // BR-19: Ngày nghỉ >= clock.Today
            if (day.Date < clock.Today)
                throw ApiException.BadRequest($"Ngày {day.Date:dd/MM/yyyy} đã qua, không thể xin nghỉ.");

            var slotIds = ValidateDay(day);

            if (LeaveRequestRules.HasConflict(day.Date, day.Date, day.SessionOfDay, slotIds, taken))
                throw ApiException.BadRequest($"Ngày {day.Date:dd/MM/yyyy} xung đột với đơn xin nghỉ đã có hoặc với dòng khác trong đơn.");

            taken.Add((day.Date, day.Date, day.SessionOfDay, slotIds));
            leaves.Add(new LeaveRequest
            {
                StudentId = dto.StudentId,
                ParentId = currentUser.PersonId,
                ClassId = classId,
                FromDate = day.Date,
                ToDate = day.Date,
                SessionOfDay = day.SessionOfDay,
                SlotIds = slotIds,
                Reason = reason,
                Status = "Pending",
                CreatedAt = now,
            });
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        db.LeaveRequest.AddRange(leaves);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return leaves;
    }

    public async Task<PagedResult<ParentLeaveItemDto>> GetParentLeavesAsync(int studentId, int page = 1, int pageSize = 10)
    {
        pageSize = Math.Clamp(pageSize, 1, 10);

        // Kiểm tra quyền: phụ huynh chỉ xem được của con liên kết hợp lệ
        var link = await db.ParentStudentLink
            .FirstOrDefaultAsync(l => l.StudentId == studentId && l.ParentId == currentUser.PersonId);
        if (link is null)
            throw ApiException.Forbidden("Bạn không có quyền xem danh sách xin nghỉ của học sinh này.");

        return await db.LeaveRequest
            .AsNoTracking()
            .Where(lr => lr.StudentId == studentId)
            .OrderByDescending(lr => lr.CreatedAt)
            .ThenByDescending(lr => lr.LeaveRequestId)
            .Select(lr => new ParentLeaveItemDto(
                lr.LeaveRequestId,
                lr.StudentId,
                lr.Student.Student.FullName,
                lr.ClassId,
                lr.Class.Name,
                lr.FromDate,
                lr.ToDate,
                lr.SessionOfDay,
                lr.SlotIds,
                lr.Reason,
                lr.Status,
                lr.CreatedAt,
                lr.DecidedByPersonId,
                lr.DecidedByPerson == null ? null : lr.DecidedByPerson.FullName,
                lr.DecidedAt,
                lr.DecisionNote))
            .ToPagedAsync(page, pageSize);
    }

    public async Task WithdrawAsync(int leaveRequestId)
    {
        var leaveRequest = await db.LeaveRequest.FindAsync(leaveRequestId)
            ?? throw ApiException.NotFound("Không tìm thấy đơn xin nghỉ.");

        // Kiểm tra quyền: phụ huynh chỉ rút được đơn của con
        var link = await db.ParentStudentLink
            .FirstOrDefaultAsync(l => l.StudentId == leaveRequest.StudentId && l.ParentId == currentUser.PersonId);
        if (link is null || leaveRequest.ParentId != currentUser.PersonId)
            throw ApiException.Forbidden("Bạn không có quyền rút đơn xin nghỉ này.");

        // Chỉ rút được khi đơn còn ở Pending
        if (leaveRequest.Status != "Pending")
            throw ApiException.BadRequest("Chỉ có thể rút đơn xin nghỉ ở trạng thái Chờ phê duyệt.");

        leaveRequest.Status = "Withdrawn";
        await db.SaveChangesAsync();
    }

    public async Task<IEnumerable<MyChildDto>> GetMyChildrenAsync()
    {
        return await db.ParentStudentLink
            .Where(l => l.ParentId == currentUser.PersonId)
            .Select(l => new MyChildDto(
                l.Student.StudentId,
                l.Student.Student.PersonCode,
                l.Student.Student.FullName,
                l.Student.Enrollment
                    .AsQueryable().Where(EnrollmentRules.ActiveAt(clock.Today))
                    .OrderByDescending(e => e.StartDate)
                    .Select(e => e.Class.Name)
                    .FirstOrDefault(),
                l.Student.Enrollment
                    .AsQueryable().Where(EnrollmentRules.ActiveAt(clock.Today))
                    .OrderByDescending(e => e.StartDate)
                    .Select(e => (int?)e.ClassId)
                    .FirstOrDefault()
            ))
            .ToListAsync();
    }

    /// <summary>Kiểm tra phụ huynh được liên kết với học sinh và học sinh có ghi danh mở; trả về lớp hiện tại.</summary>
    private async Task<int> RequireEnrolledClassIdAsync(int studentId)
    {
        var link = await db.ParentStudentLink
            .FirstOrDefaultAsync(l => l.StudentId == studentId && l.ParentId == currentUser.PersonId);
        if (link is null)
            throw ApiException.Forbidden("Bạn không có quyền tạo đơn xin nghỉ cho học sinh này.");

        var enrollment = await db.Enrollment.Where(EnrollmentRules.ActiveAt(clock.Today))
            .FirstOrDefaultAsync(e => e.StudentId == studentId);
        if (enrollment is null)
            throw ApiException.BadRequest("Học sinh không có ghi danh mở. Không thể tạo đơn xin nghỉ.");

        return enrollment.ClassId;
    }

    private async Task<List<(DateOnly From, DateOnly To, string Session, string? SlotIds)>> LoadActiveLeavesAsync(int studentId)
    {
        var rows = await db.LeaveRequest
            .Where(lr => lr.StudentId == studentId && (lr.Status == "Pending" || lr.Status == "Approved"))
            .Select(lr => new { lr.FromDate, lr.ToDate, lr.SessionOfDay, lr.SlotIds })
            .ToListAsync();

        return rows.Select(lr => (lr.FromDate, lr.ToDate, lr.SessionOfDay, lr.SlotIds)).ToList();
    }

    /// <summary>Kiểm tra buổi và tiết của một dòng; trả về chuỗi SlotIds đã chuẩn hóa (null nếu nghỉ cả buổi).</summary>
    private static string? ValidateDay(LeaveDayItemDto day)
    {
        if (day.SessionOfDay is not ("AllDay" or "Morning" or "Afternoon"))
            throw ApiException.BadRequest("Buổi nghỉ không hợp lệ.");

        if (day.SlotIds is null || day.SlotIds.Count == 0)
            return null;

        if (day.SessionOfDay == "AllDay")
            throw ApiException.BadRequest("Nghỉ theo tiết chỉ áp dụng cho buổi sáng hoặc buổi chiều.");

        if (day.SlotIds.Any(s => s is < 1 or > 5) || day.SlotIds.Distinct().Count() != day.SlotIds.Count)
            throw ApiException.BadRequest("Số tiết không hợp lệ hoặc bị trùng (tiết từ 1 đến 5).");

        return string.Join(',', day.SlotIds.Order());
    }
}
