using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Storage;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Com.Dtos;
using MSMS.Api.Modules.Com.Services;
using MSMS.Tests.Modules.Iam;

namespace MSMS.Tests.Modules.Com;

/// <summary>COM-02, BR-27: School Admin chỉ đọc bảng tin; Principal, Vice Principal, Teacher đăng; chỉ Principal ẩn bài người khác.</summary>
public class PostAuthorTests
{
    private static PostService Service(AccountTestFixture f, string role, int personId) =>
        new(f.Db, AccountTestFixture.User(role, personId), f.Clock, new PostImageStorage(null!));

    private static Post AddPost(AccountTestFixture f, int authorId, string status = PostStatuses.Posted)
    {
        var p = new Post
        {
            AuthorPersonId = authorId, Title = "Tin", Content = "Nội dung", Visibility = PostVisibilities.School,
            Status = status, CreatedAt = f.Clock.Now,
        };
        f.Db.Post.Add(p);
        f.Db.SaveChanges();
        return p;
    }

    [Fact(DisplayName = "COM | Đăng bài | School Admin | bị từ chối")]
    public async Task SchoolAdmin_CannotPost()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(1, AccountTypes.SchoolAdmin);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            Service(f, AccountTypes.SchoolAdmin, 1).CreateAsync(new PostForm { Title = "Tin", Content = "Nội dung", Visibility = PostVisibilities.School }));

        Assert.Equal(403, ex.StatusCode);
    }

    [Theory(DisplayName = "COM | Đăng bài | Principal, Vice Principal, Teacher | đăng được")]
    [InlineData(AccountTypes.Principal)]
    [InlineData(AccountTypes.VicePrincipal)]
    [InlineData(AccountTypes.Teacher)]
    public async Task Leaders_AndTeachers_CanPost(string role)
    {
        using var f = new AccountTestFixture();
        f.AddAccount(2, role);

        var post = await Service(f, role, 2).CreateAsync(new PostForm { Title = "Tin", Content = "Nội dung", Visibility = PostVisibilities.School });

        Assert.Equal("Tin", post.Title);
    }

    [Fact(DisplayName = "COM | Ẩn bài người khác | School Admin | bị từ chối")]
    public async Task SchoolAdmin_CannotHideOthersPost()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(1, AccountTypes.SchoolAdmin);
        f.AddAccount(3, AccountTypes.Teacher);
        var post = AddPost(f, 3);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            Service(f, AccountTypes.SchoolAdmin, 1).HideAsync(post.PostId, new HidePostRequest("Vi phạm")));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact(DisplayName = "COM | Ẩn bài người khác | Principal ẩn bài Vice Principal, kể cả bài cũ của School Admin")]
    public async Task Principal_HidesViceAndLegacyAdminPosts()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(1, AccountTypes.SchoolAdmin);
        f.AddAccount(2, AccountTypes.Principal);
        f.AddAccount(4, AccountTypes.VicePrincipal);
        var vice = AddPost(f, 4);
        var admin = AddPost(f, 1);

        var service = Service(f, AccountTypes.Principal, 2);

        Assert.Equal(PostStatuses.Hidden, (await service.HideAsync(vice.PostId, new HidePostRequest("Vi phạm"))).Status);
        Assert.Equal(PostStatuses.Hidden, (await service.HideAsync(admin.PostId, new HidePostRequest("Bài cũ"))).Status);
    }

    [Fact(DisplayName = "COM | Ẩn bài người khác | Vice Principal và Teacher chỉ ẩn bài của mình")]
    public async Task ViceAndTeacher_CannotHideOthersPosts()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(2, AccountTypes.Principal);
        f.AddAccount(3, AccountTypes.Teacher);
        f.AddAccount(4, AccountTypes.VicePrincipal);
        var teacher = AddPost(f, 3);
        var principal = AddPost(f, 2);
        var vice = Service(f, AccountTypes.VicePrincipal, 4);

        var onTeacher = await Assert.ThrowsAsync<ApiException>(() => vice.HideAsync(teacher.PostId, new HidePostRequest("Vi phạm")));
        var onPrincipal = await Assert.ThrowsAsync<ApiException>(() => vice.HideAsync(principal.PostId, new HidePostRequest("Vi phạm")));

        Assert.Equal(403, onTeacher.StatusCode);
        Assert.Equal(403, onPrincipal.StatusCode);
    }

    [Fact(DisplayName = "COM | Đọc bảng tin | School Admin | vẫn đọc được bài Nội bộ")]
    public async Task SchoolAdmin_StillReadsInternalPosts()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(1, AccountTypes.SchoolAdmin);
        f.AddAccount(3, AccountTypes.Teacher);
        var p = new Post { AuthorPersonId = 3, Title = "Nội bộ", Content = "x", Visibility = PostVisibilities.Internal, Status = PostStatuses.Posted, CreatedAt = f.Clock.Now };
        f.Db.Post.Add(p);
        f.Db.SaveChanges();

        var page = await Service(f, AccountTypes.SchoolAdmin, 1).ListAsync(new PostQueryDto());

        Assert.Contains(page.Items, i => i.Title == "Nội bộ");
    }
}
