using MSMS.Api.Common.Errors;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;
using static MSMS.Tests.Modules.Sch.SchTestSupport;

namespace MSMS.Tests.Modules.Sch;

/// <summary>SCH-02 (4.2.5–4.2.11): tổ chuyên môn và môn học; viết tắt môn chỉ chữ và số, duy nhất toàn trường (BR-07, BR-12).</summary>
public class CatalogServiceTests
{
    private static (MsmsDbContext Db, CatalogService Service) Create()
    {
        var db = NewDb();
        return (db, new CatalogService(db, ClockAt()));
    }

    private static async Task<DepartmentDto> Dept(CatalogService svc, string name = "Tổ Toán – Tin") =>
        await svc.CreateDepartmentAsync(new DepartmentRequest(name, null));

    // ---- Tổ chuyên môn ----
    [Fact]
    public async Task CreateDepartment_DuplicateName_IsRejected()
    {
        var (_, svc) = Create();
        await Dept(svc, "Tổ Toán");

        await Assert.ThrowsAsync<ApiException>(() => Dept(svc, "  Tổ   Toán "));
    }

    [Fact]
    public async Task UpdateDepartment_RenameToAnotherDepartment_IsRejected_ButOwnNameIsFine()
    {
        var (_, svc) = Create();
        var toan = await Dept(svc, "Tổ Toán");
        await Dept(svc, "Tổ Văn");

        await svc.UpdateDepartmentAsync(toan.DepartmentId, new DepartmentRequest("Tổ Toán", "Mô tả mới"));
        await Assert.ThrowsAsync<ApiException>(() => svc.UpdateDepartmentAsync(toan.DepartmentId, new DepartmentRequest("Tổ Văn", null)));

        Assert.Equal("Mô tả mới", (await svc.ListDepartmentsAsync()).Single(d => d.Name == "Tổ Toán").Description);
    }

    [Fact]
    public async Task DeactivateDepartment_WithActiveSubjects_IsRejected()
    {
        var (_, svc) = Create();
        var dept = await Dept(svc);
        await svc.CreateSubjectAsync(new SubjectRequest(dept.DepartmentId, "Toán", "TOAN"));

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.SetDepartmentActiveAsync(dept.DepartmentId, false));

        Assert.Contains("1 môn", ex.Message);
    }

    [Fact]
    public async Task DeactivateDepartment_WithEffectiveHead_IsRejected_ButEndedHeadDoesNotBlock()
    {
        var (db, svc) = Create();
        var dept = await Dept(svc);
        var head = new DepartmentHead { DepartmentId = dept.DepartmentId, StaffId = 2, StartDate = Today.AddDays(-30), CreatedAt = DateTime.UtcNow };
        db.DepartmentHead.Add(head);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ApiException>(() => svc.SetDepartmentActiveAsync(dept.DepartmentId, false));

        head.EndDate = Today.AddDays(-1);
        await db.SaveChangesAsync();
        await svc.SetDepartmentActiveAsync(dept.DepartmentId, false);
        Assert.False((await svc.ListDepartmentsAsync()).Single().IsActive);
    }

    // ---- Môn học ----
    [Fact]
    public async Task CreateSubject_StoresAbbreviation_AndListsUnderDepartment()
    {
        var (_, svc) = Create();
        var dept = await Dept(svc);

        var subject = await svc.CreateSubjectAsync(new SubjectRequest(dept.DepartmentId, "Ngữ văn", "VAN"));

        Assert.Equal("VAN", subject.Abbreviation);
        Assert.Equal(dept.Name, subject.DepartmentName);
        Assert.Single(await svc.ListSubjectsAsync(dept.DepartmentId));
        Assert.Empty(await svc.ListSubjectsAsync(departmentId: 9999));
    }

    [Theory]
    [InlineData("TO-AN")]    // dấu "-" là dấu tách trong ô MÔN-GV
    [InlineData("TO AN")]
    [InlineData("TOÁN")]
    [InlineData("")]
    public async Task CreateSubject_AbbreviationMustBeLettersAndDigitsOnly(string abbreviation)
    {
        var (db, svc) = Create();
        var dept = await Dept(svc);

        await Assert.ThrowsAsync<ApiException>(() => svc.CreateSubjectAsync(new SubjectRequest(dept.DepartmentId, "Toán", abbreviation)));

        Assert.Empty(db.Subject);
    }

    [Fact]
    public async Task CreateSubject_DuplicateAbbreviationOrName_IsRejected()
    {
        var (_, svc) = Create();
        var dept = await Dept(svc);
        await svc.CreateSubjectAsync(new SubjectRequest(dept.DepartmentId, "Toán", "TOAN"));

        var byAbbr = await Assert.ThrowsAsync<ApiException>(() => svc.CreateSubjectAsync(new SubjectRequest(dept.DepartmentId, "Toán nâng cao", "TOAN")));
        var byName = await Assert.ThrowsAsync<ApiException>(() => svc.CreateSubjectAsync(new SubjectRequest(dept.DepartmentId, "Toán", "TOAN2")));

        Assert.Contains("duy nhất toàn trường", byAbbr.Message);
        Assert.Contains("Toán", byName.Message);
    }

    [Fact]
    public async Task CreateSubject_InTheWrongDepartment_IsRejected()
    {
        var (_, svc) = Create();
        var dept = await Dept(svc);
        await svc.SetDepartmentActiveAsync(dept.DepartmentId, false);

        await Assert.ThrowsAsync<ApiException>(() => svc.CreateSubjectAsync(new SubjectRequest(dept.DepartmentId, "Toán", "TOAN")));
        await Assert.ThrowsAsync<ApiException>(() => svc.CreateSubjectAsync(new SubjectRequest(9999, "Toán", "TOAN")));
    }

    [Fact]
    public async Task UpdateSubject_ChangesAbbreviation_ButCannotTakeAnotherSubjectsOne()
    {
        var (_, svc) = Create();
        var dept = await Dept(svc);
        var toan = await svc.CreateSubjectAsync(new SubjectRequest(dept.DepartmentId, "Toán", "TOAN"));
        await svc.CreateSubjectAsync(new SubjectRequest(dept.DepartmentId, "Ngữ văn", "VAN"));

        await svc.UpdateSubjectAsync(toan.SubjectId, new SubjectRequest(dept.DepartmentId, "Toán học", "TOAN1"));
        await Assert.ThrowsAsync<ApiException>(() => svc.UpdateSubjectAsync(toan.SubjectId, new SubjectRequest(dept.DepartmentId, "Toán học", "VAN")));

        var updated = (await svc.ListSubjectsAsync(null)).Single(s => s.SubjectId == toan.SubjectId);
        Assert.Equal("TOAN1", updated.Abbreviation);
        Assert.Equal("Toán học", updated.Name);
    }

    [Fact]
    public async Task DeactivateSubject_ThenReactivate_KeepsTheRecord()
    {
        var (_, svc) = Create();
        var dept = await Dept(svc);
        var toan = await svc.CreateSubjectAsync(new SubjectRequest(dept.DepartmentId, "Toán", "TOAN"));

        await svc.SetSubjectActiveAsync(toan.SubjectId, false);
        Assert.False((await svc.ListSubjectsAsync(null)).Single().IsActive);

        await svc.SetSubjectActiveAsync(toan.SubjectId, true);
        Assert.True((await svc.ListSubjectsAsync(null)).Single().IsActive);
        Assert.Equal(404, (await Assert.ThrowsAsync<ApiException>(() => svc.SetSubjectActiveAsync(999, false))).StatusCode);
    }
}
