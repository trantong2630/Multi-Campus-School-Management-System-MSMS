using ClosedXML.Excel;
using MSMS.Api.Common.Auth;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Services;
using MSMS.Api.Modules.Ppl.Services;
using MSMS.Tests.Modules.Iam;

namespace MSMS.Tests.Modules.Ppl;

/// <summary>Dữ liệu màn Trách nhiệm: người đang giữ, người sắp nhận, người đã kết thúc và xuất Excel.</summary>
public class ResponsibilityListTests
{
    private static readonly DateOnly Today = new(2026, 10, 27);

    private static (AccountTestFixture f, ResponsibilityService svc) Setup()
    {
        var f = new AccountTestFixture();
        f.AddAccount(10, AccountTypes.VicePrincipal);
        f.AddAccount(11, AccountTypes.VicePrincipal);
        var user = AccountTestFixture.User(AccountTypes.SchoolAdmin, 900);
        return (f, new ResponsibilityService(f.Db, user, f.Clock, new ScopeResolver(f.Db, user, f.Clock)));
    }

    [Fact]
    public async Task Campus_rows_report_current_upcoming_and_ended_holders()
    {
        var (f, svc) = Setup();
        // PH1: người đang giữ + người được hẹn từ 2 ngày nữa (người cũ kết thúc ngày liền trước, nên hôm nay vẫn còn đương nhiệm); PH2: từng có người nhưng đã kết thúc.
        f.Db.CampusLeadership.Add(new CampusLeadership { CampusId = 1, StaffId = 10, StartDate = Today.AddDays(-20) }); f.Db.SaveChanges();
        await svc.AssignAsync(new(ResponsibilityKinds.Campus, 1, 11, Today.AddDays(2)));
        f.Db.CampusLeadership.Add(new CampusLeadership { CampusId = 2, StaffId = 10, StartDate = Today.AddDays(-60), EndDate = Today.AddDays(-30) });
        await f.Db.SaveChangesAsync();

        var rows = await svc.ListCurrentAsync(ResponsibilityKinds.Campus, null);

        var ph1 = rows.Single(r => r.TargetId == 1);
        Assert.Equal(10, ph1.StaffId);
        Assert.Equal("PH1", ph1.TargetCode);
        Assert.NotNull(ph1.StaffCode);
        Assert.Equal(Today.AddDays(2), ph1.NextStartDate);
        Assert.Equal(11, ph1.NextStaffId);   // để giao diện làm mờ người đã được xếp cho đích khác
        var ph2 = rows.Single(r => r.TargetId == 2);
        Assert.Null(ph2.StaffId);
        Assert.NotNull(ph2.EndedStaffName);
        Assert.Equal(Today.AddDays(-30), ph2.EndedDate);
    }

    [Fact]
    public async Task Export_returns_an_xlsx_with_one_row_per_target_and_a_status()
    {
        var (_, svc) = Setup();
        await svc.AssignAsync(new(ResponsibilityKinds.Campus, 1, 10, Today));

        var file = await svc.ExportAsync(ResponsibilityKinds.Campus, null);

        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
        using var wb = new XLWorkbook(new MemoryStream(file.FileContents));
        var ws = wb.Worksheet(1);
        Assert.Equal("Mã phân hiệu", ws.Cell(1, 1).GetString());
        Assert.Equal("Đã phân công", ws.Cell(2, 7).GetString());   // PH1 có người
        Assert.Equal("Chưa phân công", ws.Cell(3, 7).GetString()); // PH2 trống
    }

    [Theory]
    [InlineData(ResponsibilityKinds.Department, 1)]
    [InlineData(ResponsibilityKinds.Homeroom, 1)]
    public async Task Department_and_homeroom_rows_report_the_same_handover_fields_as_campus(string kind, int targetId)
    {
        var (f, svc) = Setup();
        f.AddAccount(20, AccountTypes.Teacher);
        f.AddAccount(21, AccountTypes.Teacher);
        if (kind == ResponsibilityKinds.Department) f.Db.DepartmentHead.Add(new DepartmentHead { DepartmentId = targetId, StaffId = 20, StartDate = Today.AddDays(-20) });
        else f.Db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = targetId, StaffId = 20, StartDate = Today.AddDays(-20) });
        f.Db.SaveChanges();
        await svc.AssignAsync(new(kind, targetId, 21, Today.AddDays(2)));   // người cũ kết thúc ngày liền trước

        var row = (await svc.ListCurrentAsync(kind, null)).Single(r => r.TargetId == targetId);

        Assert.Equal(20, row.StaffId);                                 // hôm nay người cũ vẫn đang giữ
        Assert.NotNull(row.StaffCode);
        Assert.Equal(Today.AddDays(1), row.EndDate);                   // kết thúc ngày liền trước ngày bắt đầu của người mới
        Assert.Equal(21, row.NextStaffId);
        Assert.Equal(Today.AddDays(2), row.NextStartDate);
    }
}