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
        // Kiểm tra StudentId có liên kết với Parent hiện tại không
        var link = await db.ParentStudentLink
            .FirstOrDefaultAsync(l => l.StudentId == dto.StudentId && l.ParentId == currentUser.PersonId);
        if (link is null)
            throw ApiException.Forbidden("Bạn không có quyền tạo đơn xin nghỉ cho học sinh này.");

        // Kiểm tra học sinh phải có ghi danh mở (Enrollment.Status == "Studying")
        var enrollment = await db.Enrollment
            .FirstOrDefaultAsync(e => e.StudentId == dto.StudentId);
        if (enrollment is null || enrollment.EndDate < clock.Today)
            throw ApiException.BadRequest("Học sinh không có ghi danh mở. Không thể tạo đơn xin nghỉ.");

        // BR-19: FromDate >= clock.Today (không cho xin nghỉ ngày đã qua)
        if (dto.FromDate < clock.Today)
            throw ApiException.BadRequest("Ngày bắt đầu không được ở quá khứ.");

        // Validate ToDate >= FromDate
        if (dto.ToDate < dto.FromDate)
            throw ApiException.BadRequest("Ngày kết thúc phải >= ngày bắt đầu.");

        // Kiểm tra xung đột với các đơn đang Pending hoặc Approved
        var existingLeaves = await db.LeaveRequest
            .Where(lr => lr.StudentId == dto.StudentId && 
                   (lr.Status == "Pending" || lr.Status == "Approved"))
            .Select(lr => new { lr.FromDate, lr.ToDate, lr.SessionOfDay })
            .ToListAsync();

        var hasConflict = LeaveRequestRules.HasConflict(
            dto.FromDate,
            dto.ToDate,
            dto.SessionOfDay,
            existingLeaves.Select(lr => (lr.FromDate, lr.ToDate, lr.SessionOfDay)));

        if (hasConflict)
            throw ApiException.BadRequest("Có xung đột với đơn xin nghỉ đã tồn tại của học sinh này.");

        // Lấy ClassId hiện tại của học sinh từ enrollment
        var currentClassId = enrollment.ClassId;

        var leaveRequest = new LeaveRequest
        {
            StudentId = dto.StudentId,
            ParentId = currentUser.PersonId,
            ClassId = currentClassId,
            FromDate = dto.FromDate,
            ToDate = dto.ToDate,
            SessionOfDay = dto.SessionOfDay,
            Reason = dto.Reason,
            Status = "Pending",
            CreatedAt = clock.Now,
        };

        db.LeaveRequest.Add(leaveRequest);
        await db.SaveChangesAsync();

        return leaveRequest;
    }

    public async Task<IEnumerable<ParentLeaveItemDto>> GetParentLeavesAsync(int studentId)
    {
        // Kiểm tra quyền: phụ huynh chỉ xem được của con liên kết hợp lệ
        var link = await db.ParentStudentLink
            .FirstOrDefaultAsync(l => l.StudentId == studentId && l.ParentId == currentUser.PersonId);
        if (link is null)
            throw ApiException.Forbidden("Bạn không có quyền xem danh sách xin nghỉ của học sinh này.");

        return await db.LeaveRequest
            .Where(lr => lr.StudentId == studentId)
            .OrderByDescending(lr => lr.CreatedAt)
            .Select(lr => new ParentLeaveItemDto(
                lr.LeaveRequestId,
                lr.StudentId,
                lr.Student.Student.FullName,
                lr.ClassId,
                lr.Class.Name,
                lr.FromDate,
                lr.ToDate,
                lr.SessionOfDay,
                lr.Reason,
                lr.Status,
                lr.CreatedAt,
                lr.DecidedByPersonId,
                lr.DecidedByPerson == null ? null : lr.DecidedByPerson.FullName,
                lr.DecidedAt,
                lr.DecisionNote))
            .ToListAsync();
    }

    public async Task WithdrawAsync(int leaveRequestId)
    {
        var leaveRequest = await db.LeaveRequest.FindAsync(leaveRequestId)
            ?? throw ApiException.NotFound("Không tìm thấy đơn xin nghỉ.");

        // Kiểm tra quyền: phụ huynh chỉ rút được đơn của con
        var link = await db.ParentStudentLink
            .FirstOrDefaultAsync(l => l.StudentId == leaveRequest.StudentId && l.ParentId == currentUser.PersonId);
        if (link is null)
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
                    .Where(e => e.EndDate == null || e.EndDate >= clock.Today)
                    .OrderByDescending(e => e.StartDate)
                    .Select(e => e.Class.Name)
                    .FirstOrDefault(),
                l.Student.Enrollment
                    .Where(e => e.EndDate == null || e.EndDate >= clock.Today)
                    .OrderByDescending(e => e.StartDate)
                    .Select(e => (int?)e.ClassId)
                    .FirstOrDefault()
            ))
            .ToListAsync();
    }
}
