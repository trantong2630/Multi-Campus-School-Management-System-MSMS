using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data;
using MSMS.Api.Modules.Ppl.Dtos;
using MSMS.Api.Modules.Ppl.Services;
using static MSMS.Tests.Modules.Sch.SchTestSupport;

namespace MSMS.Tests.Modules.Ppl;

/// <summary>Email cán bộ là nơi nhận mã OTP khi quên mật khẩu (BR-04): email sai thì không được lưu, kể cả khi chỉ có dấu @.</summary>
public class StaffEmailTests
{
    private static (MsmsDbContext Db, StaffService Service) Create()
    {
        var db = NewDb();
        AddCampus(db, 1, "Phân hiệu 1");
        var user = UserOf(AccountTypes.SchoolAdmin, 900);
        return (db, new StaffService(db, ClockAt(), new ScopeResolver(db, user, ClockAt())));
    }

    private static StaffRequest Request(string? email) =>
        new("Nguyễn Văn An", new DateOnly(1985, 3, 2), "012345678901", "Male", null, email, 1, null, null, null);

    [Theory]
    [InlineData("nguyenvana@")]
    [InlineData("an dang@gmail.com")]
    [InlineData("an@@school.vn")]
    [InlineData("an@gmail")]
    public async Task AMalformedEmail_IsRejected_AndNothingIsSaved(string email)
    {
        var (db, svc) = Create();

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.CreateAsync(Request(email)));

        Assert.Contains("Email", ex.Message);
        Assert.Empty(db.Person);
    }
}
