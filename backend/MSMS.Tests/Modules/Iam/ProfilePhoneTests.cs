using Moq;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Storage;
using MSMS.Api.Modules.Iam.Dtos;
using MSMS.Api.Modules.Iam.Services;

namespace MSMS.Tests.Modules.Iam;

/// <summary>4.1.28: tự sửa số điện thoại theo cùng quy tắc với School Admin và import (di động 10 chữ số, để trống được).</summary>
public class ProfilePhoneTests
{
    private static ProfileService Profile(AccountTestFixture f, int personId) =>
        new(f.Db, AccountTestFixture.User(AccountTypes.Teacher, personId), f.Clock, Mock.Of<IImageStorage>());

    [Theory(DisplayName = "IAM | Hồ sơ cá nhân | Số điện thoại sai định dạng di động | bị từ chối")]
    [InlineData("12345")]
    [InlineData("0212345678")]
    [InlineData("09123456789")]
    [InlineData("09a2345678")]
    public async Task InvalidMobile_IsRejected(string phone)
    {
        using var f = new AccountTestFixture();
        f.AddAccount(5, AccountTypes.Teacher);

        var ex = await Assert.ThrowsAsync<ApiException>(() => Profile(f, 5).UpdateAsync(new UpdateProfileRequest(phone, null, null)));
        Assert.Equal(Guard.PhoneRuleMessage, ex.Message);
    }

    [Fact(DisplayName = "IAM | Hồ sơ cá nhân | Số di động hợp lệ hoặc để trống | lưu được")]
    public async Task ValidMobileOrEmpty_IsSaved()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(5, AccountTypes.Teacher);

        await Profile(f, 5).UpdateAsync(new UpdateProfileRequest("0912345678", null, null));
        Assert.Equal("0912345678", f.Db.Person.Find(5)!.PhoneNumber);

        await Profile(f, 5).UpdateAsync(new UpdateProfileRequest(null, null, null));
        Assert.Null(f.Db.Person.Find(5)!.PhoneNumber);
    }
}
