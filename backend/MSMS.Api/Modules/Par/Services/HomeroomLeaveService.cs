using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Par.Dtos;

namespace MSMS.Api.Modules.Par.Services;

public sealed class HomeroomLeaveService(MsmsDbContext db, IClock clock, CurrentUser currentUser)
{
    public async Task<IEnumerable<HomeroomLeaveItemDto>> GetHomeroomInboxAsync(int? classId)
    {
        var today = clock.Today;
        var myHomeroomClasses = await db.HomeroomAssignment
            .AsNoTracking()
            .Where(EffectivePeriod.EffectiveAt<HomeroomAssignment>(today))
            .Where(ha => ha.StaffId == currentUser.PersonId)
            .Select(ha => ha.ClassId)
            .ToListAsync();

        if (classId.HasValue)
        {
            if (!myHomeroomClasses.Contains(classId.Value))
                throw ApiException.Forbidden("Bạn không phải là giáo viên chủ nhiệm hiện tại của lớp này.");

            myHomeroomClasses = [classId.Value];
        }

        if (myHomeroomClasses.Count == 0)
            return [];

        return await db.LeaveRequest
            .AsNoTracking()
            .Where(lr => myHomeroomClasses.Contains(lr.ClassId))
            .OrderByDescending(lr => lr.CreatedAt)
            .Select(lr => new HomeroomLeaveItemDto(
                lr.LeaveRequestId,
                lr.StudentId,
                lr.Student.Student.FullName,
                lr.Student.Student.PersonCode,
                lr.ClassId,
                lr.Class.Name,
                lr.ParentId,
                lr.Parent.FullName,
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

    public async Task ApproveAsync(int leaveRequestId)
    {
        var leaveRequest = await db.LeaveRequest.FindAsync(leaveRequestId)
            ?? throw ApiException.NotFound("Không tìm thấy đơn xin nghỉ.");

        if (leaveRequest.Status != "Pending")
            throw ApiException.BadRequest("Chỉ có thể duyệt đơn xin nghỉ ở trạng thái Chờ phê duyệt.");

        var today = clock.Today;
        var isHomeroom = await db.HomeroomAssignment
            .Where(EffectivePeriod.EffectiveAt<HomeroomAssignment>(today))
            .AnyAsync(ha => ha.ClassId == leaveRequest.ClassId && ha.StaffId == currentUser.PersonId);

        if (!isHomeroom)
            throw ApiException.Forbidden("Bạn không phải là giáo viên chủ nhiệm hiện tại của lớp này.");

        leaveRequest.Status = "Approved";
        leaveRequest.DecidedByPersonId = currentUser.PersonId;
        leaveRequest.DecidedAt = clock.Now;

        await db.SaveChangesAsync();
    }

    public async Task RejectAsync(int leaveRequestId, DecideLeaveRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.DecisionNote))
            throw ApiException.BadRequest("Vui lòng nhập lý do từ chối đơn xin nghỉ.");

        var leaveRequest = await db.LeaveRequest.FindAsync(leaveRequestId)
            ?? throw ApiException.NotFound("Không tìm thấy đơn xin nghỉ.");

        if (leaveRequest.Status != "Pending")
            throw ApiException.BadRequest("Chỉ có thể từ chối đơn xin nghỉ ở trạng thái Chờ phê duyệt.");

        var today = clock.Today;
        var isHomeroom = await db.HomeroomAssignment
            .Where(EffectivePeriod.EffectiveAt<HomeroomAssignment>(today))
            .AnyAsync(ha => ha.ClassId == leaveRequest.ClassId && ha.StaffId == currentUser.PersonId);

        if (!isHomeroom)
            throw ApiException.Forbidden("Bạn không phải là giáo viên chủ nhiệm hiện tại của lớp này.");

        leaveRequest.Status = "Rejected";
        leaveRequest.DecisionNote = dto.DecisionNote.Trim();
        leaveRequest.DecidedByPersonId = currentUser.PersonId;
        leaveRequest.DecidedAt = clock.Now;

        await db.SaveChangesAsync();
    }
}
