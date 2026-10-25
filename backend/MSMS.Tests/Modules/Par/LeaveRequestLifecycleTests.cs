using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;

using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Par.Dtos;
using MSMS.Api.Modules.Par.Services;

namespace MSMS.Tests.Modules.Par;

public class LeaveRequestLifecycleTests
{
    private sealed class FakeClock(DateOnly today, DateTime now) : IClock
    {
        public DateTime Now { get; } = now;
        public DateOnly Today { get; } = today;
    }

    private sealed class SimpleHttpContextAccessor(HttpContext context) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = context;
    }

    private static CurrentUser CreateCurrentUser(int personId, string accountType = AccountTypes.Teacher)
    {
        var claims = new[]
        {
            new Claim(CurrentUser.AccountIdClaim, personId.ToString()),
            new Claim(CurrentUser.PersonIdClaim, personId.ToString()),
            new Claim(CurrentUser.TypeClaim, accountType),
            new Claim(CurrentUser.SessionIdClaim, Guid.NewGuid().ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        return new CurrentUser(new SimpleHttpContextAccessor(httpContext));
    }


    private static MsmsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MsmsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new MsmsDbContext(options);
    }

    [Fact]
    public async Task ChangeHomeroomTeacher_Midway_NewTeacherCanApprove_OldTeacherForbidden()
    {
        // Arrange
        using var db = CreateDbContext();
        var today = new DateOnly(2026, 10, 24);
        var clock = new FakeClock(today, new DateTime(2026, 10, 24, 8, 30, 0));

        const int classId = 10;
        const int oldTeacherPersonId = 101;
        const int newTeacherPersonId = 102;
        const int leaveRequestId = 1;

        // BR-09: GVCN cũ kết thúc trước ngày hôm nay (hết hạn ngày 23/10)
        db.HomeroomAssignment.Add(new HomeroomAssignment
        {
            Id = 1,
            ClassId = classId,
            StaffId = oldTeacherPersonId,
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 10, 23),
            CreatedAt = DateTime.UtcNow
        });

        // GVCN mới bắt đầu từ hôm nay (24/10)
        db.HomeroomAssignment.Add(new HomeroomAssignment
        {
            Id = 2,
            ClassId = classId,
            StaffId = newTeacherPersonId,
            StartDate = today,
            EndDate = null,
            CreatedAt = DateTime.UtcNow
        });

        // Đơn xin nghỉ đang ở trạng thái Pending
        db.LeaveRequest.Add(new LeaveRequest
        {
            LeaveRequestId = leaveRequestId,
            ClassId = classId,
            StudentId = 201,
            ParentId = 301,
            FromDate = new DateOnly(2026, 10, 25),
            ToDate = new DateOnly(2026, 10, 26),
            SessionOfDay = "AllDay",
            Status = "Pending",
            CreatedAt = new DateTime(2026, 10, 23, 10, 0, 0)
        });
        await db.SaveChangesAsync();

        var oldTeacherUser = CreateCurrentUser(oldTeacherPersonId);
        var newTeacherUser = CreateCurrentUser(newTeacherPersonId);

        var serviceOldTeacher = new HomeroomLeaveService(db, clock, oldTeacherUser);
        var serviceNewTeacher = new HomeroomLeaveService(db, clock, newTeacherUser);

        // Act & Assert 1: GVCN cũ cố gắng duyệt -> Bị từ chối quyền (403 Forbidden)
        var forbiddenEx = await Assert.ThrowsAsync<ApiException>(() => serviceOldTeacher.ApproveAsync(leaveRequestId));
        Assert.Equal(403, forbiddenEx.StatusCode);




        // Act & Assert 2: GVCN mới duyệt đơn -> Thành công
        await serviceNewTeacher.ApproveAsync(leaveRequestId);

        var updated = await db.LeaveRequest.FindAsync(leaveRequestId);
        Assert.NotNull(updated);
        Assert.Equal("Approved", updated.Status);
        Assert.Equal(newTeacherPersonId, updated.DecidedByPersonId);
        Assert.Equal(clock.Now, updated.DecidedAt);
    }

    [Fact]
    public async Task DecidedLeaveRequest_CannotBeApprovedAgain()
    {
        // Arrange: Đơn đã duyệt (Approved), không cho duyệt lại
        using var db = CreateDbContext();
        var today = new DateOnly(2026, 10, 24);
        var clock = new FakeClock(today, new DateTime(2026, 10, 24, 8, 30, 0));

        const int classId = 10;
        const int teacherPersonId = 101;
        const int leaveRequestId = 1;

        db.HomeroomAssignment.Add(new HomeroomAssignment
        {
            Id = 1,
            ClassId = classId,
            StaffId = teacherPersonId,
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = null,
            CreatedAt = DateTime.UtcNow
        });

        db.LeaveRequest.Add(new LeaveRequest
        {
            LeaveRequestId = leaveRequestId,
            ClassId = classId,
            StudentId = 201,
            ParentId = 301,
            FromDate = new DateOnly(2026, 10, 25),
            ToDate = new DateOnly(2026, 10, 26),
            SessionOfDay = "AllDay",
            Status = "Approved",
            DecidedByPersonId = teacherPersonId,
            DecidedAt = new DateTime(2026, 10, 23, 10, 0, 0),
            CreatedAt = new DateTime(2026, 10, 23, 9, 0, 0)
        });
        await db.SaveChangesAsync();

        var teacherUser = CreateCurrentUser(teacherPersonId);
        var service = new HomeroomLeaveService(db, clock, teacherUser);

        // Act & Assert: Thử duyệt lại đơn đã duyệt -> Lỗi BadRequest (400)
        var badRequestEx = await Assert.ThrowsAsync<ApiException>(() => service.ApproveAsync(leaveRequestId));
        Assert.Equal(400, badRequestEx.StatusCode);
    }

    [Fact]
    public async Task DecidedLeaveRequest_CannotBeRejectedAgain()
    {
        // Arrange: Đơn đã từ chối (Rejected), không cho từ chối lại hoặc duyệt lại
        using var db = CreateDbContext();
        var today = new DateOnly(2026, 10, 24);
        var clock = new FakeClock(today, new DateTime(2026, 10, 24, 8, 30, 0));

        const int classId = 10;
        const int teacherPersonId = 101;
        const int leaveRequestId = 1;

        db.HomeroomAssignment.Add(new HomeroomAssignment
        {
            Id = 1,
            ClassId = classId,
            StaffId = teacherPersonId,
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = null,
            CreatedAt = DateTime.UtcNow
        });

        db.LeaveRequest.Add(new LeaveRequest
        {
            LeaveRequestId = leaveRequestId,
            ClassId = classId,
            StudentId = 201,
            ParentId = 301,
            FromDate = new DateOnly(2026, 10, 25),
            ToDate = new DateOnly(2026, 10, 26),
            SessionOfDay = "AllDay",
            Status = "Rejected",
            DecisionNote = "Không hợp lệ",
            DecidedByPersonId = teacherPersonId,
            DecidedAt = new DateTime(2026, 10, 23, 10, 0, 0),
            CreatedAt = new DateTime(2026, 10, 23, 9, 0, 0)
        });
        await db.SaveChangesAsync();

        var teacherUser = CreateCurrentUser(teacherPersonId);
        var service = new HomeroomLeaveService(db, clock, teacherUser);

        // Act & Assert 1: Thử từ chối lại -> Lỗi BadRequest (400)
        var rejectEx = await Assert.ThrowsAsync<ApiException>(() =>
            service.RejectAsync(leaveRequestId, new DecideLeaveRequestDto("Lý do khác")));
        Assert.Equal(400, rejectEx.StatusCode);

        // Act & Assert 2: Thử duyệt đơn đã từ chối -> Lỗi BadRequest (400)
        var approveEx = await Assert.ThrowsAsync<ApiException>(() => service.ApproveAsync(leaveRequestId));
        Assert.Equal(400, approveEx.StatusCode);
    }

    [Fact]
    public async Task RejectAsync_RequiresDecisionNote()
    {
        // Arrange
        using var db = CreateDbContext();
        var today = new DateOnly(2026, 10, 24);
        var clock = new FakeClock(today, new DateTime(2026, 10, 24, 8, 30, 0));

        const int classId = 10;
        const int teacherPersonId = 101;
        const int leaveRequestId = 1;

        db.HomeroomAssignment.Add(new HomeroomAssignment
        {
            Id = 1,
            ClassId = classId,
            StaffId = teacherPersonId,
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = null,
            CreatedAt = DateTime.UtcNow
        });

        db.LeaveRequest.Add(new LeaveRequest
        {
            LeaveRequestId = leaveRequestId,
            ClassId = classId,
            StudentId = 201,
            ParentId = 301,
            FromDate = new DateOnly(2026, 10, 25),
            ToDate = new DateOnly(2026, 10, 26),
            SessionOfDay = "AllDay",
            Status = "Pending",
            CreatedAt = new DateTime(2026, 10, 23, 9, 0, 0)
        });
        await db.SaveChangesAsync();

        var teacherUser = CreateCurrentUser(teacherPersonId);
        var service = new HomeroomLeaveService(db, clock, teacherUser);

        // Act & Assert: Từ chối không có lý do -> BadRequest (400)
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            service.RejectAsync(leaveRequestId, new DecideLeaveRequestDto("")));
        Assert.Equal(400, ex.StatusCode);
    }
}
