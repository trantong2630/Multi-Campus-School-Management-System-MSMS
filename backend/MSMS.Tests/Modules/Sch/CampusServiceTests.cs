using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;
using static MSMS.Tests.Modules.Sch.SchTestSupport;

namespace MSMS.Tests.Modules.Sch;

/// <summary>SCH-01 (4.2.1–4.2.4): phân hiệu có mã và tên duy nhất; có lớp đang hoạt động thì không ngừng hoạt động được (BR-07).</summary>
public class CampusServiceTests
{
    private static (MsmsDbContext Db, CampusService Service) Create()
    {
        var db = NewDb();
        return (db, new CampusService(db, ClockAt()));
    }

    private static CampusRequest Req(string code, string name, string? address = null) => new(code, name, address, null);

    [Fact]
    public async Task Create_NormalizesTextAndStartsActive()
    {
        var (_, svc) = Create();

        var campus = await svc.CreateAsync(Req("  PH1 ", "Phân   hiệu  1", "  12 Lê Lợi "));

        Assert.Equal("PH1", campus.Code);
        Assert.Equal("Phân hiệu 1", campus.Name);
        Assert.Equal("12 Lê Lợi", campus.Address);
        Assert.True(campus.IsActive);
        Assert.Equal(0, campus.ActiveClassCount);
    }

    [Fact]
    public async Task Create_DuplicateCode_IsRejected()
    {
        var (_, svc) = Create();
        await svc.CreateAsync(Req("PH1", "Phân hiệu 1"));

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.CreateAsync(Req("PH1", "Phân hiệu 2")));

        Assert.Contains("Mã phân hiệu", ex.Message);
    }

    [Fact]
    public async Task Create_DuplicateName_IsRejected_EvenWithDifferentSpacing()
    {
        var (_, svc) = Create();
        await svc.CreateAsync(Req("PH1", "Phân hiệu 1"));

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.CreateAsync(Req("PH2", "  Phân   hiệu 1 ")));

        Assert.Contains("Tên phân hiệu", ex.Message);
    }

    [Theory]
    [InlineData("", "Phân hiệu 1")]
    [InlineData("PH1", "   ")]
    public async Task Create_WithoutCodeOrName_IsRejected(string code, string name)
    {
        var (db, svc) = Create();

        await Assert.ThrowsAsync<ApiException>(() => svc.CreateAsync(Req(code, name)));

        Assert.Empty(await db.Campus.ToListAsync());
    }

    [Fact]
    public async Task Update_KeepingOwnCodeAndName_IsAllowed_ButTakingAnotherName_IsRejected()
    {
        var (_, svc) = Create();
        var first = await svc.CreateAsync(Req("PH1", "Phân hiệu 1"));
        await svc.CreateAsync(Req("PH2", "Phân hiệu 2"));

        await svc.UpdateAsync(first.CampusId, Req("PH1", "Phân hiệu 1", "Địa chỉ mới"));
        await Assert.ThrowsAsync<ApiException>(() => svc.UpdateAsync(first.CampusId, Req("PH1", "Phân hiệu 2")));

        Assert.Equal("Địa chỉ mới", (await svc.ListAsync()).Single(c => c.CampusId == first.CampusId).Address);
    }

    [Fact]
    public async Task Update_UnknownCampus_IsNotFound()
    {
        var (_, svc) = Create();
        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.UpdateAsync(999, Req("PH9", "Không có")));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task Deactivate_WithActiveClasses_IsRejected_AndAllowedOnceClassesAreInactive()
    {
        var (db, svc) = Create();
        var campus = await svc.CreateAsync(Req("PH1", "Phân hiệu 1"));
        AddYear(db, 1, "2026-2027", D(2026, 9, 5), D(2027, 5, 31));
        var cls = AddClass(db, 11, campus.CampusId, 1, "6A");

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.SetActiveAsync(campus.CampusId, false));
        Assert.Contains("1 lớp", ex.Message);

        cls.IsActive = false;
        await db.SaveChangesAsync();
        await svc.SetActiveAsync(campus.CampusId, false);
        Assert.False((await svc.ListAsync()).Single().IsActive);

        await svc.SetActiveAsync(campus.CampusId, true);
        Assert.True((await svc.ListAsync()).Single().IsActive);
    }

    [Fact]
    public async Task List_ReportsOnlyActiveClassesInCount_AndOrdersByName()
    {
        var (db, svc) = Create();
        var b = await svc.CreateAsync(Req("PH2", "Phân hiệu B"));
        var a = await svc.CreateAsync(Req("PH1", "Phân hiệu A"));
        AddYear(db, 1, "2026-2027", D(2026, 9, 5), D(2027, 5, 31));
        AddClass(db, 11, a.CampusId, 1, "6A");
        AddClass(db, 12, a.CampusId, 1, "6B", active: false);

        var list = await svc.ListAsync();

        Assert.Equal(["Phân hiệu A", "Phân hiệu B"], list.Select(c => c.Name));
        Assert.Equal(1, list[0].ActiveClassCount);
        Assert.Equal(0, list[1].ActiveClassCount);
        Assert.Equal(b.CampusId, list[1].CampusId);
    }
}
