using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Ppl.Services;
using MSMS.Tests.Modules.Iam;

namespace MSMS.Tests.Modules.Ppl;

/// <summary>Giới tính của phụ huynh: lưu khi sửa, hiện trong danh bạ, nhận nhãn Nam/Nữ/Khác, từ chối giá trị lạ.</summary>
public class GuardianGenderTests
{
    private static GuardianService Setup(out AccountTestFixture f)
    {
        f = new AccountTestFixture();
        f.Db.Person.Add(new Person { PersonId = 50, PersonType = "Parent", PersonCode = "PH000050", FullName = "Nguyễn Thị Lan",
            PhoneNumber = "0912345678", CreatedAt = f.Clock.Now });
        f.Db.SaveChanges();
        var user = AccountTestFixture.User(AccountTypes.SchoolAdmin, 900);
        return new GuardianService(f.Db, f.Clock, new ScopeResolver(f.Db, user, f.Clock));
    }

    [Fact]
    public async Task Gender_is_saved_on_update_and_shown_in_the_list_and_can_be_cleared()
    {
        var svc = Setup(out _);
        Assert.Null((await svc.ListAsync(null, 1, 20)).Items.Single().Gender);

        var updated = await svc.UpdateAsync(50, new("Nguyễn Thị Lan", "0912345678", null, null, "Nữ"));
        Assert.Equal("Female", updated.Gender);
        Assert.Equal("Female", (await svc.ListAsync(null, 1, 20)).Items.Single().Gender);

        var cleared = await svc.UpdateAsync(50, new("Nguyễn Thị Lan", "0912345678", null, null));
        Assert.Null(cleared.Gender);
    }

    [Fact]
    public async Task Unknown_gender_values_are_rejected()
    {
        var svc = Setup(out _);

        var onUpdate = await Assert.ThrowsAsync<ApiException>(() => svc.UpdateAsync(50, new("Nguyễn Thị Lan", "0912345678", null, null, "robot")));
        Assert.Equal(400, onUpdate.StatusCode);
        var onCreate = await Assert.ThrowsAsync<ApiException>(() => svc.CreateAsync(new("Lê Văn C", "0911111111", null, null, "robot")));
        Assert.Equal(400, onCreate.StatusCode);
    }
}