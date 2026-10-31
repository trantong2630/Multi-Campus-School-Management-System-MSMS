using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Com.Dtos;

namespace MSMS.Api.Modules.Com.Services;

public static class PostVisibilities
{
    public const string Internal = "Internal";   // Nội bộ: chỉ cán bộ đọc
    public const string School = "School";       // Toàn trường: mọi người đọc
}

public static class PostStatuses
{
    public const string Posted = "Posted";
    public const string Hidden = "Hidden";
}

/// <summary>
/// Bảng tin (COM-02, BR-27). Mọi cán bộ đăng và chọn phạm vi; học sinh, phụ huynh chỉ đọc bài Toàn trường đang đăng.
/// Tác giả sửa, ẩn, xóa bài của mình; School Admin và Principal ẩn được bài của người khác kèm lý do.
/// Bảng tin không tạo thông báo nên không gọi NotificationSender.
/// </summary>
public sealed class PostService(MsmsDbContext db, CurrentUser user, IClock clock, PostImageStorage storage)
{
    public const int MaxImages = 5;
    private const int MaxPageSize = 50;
    private const int MaxTitle = 200;
    private const int MaxContent = 5000;
    private const int MaxReason = 500;
    private const int ExcerptLength = 200;

    private bool IsStaff => user.AccountType is AccountTypes.SchoolAdmin or AccountTypes.Principal or AccountTypes.VicePrincipal or AccountTypes.Teacher;
    private bool CanModerate => user.AccountType is AccountTypes.SchoolAdmin or AccountTypes.Principal;

    /// <summary>Bài người đọc được thấy. Bài đã ẩn chỉ hiện cho tác giả, người ẩn và School Admin/Principal.</summary>
    private IQueryable<Post> VisibleToMe()
    {
        var q = db.Post.AsNoTracking();
        if (!IsStaff) return q.Where(p => p.Status == PostStatuses.Posted && p.Visibility == PostVisibilities.School);
        var me = user.PersonId;
        var moderate = CanModerate;
        return q.Where(p => p.Status == PostStatuses.Posted || moderate || p.AuthorPersonId == me || p.HiddenByPersonId == me);
    }

    // ---- 4.8.11 Xem bảng tin ----

    public async Task<PagedResult<PostListItemDto>> ListAsync(PostQueryDto q)
    {
        var posts = VisibleToMe();
        var wantHidden = IsStaff && q.Status == PostStatuses.Hidden;
        posts = posts.Where(p => p.Status == (wantHidden ? PostStatuses.Hidden : PostStatuses.Posted));
        if (q.Visibility is PostVisibilities.Internal or PostVisibilities.School)
            posts = posts.Where(p => p.Visibility == q.Visibility);

        var page = await posts
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.PostId)
            .Select(p => new Row(
                p.PostId, p.Title, p.Content, p.Visibility, p.Status, p.AuthorPersonId, p.AuthorPerson.FullName,
                p.CreatedAt, p.UpdatedAt, p.HiddenByPersonId,
                p.PostImage.OrderBy(i => i.OrderNo).ThenBy(i => i.PostImageId).Select(i => (int?)i.PostImageId).FirstOrDefault(),
                p.PostImage.Count))
            .ToPagedAsync(q.Page, Math.Min(q.PageSize, MaxPageSize));

        return new PagedResult<PostListItemDto>(page.Items.Select(ToItem).ToList(), page.Total);
    }

    public async Task<PostDetailDto> GetAsync(int id)
    {
        var p = await VisibleToMe().Include(x => x.AuthorPerson).Include(x => x.HiddenByPerson).Include(x => x.PostImage)
            .FirstOrDefaultAsync(x => x.PostId == id) ?? throw NotFound();
        var images = p.PostImage.OrderBy(i => i.OrderNo).ThenBy(i => i.PostImageId).Select(i => new PostImageDto(i.PostImageId, i.OrderNo)).ToList();
        return new PostDetailDto(
            p.PostId, p.Title, p.Content, p.Visibility, p.Status, p.AuthorPersonId, p.AuthorPerson.FullName,
            p.CreatedAt, p.UpdatedAt, images, p.HiddenByPerson?.FullName, p.HiddenAt, p.HiddenReason,
            CanEdit(p.AuthorPersonId), CanHide(p.Status, p.AuthorPersonId), CanUnhide(p.Status, p.HiddenByPersonId), CanDelete(p.AuthorPersonId));
    }

    /// <summary>Đường dẫn file ảnh nếu người đọc được xem bài chứa ảnh đó.</summary>
    public async Task<(string Path, string ContentType)> FindImageAsync(int postId, int imageId)
    {
        var file = await VisibleToMe().Where(p => p.PostId == postId)
            .SelectMany(p => p.PostImage).Where(i => i.PostImageId == imageId).Select(i => i.FilePath)
            .FirstOrDefaultAsync() ?? throw NotFound();
        return storage.Find(file) ?? throw NotFound();
    }

    // ---- 4.8.12, 4.8.13 Soạn bài, đính kèm ảnh ----

    public async Task<PostDetailDto> CreateAsync(PostForm form)
    {
        RequireStaff();
        var title = Guard.Required(form.Title, "Tiêu đề", MaxTitle);
        var content = Guard.Required(form.Content, "Nội dung", MaxContent);
        var visibility = ParseVisibility(form.Visibility);
        var files = form.Files?.ToList() ?? [];
        if (files.Count > MaxImages) throw TooManyImages();
        var images = await ReadAllAsync(files);

        var now = clock.Now;
        var post = new Post
        {
            AuthorPersonId = user.PersonId, Title = title, Content = content, Visibility = visibility,
            Status = PostStatuses.Posted, CreatedAt = now,
        };
        var saved = new List<string>();
        try
        {
            await AddImagesAsync(post, images, saved, 1, now);
            db.Post.Add(post);
            await db.SaveChangesAsync();
        }
        catch
        {
            saved.ForEach(storage.Delete);
            throw;
        }
        return await GetAsync(post.PostId);
    }

    public async Task<PostDetailDto> AddImagesAsync(int id, IFormFileCollection? files)
    {
        var post = await FindOwnAsync(id);
        var list = files?.ToList() ?? [];
        if (list.Count == 0) throw ApiException.BadRequest("Chưa chọn ảnh. Chọn file JPG hoặc PNG rồi tải lên.");
        if (post.PostImage.Count + list.Count > MaxImages) throw TooManyImages();
        var images = await ReadAllAsync(list);

        var next = post.PostImage.Count == 0 ? 1 : post.PostImage.Max(i => i.OrderNo) + 1;
        var saved = new List<string>();
        try
        {
            await AddImagesAsync(post, images, saved, next, clock.Now);
            post.UpdatedAt = clock.Now;
            await db.SaveChangesAsync();
        }
        catch
        {
            saved.ForEach(storage.Delete);
            throw;
        }
        return await GetAsync(id);
    }

    public async Task<PostDetailDto> RemoveImageAsync(int id, int imageId)
    {
        var post = await FindOwnAsync(id);
        var image = post.PostImage.FirstOrDefault(i => i.PostImageId == imageId)
            ?? throw ApiException.NotFound("Ảnh không tồn tại trong bài đăng này.");
        db.PostImage.Remove(image);
        post.UpdatedAt = clock.Now;
        await db.SaveChangesAsync();
        storage.Delete(image.FilePath);
        return await GetAsync(id);
    }

    // ---- 4.8.14 Cập nhật bài của mình ----

    public async Task<PostDetailDto> UpdateAsync(int id, PostUpdateRequest r)
    {
        var post = await FindOwnAsync(id);
        post.Title = Guard.Required(r.Title, "Tiêu đề", MaxTitle);
        post.Content = Guard.Required(r.Content, "Nội dung", MaxContent);
        post.Visibility = ParseVisibility(r.Visibility);
        post.UpdatedAt = clock.Now;
        await db.SaveChangesAsync();
        return await GetAsync(id);
    }

    // ---- 4.8.15 Ẩn và hiện lại ----

    public async Task<PostDetailDto> HideAsync(int id, HidePostRequest r)
    {
        RequireStaff();
        var post = await db.Post.FirstOrDefaultAsync(p => p.PostId == id) ?? throw NotFound();
        if (post.Status == PostStatuses.Hidden) throw ApiException.Conflict("Bài đăng này đã được ẩn.");

        string? reason;
        if (post.AuthorPersonId == user.PersonId) reason = Guard.Optional(r.Reason, "Lý do", MaxReason);
        else if (CanModerate) reason = Guard.Required(r.Reason, "Lý do ẩn bài", MaxReason);
        else throw ApiException.Forbidden("Chỉ tác giả, School Admin hoặc Hiệu trưởng mới ẩn được bài này.");

        post.Status = PostStatuses.Hidden;
        post.HiddenByPersonId = user.PersonId;
        post.HiddenAt = clock.Now;
        post.HiddenReason = reason;
        await db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task<PostDetailDto> UnhideAsync(int id)
    {
        RequireStaff();
        var post = await db.Post.FirstOrDefaultAsync(p => p.PostId == id) ?? throw NotFound();
        if (post.Status != PostStatuses.Hidden) throw ApiException.Conflict("Bài đăng này đang hiển thị.");
        if (post.HiddenByPersonId != user.PersonId)
            throw ApiException.Forbidden("Chỉ người đã ẩn bài mới hiện lại được bài này.");

        post.Status = PostStatuses.Posted;
        post.HiddenByPersonId = null;
        post.HiddenAt = null;
        post.HiddenReason = null;
        await db.SaveChangesAsync();
        return await GetAsync(id);
    }

    // ---- 4.8.16 Xóa bài và file ảnh ----

    public async Task DeleteAsync(int id)
    {
        var post = await FindOwnAsync(id);
        var files = post.PostImage.Select(i => i.FilePath).ToList();
        db.PostImage.RemoveRange(post.PostImage);
        db.Post.Remove(post);
        await db.SaveChangesAsync();
        files.ForEach(storage.Delete);
    }

    // ---- helpers ----

    private void RequireStaff()
    {
        if (!IsStaff) throw ApiException.Forbidden("Chỉ cán bộ mới thao tác được với bài đăng.");
    }

    /// <summary>Bài của chính mình (kể cả đã ẩn), kèm danh sách ảnh để theo dõi thay đổi.</summary>
    private async Task<Post> FindOwnAsync(int id)
    {
        RequireStaff();
        var post = await db.Post.Include(p => p.PostImage).FirstOrDefaultAsync(p => p.PostId == id && p.AuthorPersonId == user.PersonId);
        return post ?? throw ApiException.NotFound("Bài đăng không tồn tại hoặc không phải bài của bạn.");
    }

    private bool CanEdit(int authorId) => IsStaff && authorId == user.PersonId;
    private bool CanDelete(int authorId) => CanEdit(authorId);
    private bool CanHide(string status, int authorId) =>
        IsStaff && status == PostStatuses.Posted && (authorId == user.PersonId || CanModerate);
    private bool CanUnhide(string status, int? hiddenBy) => IsStaff && status == PostStatuses.Hidden && hiddenBy == user.PersonId;

    private static string ParseVisibility(string? v) => v switch
    {
        PostVisibilities.Internal or PostVisibilities.School => v,
        _ => throw ApiException.BadRequest("Chọn phạm vi bài đăng: Nội bộ hoặc Toàn trường."),
    };

    private static ApiException NotFound() => ApiException.NotFound("Bài đăng không tồn tại hoặc không còn hiển thị.");
    private static ApiException TooManyImages() => ApiException.BadRequest($"Mỗi bài tối đa {MaxImages} ảnh. Bỏ bớt ảnh rồi thử lại.");

    private async Task<List<(byte[] Bytes, string Extension)>> ReadAllAsync(List<IFormFile> files)
    {
        var result = new List<(byte[], string)>();
        foreach (var f in files) result.Add(await storage.ReadAsync(f));
        return result;
    }

    private async Task AddImagesAsync(Post post, List<(byte[] Bytes, string Extension)> images, List<string> saved, int firstOrder, DateTime now)
    {
        var order = firstOrder;
        foreach (var (bytes, ext) in images)
        {
            var name = await storage.SaveAsync(bytes, ext);
            saved.Add(name);
            post.PostImage.Add(new PostImage { FilePath = name, OrderNo = (byte)order++, FileSizeBytes = bytes.Length, UploadedAt = now });
        }
    }

    private PostListItemDto ToItem(Row r) => new(
        r.PostId, r.Title, Excerpt(r.Content), r.Visibility, r.Status, r.AuthorPersonId, r.AuthorName, r.CreatedAt, r.UpdatedAt,
        r.CoverImageId, r.ImageCount,
        CanEdit(r.AuthorPersonId), CanHide(r.Status, r.AuthorPersonId), CanUnhide(r.Status, r.HiddenByPersonId), CanDelete(r.AuthorPersonId));

    private static string Excerpt(string content) =>
        content.Length <= ExcerptLength ? content : content[..ExcerptLength].TrimEnd() + "…";

    private sealed record Row(
        int PostId, string Title, string Content, string Visibility, string Status, int AuthorPersonId, string AuthorName,
        DateTime CreatedAt, DateTime? UpdatedAt, int? HiddenByPersonId, int? CoverImageId, int ImageCount);
}
