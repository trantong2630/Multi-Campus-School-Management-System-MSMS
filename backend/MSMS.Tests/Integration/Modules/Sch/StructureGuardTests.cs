using MSMS.Api.Data.Entities;
using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Sch;

/// <summary>SCH-02/03: các chặn khi ngừng lớp, ngừng khung giờ, đổi và xóa năm học (STR-13, 17, 21, 22).</summary>
[Collection("api")]
public class StructureGuardTests(ApiFactory f)
{
    private World w => f.World;
    private async Task<ApiClient> AdminAsync() { await w.EnsureAsync(); f.Clock.Reset(); return await f.LoginAsync(w.SchoolAdmin.Username); }
    private static string U() => World.Uniq();

    [Fact]
    public async Task STR_13_Deactivating_a_class_is_blocked_until_pupils_sessions_and_homeroom_are_cleared()
    {
        var a = await AdminAsync();
        var s = await w.NewScenarioAsync();
        var cls = s.ClassAId;
        var (pupil, enr) = await w.NewStudentAsync(cls, "HS ngừng lớp " + U());
        var day = f.Clock.Today.AddDays(3);
        var session = await w.NewSessionAsync(cls, s.SubjectId, s.Absent.PersonId, day, 1);
        var hr = await f.WithDbAsync(async db =>
        {
            var h = new HomeroomAssignment { ClassId = cls, StaffId = s.Sub.PersonId, StartDate = new DateOnly(2026, 9, 5), CreatedAt = f.Clock.Now };
            db.HomeroomAssignment.Add(h);
            await db.SaveChangesAsync();
            return h.Id;
        });
        // Bước 1: còn học sinh
        var r1 = await a.PutAsync($"/api/sch/classes/{cls}/active", new { isActive = false });
        Assert.Equal(400, r1.Status);
        Assert.Equal("Lớp còn 1 học sinh đang học hoặc sắp vào lớp. Chuyển lớp hoặc cho rời lớp trước.", r1.Message);
        // Bước 2: cho học sinh rời lớp, còn tiết và chủ nhiệm
        Assert.Equal(204, (await a.PostAsync($"/api/sch/enrollments/{enr}/close", new { reason = "SchoolTransfer" })).Status);
        await f.WithDbAsync(async db => { (await db.Enrollment.SingleAsync(e => e.EnrollmentId == enr)).EndDate = f.Clock.Today.AddDays(-1); await db.SaveChangesAsync(); });   // rời lớp từ hôm qua
        var r2 = await a.PutAsync($"/api/sch/classes/{cls}/active", new { isActive = false });
        Assert.Equal(400, r2.Status);
        Assert.StartsWith("Lớp còn 1 tiết chưa diễn ra", r2.Message);
        Assert.Contains("chủ nhiệm " + s.Sub.FullName, r2.Message);
        Assert.EndsWith("Dọn xong rồi ngừng hoạt động lớp.", r2.Message);
        // Bước 3: hủy tiết
        await f.WithDbAsync(async db => { (await db.TeachingSession.SingleAsync(x => x.SessionId == session)).Status = "Cancelled"; await db.SaveChangesAsync(); });
        var r3 = await a.PutAsync($"/api/sch/classes/{cls}/active", new { isActive = false });
        Assert.Equal(400, r3.Status);
        Assert.DoesNotContain("tiết chưa diễn ra", r3.Message);
        Assert.Contains("chủ nhiệm", r3.Message);
        // Bước 4: kết thúc chủ nhiệm
        await f.WithDbAsync(async db => { (await db.HomeroomAssignment.SingleAsync(x => x.Id == hr)).EndDate = f.Clock.Today.AddDays(-1); await db.SaveChangesAsync(); });
        var r4 = await a.PutAsync($"/api/sch/classes/{cls}/active", new { isActive = false });
        Assert.True(r4.Ok);
        Assert.False(await f.WithDbAsync(db => db.Class.Where(c => c.ClassId == cls).Select(c => c.IsActive).SingleAsync()));
        Assert.NotEqual(0, pupil.PersonId);
    }

    [Fact]
    public async Task STR_17_A_period_slot_used_by_sessions_cannot_be_deactivated()
    {
        var a = await AdminAsync();
        var s = await w.NewScenarioAsync();
        await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, f.Clock.Today.AddDays(4), 1);
        var slotId = await f.WithDbAsync(db => db.PeriodSlot.Where(p => p.SessionOfDay == "Morning" && p.PeriodNo == 1).Select(p => p.PeriodSlotId).SingleAsync());
        var r = await a.PutAsync($"/api/sch/period-slots/{slotId}/active", new { isActive = false });
        Assert.Equal(400, r.Status);
        Assert.Equal("Khung giờ đang có tiết học hoặc thời khóa biểu tham chiếu nên không ngừng dùng được.", r.Message);
        Assert.True(await f.WithDbAsync(db => db.PeriodSlot.Where(p => p.PeriodSlotId == slotId).Select(p => p.IsActive).SingleAsync()));
    }

    [Fact]
    public async Task STR_21_Setting_the_current_year_leaves_exactly_one_current_year()
    {
        var a = await AdminAsync();
        var next = await f.WithDbAsync(async db =>
        {
            var y = new AcademicYear { Name = "Năm kế " + U(), StartDate = new DateOnly(2300, 9, 5), EndDate = new DateOnly(2301, 5, 31), IsCurrent = false };
            db.AcademicYear.Add(y);
            await db.SaveChangesAsync();
            return y.AcademicYearId;
        });
        try
        {
            Assert.True((await a.PutAsync($"/api/sch/academic-years/{next}/current", new { })).Ok);
            var current = await f.WithDbAsync(db => db.AcademicYear.Where(y => y.IsCurrent).Select(y => y.AcademicYearId).ToListAsync());
            Assert.Equal([next], current);
            Assert.Equal(404, (await a.PutAsync("/api/sch/academic-years/987654/current", new { })).Status);
        }
        finally { await a.PutAsync($"/api/sch/academic-years/{w.YearId}/current", new { }); }
        Assert.Equal([w.YearId], await f.WithDbAsync(db => db.AcademicYear.Where(y => y.IsCurrent).Select(y => y.AcademicYearId).ToListAsync()));
    }

    [Fact]
    public async Task STR_22_Delete_academic_year_guards()
    {
        var a = await AdminAsync();
        var (used, empty) = await f.WithDbAsync(async db =>
        {
            var u = new AcademicYear { Name = "Năm đã dùng " + U(), StartDate = new DateOnly(2310, 9, 5), EndDate = new DateOnly(2311, 5, 31), IsCurrent = false };
            var e = new AcademicYear { Name = "Năm trống " + U(), StartDate = new DateOnly(2320, 9, 5), EndDate = new DateOnly(2321, 5, 31), IsCurrent = false };
            db.AcademicYear.AddRange(u, e);
            await db.SaveChangesAsync();
            db.Class.Add(new Class { CampusId = w.CampusAId, AcademicYearId = u.AcademicYearId, Name = "L" + U(), GradeLevel = 6, IsActive = true, CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
            return (u.AcademicYearId, e.AcademicYearId);
        });
        var cur = await a.DeleteAsync($"/api/sch/academic-years/{w.YearId}");
        Assert.Equal(400, cur.Status);
        Assert.EndsWith("đang là năm học hiện hành. Đặt năm học khác làm hiện hành trước rồi mới xóa.", cur.Message);
        var u1 = await a.DeleteAsync($"/api/sch/academic-years/{used}");
        Assert.Equal(400, u1.Status);
        Assert.Contains("đã có 1 lớp nên không xóa được.", u1.Message);
        Assert.True((await a.DeleteAsync($"/api/sch/academic-years/{empty}")).Ok);
        Assert.False(await f.WithDbAsync(db => db.AcademicYear.AnyAsync(y => y.AcademicYearId == empty)));
        Assert.Equal(404, (await a.DeleteAsync($"/api/sch/academic-years/{empty}")).Status);
    }

    [Fact]
    public async Task STR_18_Changing_slot_times_changes_when_a_session_gets_locked()
    {
        var a = await AdminAsync();
        var s = await w.NewScenarioAsync();
        var slot = await f.WithDbAsync(db => db.PeriodSlot.Where(p => p.SessionOfDay == "Morning" && p.PeriodNo == 1).Select(p => new { p.PeriodSlotId, p.StartTime, p.EndTime }).SingleAsync());
        var sid = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, f.Clock.Today, 1);
        var vice = await f.LoginAsync(w.ViceA.Username);
        var body = new { teacherId = s.Sub.PersonId };
        try
        {
            f.Clock.Set(f.Clock.Today.ToDateTime(new TimeOnly(7, 47)));
            var locked = await vice.PutAsync($"/api/scd/adjust/sessions/{sid}/teacher", body);   // 07:47 > 07:45: tiết đã kết thúc
            Assert.Equal(400, locked.Status);
            // kéo dài giờ kết thúc tới 07:49 (tiết 2 bắt đầu 07:50 nên không thể dài hơn)
            Assert.True((await a.PutAsync($"/api/sch/period-slots/{slot.PeriodSlotId}", new { startTime = "07:00", endTime = "07:49" })).Ok);
            f.Clock.Set(f.Clock.Today.ToDateTime(new TimeOnly(7, 47)));
            var ok = await vice.PutAsync($"/api/scd/adjust/sessions/{sid}/teacher", body);
            Assert.True(ok.Ok, ok.Raw);
            var tooLong = await a.PutAsync($"/api/sch/period-slots/{slot.PeriodSlotId}", new { startTime = "07:00", endTime = "08:00" });
            Assert.Equal(400, tooLong.Status);   // chồng với tiết 2
        }
        finally
        {
            f.Clock.Reset();
            await a.PutAsync($"/api/sch/period-slots/{slot.PeriodSlotId}", new { startTime = slot.StartTime.ToString("HH:mm"), endTime = slot.EndTime.ToString("HH:mm") });
        }
    }
}
