using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Audit;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Rules;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Att.Dtos;
using MSMS.Api.Modules.Ppl.Dtos;
using MSMS.Api.Modules.Ppl.Services;
using MSMS.Tests.Modules.Ppl;

namespace MSMS.Tests.Modules.Adm;

/// <summary>Nhật ký nhóm Trách nhiệm và Điểm danh (BR-28): ghi bằng tiếng Việt, kèm giá trị trước và sau.</summary>
public class AuditNewGroupsTests
{
    private const string Homeroom = ResponsibilityKinds.Homeroom;
    private static readonly DateOnly T = PplEnv.Today;

    private static Dictionary<string, string> Detail(AuditLog a) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(a.Detail!)!
            .Where(p => p.Value.ValueKind != JsonValueKind.Null)
            .ToDictionary(p => p.Key, p => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText());

    // ---------------------------------------------------------------- Trách nhiệm

    [Fact(DisplayName = "Nhật ký | Trách nhiệm | Gán chủ nhiệm | ghi tên lớp, người nhận và ngày hết hiệu lực bằng tiếng Việt")]
    public async Task Assign_WritesVietnameseRecord()
    {
        using var e = new PplEnv();
        await e.Resp.AssignAsync(new ResponsibilityAssignRequest(Homeroom, PplEnv.Class6A, PplEnv.Teacher, T));

        var a = await e.Db.AuditLog.SingleAsync();
        Assert.Equal(AuditCategories.Responsibility, a.Category);
        Assert.Equal(AuditActions.AssignResponsibility, a.Action);
        Assert.Equal(PplEnv.Admin, a.ActorPersonId);
        var d = Detail(a);
        Assert.Equal("Chủ nhiệm", d["Loại trách nhiệm"]);
        Assert.Equal("6A", d["Đối tượng"]);
        Assert.Equal("Giáo viên A", d["Người đảm nhiệm"]);
        Assert.Equal("27/10/2026", d["Từ ngày"]);
        Assert.Equal("31/05/2027", d["Đến ngày"]);
        Assert.False(d.ContainsKey("Người đảm nhiệm cũ"));
        Assert.Contains("Chủ nhiệm", a.Detail!);   // chữ có dấu lưu nguyên dạng, không phải \uXXXX
    }

    [Fact(DisplayName = "Nhật ký | Trách nhiệm | Thay người | ghi hành động thay người và tên người cũ")]
    public async Task Replace_WritesOldHolder()
    {
        using var e = new PplEnv();
        e.Seed("department", PplEnv.Dept1, PplEnv.Teacher, T.AddDays(-10));
        await e.Resp.AssignAsync(new ResponsibilityAssignRequest("department", PplEnv.Dept1, PplEnv.OtherTeacher, T));

        var a = await e.Db.AuditLog.SingleAsync();
        Assert.Equal(AuditActions.ReplaceResponsibility, a.Action);
        var d = Detail(a);
        Assert.Equal("Tổ trưởng", d["Loại trách nhiệm"]);
        Assert.Equal("Tổ Toán", d["Đối tượng"]);
        Assert.Equal("Giáo viên B", d["Người đảm nhiệm"]);
        Assert.Equal("Giáo viên A", d["Người đảm nhiệm cũ"]);
    }

    [Fact(DisplayName = "Nhật ký | Trách nhiệm | Kết thúc | ghi ngày kết thúc")]
    public async Task End_WritesEndDate()
    {
        using var e = new PplEnv();
        e.Seed("campus", PplEnv.Campus1, PplEnv.ViceP, T.AddDays(-30));
        var id = e.Db.CampusLeadership.Single().Id;
        await e.Resp.EndAsync(new ResponsibilityEndRequest("campus", id, T.AddDays(3)));

        var a = await e.Db.AuditLog.SingleAsync();
        Assert.Equal(AuditActions.EndResponsibility, a.Action);
        var d = Detail(a);
        Assert.Equal("Phụ trách phân hiệu", d["Loại trách nhiệm"]);
        Assert.Equal("Phân hiệu 1", d["Đối tượng"]);
        Assert.Equal("Hiệu phó A", d["Người đảm nhiệm"]);
        Assert.Equal("30/10/2026", d["Kết thúc ngày"]);
    }

    [Fact(DisplayName = "Nhật ký | Trách nhiệm | Hủy lịch hẹn | bản hẹn bị xóa hẳn nhưng nhật ký còn giữ lớp và người được hẹn")]
    public async Task Cancel_KeepsWhatWasDeleted()
    {
        using var e = new PplEnv();
        await e.Resp.AssignAsync(new ResponsibilityAssignRequest(Homeroom, PplEnv.Class6A, PplEnv.Teacher, T.AddDays(5)));
        e.Db.AuditLog.RemoveRange(e.Db.AuditLog);   // bỏ bản ghi của lần gán, chỉ xét lần hủy
        e.Db.SaveChanges();
        var row = (await e.Resp.ListCurrentAsync(Homeroom, null)).Single(r => r.TargetId == PplEnv.Class6A);

        await e.Resp.CancelAsync(new ResponsibilityCancelRequest(Homeroom, row.NextRecordId!.Value));

        Assert.Empty(e.Db.HomeroomAssignment);
        var a = await e.Db.AuditLog.SingleAsync();
        Assert.Equal(AuditActions.CancelResponsibility, a.Action);
        var d = Detail(a);
        Assert.Equal("6A", d["Đối tượng"]);
        Assert.Equal("Giáo viên A", d["Người được hẹn"]);
        Assert.Equal("01/11/2026", d["Hẹn từ ngày"]);
    }

    [Fact(DisplayName = "Nhật ký | Trách nhiệm | Gán thất bại | không để lại bản ghi")]
    public async Task RejectedAssign_WritesNothing()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Resp.AssignAsync(new ResponsibilityAssignRequest(Homeroom, PplEnv.Class6A, PplEnv.Teacher, T.AddDays(-1))));

        Assert.Empty(e.Db.AuditLog);
    }

    // ---------------------------------------------------------------- Điểm danh

    private static long SeedSession(PplEnv e, DateOnly date, int teacherId = PplEnv.Teacher)
    {
        if (!e.Db.Subject.Any())
            e.Db.Subject.Add(new Subject { SubjectId = 1, DepartmentId = PplEnv.Dept1, Name = "Toán", Abbreviation = "TO", IsActive = true });
        var s = new TeachingSession
        {
            ClassId = PplEnv.Class6A, SubjectId = 1, TeacherId = teacherId, SessionDate = date, PeriodSlotId = 1,
            Status = "Scheduled", Source = "Auto", CreatedAt = DateTime.UtcNow,
        };
        e.Db.TeachingSession.Add(s);
        e.Db.SaveChanges();
        return s.SessionId;
    }

    private static SaveAttendanceRequest Mark(string status, string? note = null) => new([new SaveAttendanceItem(PplEnv.StudentA, status, note)]);

    [Fact(DisplayName = "Nhật ký | Điểm danh | School Admin nhập bù | ghi số bản ghi nhập bù")]
    public async Task AdminBackfill_IsLogged()
    {
        using var e = new PplEnv();
        var id = SeedSession(e, T.AddDays(-3));

        await e.Attendance.SaveAsync(id, Mark(AttendanceStatuses.Absent));

        var a = await e.Db.AuditLog.SingleAsync();
        Assert.Equal(AuditCategories.Attendance, a.Category);
        Assert.Equal(AuditActions.CorrectAttendance, a.Action);
        Assert.Equal(PplEnv.Admin, a.ActorPersonId);
        Assert.Equal($"[{id}]", a.TargetIds);
        var d = Detail(a);
        Assert.Equal("1", d["Số bản ghi nhập bù"]);
        Assert.Equal("0", d["Số bản ghi sửa"]);
        Assert.Equal("24/10/2026", d["Ngày"]);
    }

    [Fact(DisplayName = "Nhật ký | Điểm danh | School Admin sửa | ghi giá trị trước và sau bằng tiếng Việt")]
    public async Task AdminEdit_RecordsBeforeAndAfter()
    {
        using var e = new PplEnv();
        var id = SeedSession(e, T.AddDays(-3));
        await e.Attendance.SaveAsync(id, Mark(AttendanceStatuses.Present));
        e.Db.AuditLog.RemoveRange(e.Db.AuditLog);
        e.Db.SaveChanges();

        await e.Attendance.SaveAsync(id, Mark(AttendanceStatuses.Absent));

        var d = Detail(await e.Db.AuditLog.SingleAsync());
        Assert.Equal("0", d["Số bản ghi nhập bù"]);
        Assert.Equal("1", d["Số bản ghi sửa"]);
        Assert.Contains("Học sinh A: Có mặt → Vắng", d["Các thay đổi"]);
    }

    [Fact(DisplayName = "Nhật ký | Điểm danh | School Admin lưu lại không đổi gì | không ghi bản nào")]
    public async Task AdminUnchanged_WritesNothing()
    {
        using var e = new PplEnv();
        var id = SeedSession(e, T.AddDays(-3));
        await e.Attendance.SaveAsync(id, Mark(AttendanceStatuses.Present));
        e.Db.AuditLog.RemoveRange(e.Db.AuditLog);
        e.Db.SaveChanges();

        await e.Attendance.SaveAsync(id, Mark(AttendanceStatuses.Present));

        Assert.Empty(e.Db.AuditLog);
    }

    [Fact(DisplayName = "Nhật ký | Điểm danh | Giáo viên điểm danh trong ngày | không vào nhật ký")]
    public async Task TeacherSave_IsNotLogged()
    {
        using var e = new PplEnv(AccountTypes.Teacher, PplEnv.Teacher);
        var id = SeedSession(e, T);

        await e.Attendance.SaveAsync(id, Mark(AttendanceStatuses.Present));
        await e.Attendance.SaveAsync(id, Mark(AttendanceStatuses.Late));

        Assert.Empty(e.Db.AuditLog);
        Assert.Equal(AttendanceStatuses.Late, (await e.Db.AttendanceRecord.SingleAsync()).Status);
    }
}
