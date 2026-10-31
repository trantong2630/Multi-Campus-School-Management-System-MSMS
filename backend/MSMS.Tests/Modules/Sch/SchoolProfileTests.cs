using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data;
using MSMS.Api.Modules.Sch.Controllers;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;
using static MSMS.Tests.Modules.Sch.SchTestSupport;

namespace MSMS.Tests.Modules.Sch;

/// <summary>
/// 4.2.43: thông tin trường (SCH-07). Đúng một bản; tên bắt buộc; logo JPG/PNG tối đa 2 MB kiểm theo nội dung file; đổi hoặc gỡ logo xóa file cũ;
/// chưa có tên thì không tải được logo; ai cũng đọc được tên và logo nhưng chỉ Hiệu trưởng và School Admin sửa được (BR-27).
/// </summary>
public sealed class SchoolProfileTests : IDisposable
{
    private const long TwoMegabytes = 2 * 1024 * 1024;
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpgHeader = [0xFF, 0xD8, 0xFF, 0xE0];

    private readonly string _root = Path.Combine(Path.GetTempPath(), "msms-school-tests-" + Guid.NewGuid().ToString("N"));
    private string LogoDirectory => Path.Combine(_root, "App_Data", "uploads", "school");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class FakeEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "MSMS.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private (MsmsDbContext Db, SchoolProfileService Service) Create()
    {
        var db = NewDb();
        var user = UserOf(AccountTypes.Principal, personId: 7);
        return (db, new SchoolProfileService(db, user, ClockAt(), new FakeEnvironment(_root)));
    }

    private static SchoolProfileRequest Profile(string name = "Trường THCS Ánh Sáng", string? email = null, string? website = null) =>
        new(name, "12 Lê Lợi, Hà Nội", "0243123456", email, website);

    private static byte[] Image(byte[] header, long size)
    {
        var bytes = new byte[size];
        header.CopyTo(bytes, 0);
        return bytes;
    }

    private static IFormFile Upload(byte[] bytes, string fileName = "logo.png", string contentType = "image/png") =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName) { Headers = new HeaderDictionary(), ContentType = contentType };

    private string[] LogoFiles() => Directory.Exists(LogoDirectory) ? Directory.GetFiles(LogoDirectory) : [];

    // ---- Xem và sửa thông tin (4.2.39, 4.2.40) ----
    [Fact]
    public async Task Get_BeforeAnythingIsSaved_ReturnsEmptyProfile_SoTheUiCanUseTheDefaultName()
    {
        var (_, svc) = Create();

        var profile = await svc.GetAsync();

        Assert.Null(profile.Name);
        Assert.False(profile.HasLogo);
    }

    [Fact]
    public async Task Update_SavesTheFieldsNormalized_AndStampsWhoAndWhen()
    {
        var (db, svc) = Create();

        var saved = await svc.UpdateAsync(new SchoolProfileRequest("  Trường   THCS Ánh Sáng ", " 12 Lê Lợi ", " 0243123456 ", "info@school.vn", "https://school.vn"));

        Assert.Equal("Trường THCS Ánh Sáng", saved.Name);
        Assert.Equal("12 Lê Lợi", saved.Address);
        var row = await db.SchoolProfile.SingleAsync();
        Assert.Equal(7, row.UpdatedByPersonId);
        Assert.Equal(Today, DateOnly.FromDateTime(row.UpdatedAt!.Value));
    }

    [Fact]
    public async Task Update_Twice_KeepsExactlyOneProfile()
    {
        var (db, svc) = Create();

        await svc.UpdateAsync(Profile("Tên cũ"));
        await svc.UpdateAsync(Profile("Tên mới"));

        Assert.Equal("Tên mới", (await db.SchoolProfile.SingleAsync()).Name);
        Assert.Equal("Tên mới", (await svc.GetAsync()).Name);
    }

    [Fact]
    public async Task Update_WithoutName_OrTooLongName_IsRejected()
    {
        var (db, svc) = Create();

        await Assert.ThrowsAsync<ApiException>(() => svc.UpdateAsync(Profile("   ")));
        await Assert.ThrowsAsync<ApiException>(() => svc.UpdateAsync(Profile(new string('A', 201))));

        Assert.Empty(db.SchoolProfile);
    }

    [Theory]
    [InlineData("khong-co-a-cong")]
    [InlineData("nguoi dung@")]
    public async Task Update_WithAnInvalidEmail_IsRejected(string email)
    {
        var (_, svc) = Create();
        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.UpdateAsync(Profile(email: email)));
        Assert.Contains("Email", ex.Message);
    }

    [Theory]
    [InlineData("school.vn")]
    [InlineData("ftp://school.vn")]
    [InlineData("javascript:alert(1)")]
    public async Task Update_WithAWebsiteThatIsNotHttpOrHttps_IsRejected(string website)
    {
        var (_, svc) = Create();
        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.UpdateAsync(Profile(website: website)));
        Assert.Contains("http", ex.Message);
    }

    [Fact]
    public async Task Update_AcceptsEmptyOptionalFields()
    {
        var (_, svc) = Create();

        var saved = await svc.UpdateAsync(new SchoolProfileRequest("Trường A", null, null, "", ""));

        Assert.Null(saved.Email);
        Assert.Null(saved.Website);
    }

    // ---- Logo (4.2.41) ----
    [Fact]
    public async Task Logo_CannotBeUploaded_UntilTheSchoolNameIsSaved()
    {
        var (_, svc) = Create();

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.SetLogoAsync(Upload(Image(PngHeader, 1000))));

        Assert.Contains("Nhập tên trường", ex.Message);
        Assert.Empty(LogoFiles());
    }

    [Fact]
    public async Task Png_IsAccepted_AndCanBeServedWithItsContentType()
    {
        var (_, svc) = Create();
        await svc.UpdateAsync(Profile());

        var saved = await svc.SetLogoAsync(Upload(Image(PngHeader, 2048)));

        Assert.True(saved.HasLogo);
        Assert.NotNull(saved.LogoVersion);
        var logo = await svc.FindLogoAsync();
        Assert.Equal("image/png", logo!.Value.ContentType);
        Assert.True(File.Exists(logo.Value.Path));
    }

    [Fact]
    public async Task Jpg_IsAccepted_AsJpeg()
    {
        var (_, svc) = Create();
        await svc.UpdateAsync(Profile());

        await svc.SetLogoAsync(Upload(Image(JpgHeader, 2048), "logo.jpg", "image/jpeg"));

        Assert.Equal("image/jpeg", (await svc.FindLogoAsync())!.Value.ContentType);
    }

    [Theory]
    [InlineData("logo.png", "image/png")]    // đuôi và Content-Type nói là ảnh nhưng nội dung không phải
    [InlineData("logo.jpg", "image/jpeg")]
    [InlineData("logo.exe", "application/octet-stream")]
    public async Task AFileThatIsNotAnImage_IsRejected_EvenIfTheExtensionAndContentTypeSayOtherwise(string fileName, string contentType)
    {
        var (_, svc) = Create();
        await svc.UpdateAsync(Profile());
        var notImage = System.Text.Encoding.UTF8.GetBytes("MZ... day khong phai anh ...................");

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.SetLogoAsync(Upload(notImage, fileName, contentType)));

        Assert.Contains("JPG hoặc PNG", ex.Message);
        Assert.Empty(LogoFiles());
        Assert.False((await svc.GetAsync()).HasLogo);
    }

    [Fact]
    public async Task AValidImageDisguisedWithAWrongExtension_IsStillAccepted_BecauseOnlyTheContentCounts()
    {
        var (_, svc) = Create();
        await svc.UpdateAsync(Profile());

        var saved = await svc.SetLogoAsync(Upload(Image(PngHeader, 500), "logo.txt", "text/plain"));

        Assert.True(saved.HasLogo);
        Assert.EndsWith(".png", Assert.Single(LogoFiles()));
    }

    [Fact]
    public async Task ALogoOverTwoMegabytes_IsRejected_ButExactlyTwoMegabytesIsAccepted()
    {
        var (_, svc) = Create();
        await svc.UpdateAsync(Profile());

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.SetLogoAsync(Upload(Image(PngHeader, TwoMegabytes + 1))));
        Assert.Contains("2 MB", ex.Message);
        Assert.Empty(LogoFiles());

        var saved = await svc.SetLogoAsync(Upload(Image(PngHeader, TwoMegabytes)));
        Assert.True(saved.HasLogo);
    }

    [Fact]
    public async Task AnEmptyFile_IsRejected()
    {
        var (_, svc) = Create();
        await svc.UpdateAsync(Profile());
        await Assert.ThrowsAsync<ApiException>(() => svc.SetLogoAsync(Upload([])));
    }

    [Fact]
    public async Task ReplacingTheLogo_DeletesTheOldFile()
    {
        var (_, svc) = Create();
        await svc.UpdateAsync(Profile());
        await svc.SetLogoAsync(Upload(Image(PngHeader, 1000)));
        var oldPath = (await svc.FindLogoAsync())!.Value.Path;

        await svc.SetLogoAsync(Upload(Image(JpgHeader, 1000), "moi.jpg", "image/jpeg"));

        Assert.False(File.Exists(oldPath));
        var only = Assert.Single(LogoFiles());
        Assert.EndsWith(".jpg", only);
        Assert.Equal(only, (await svc.FindLogoAsync())!.Value.Path);
    }

    [Fact]
    public async Task ARejectedUpload_LeavesTheExistingLogoUntouched()
    {
        var (_, svc) = Create();
        await svc.UpdateAsync(Profile());
        await svc.SetLogoAsync(Upload(Image(PngHeader, 1000)));
        var before = (await svc.FindLogoAsync())!.Value.Path;

        await Assert.ThrowsAsync<ApiException>(() => svc.SetLogoAsync(Upload(System.Text.Encoding.UTF8.GetBytes("khong phai anh"))));

        Assert.True(File.Exists(before));
        Assert.Equal(before, (await svc.FindLogoAsync())!.Value.Path);
    }

    [Fact]
    public async Task RemovingTheLogo_DeletesTheFile_AndRemovingAgainIsHarmless()
    {
        var (_, svc) = Create();
        await svc.UpdateAsync(Profile());
        await svc.SetLogoAsync(Upload(Image(PngHeader, 1000)));

        var removed = await svc.RemoveLogoAsync();

        Assert.False(removed.HasLogo);
        Assert.Empty(LogoFiles());
        Assert.Null(await svc.FindLogoAsync());
        Assert.False((await svc.RemoveLogoAsync()).HasLogo);   // chưa có logo thì không lỗi
    }

    [Fact]
    public async Task SavingTheProfile_DoesNotTouchTheLogo()
    {
        var (_, svc) = Create();
        await svc.UpdateAsync(Profile());
        await svc.SetLogoAsync(Upload(Image(PngHeader, 1000)));

        var saved = await svc.UpdateAsync(Profile("Tên mới của trường"));

        Assert.True(saved.HasLogo);
        Assert.Single(LogoFiles());
    }

    [Fact]
    public async Task FindLogo_ReturnsNull_WhenTheFileIsMissingOnDisk()
    {
        var (_, svc) = Create();
        await svc.UpdateAsync(Profile());
        await svc.SetLogoAsync(Upload(Image(PngHeader, 1000)));
        File.Delete(LogoFiles().Single());

        Assert.Null(await svc.FindLogoAsync());
    }

    [Fact]
    public async Task FindLogo_NeverLeavesTheLogoDirectory_EvenIfTheStoredNameHasPathParts()
    {
        var (db, svc) = Create();
        await svc.UpdateAsync(Profile());
        Directory.CreateDirectory(LogoDirectory);
        await File.WriteAllBytesAsync(Path.Combine(LogoDirectory, "evil.png"), PngHeader);
        (await db.SchoolProfile.SingleAsync()).LogoPath = Path.Combine("..", "..", "evil.png");
        await db.SaveChangesAsync();

        var logo = await svc.FindLogoAsync();

        Assert.Equal(Path.Combine(LogoDirectory, "evil.png"), logo!.Value.Path);
    }

    // ---- Ai đọc, ai sửa (4.2.42, BR-27) ----
    private static MethodInfo Endpoint(string name) =>
        typeof(SchoolController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;

    [Theory]
    [InlineData(nameof(SchoolController.Get))]
    [InlineData(nameof(SchoolController.Logo))]
    public void ReadingTheNameAndLogo_DoesNotRequireLogin(string endpoint)
    {
        var method = Endpoint(endpoint);

        Assert.NotNull(method.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.All(method.GetCustomAttributes<HttpMethodAttribute>(), a => Assert.Contains("GET", a.HttpMethods));
    }

    [Theory]
    [InlineData(nameof(SchoolController.Update))]
    [InlineData(nameof(SchoolController.UploadLogo))]
    [InlineData(nameof(SchoolController.RemoveLogo))]
    public void ChangingTheProfileOrLogo_IsOnlyForThePrincipalAndSchoolAdmin(string endpoint)
    {
        var method = Endpoint(endpoint);

        Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal(Policies.SchoolProfileManager, method.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
    }
}
