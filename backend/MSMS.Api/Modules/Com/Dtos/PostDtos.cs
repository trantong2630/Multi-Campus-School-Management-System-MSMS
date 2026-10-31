namespace MSMS.Api.Modules.Com.Dtos;

/// <summary>Một dòng trên danh sách bảng tin (COM-02). Ảnh đầu là ảnh bìa (BR-27).</summary>
public record PostListItemDto(
    int PostId,
    string Title,
    string Excerpt,
    string Visibility,
    string Status,
    int AuthorPersonId,
    string AuthorName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int? CoverImageId,
    int ImageCount,
    bool CanEdit,
    bool CanHide,
    bool CanUnhide,
    bool CanDelete
);

public record PostImageDto(int PostImageId, int OrderNo);

public record PostDetailDto(
    int PostId,
    string Title,
    string Content,
    string Visibility,
    string Status,
    int AuthorPersonId,
    string AuthorName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<PostImageDto> Images,
    string? HiddenByName,
    DateTime? HiddenAt,
    string? HiddenReason,
    bool CanEdit,
    bool CanHide,
    bool CanUnhide,
    bool CanDelete
);

public class PostQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    /// <summary>Internal hoặc School; bỏ trống là cả hai (chỉ cán bộ mới thấy bài Internal).</summary>
    public string? Visibility { get; set; }

    /// <summary>Posted (mặc định) hoặc Hidden — Hidden chỉ có nghĩa với cán bộ.</summary>
    public string? Status { get; set; }
}

public class PostForm
{
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? Visibility { get; set; }
    public IFormFileCollection? Files { get; set; }
}

public record PostUpdateRequest(string? Title, string? Content, string? Visibility);

public record HidePostRequest(string? Reason);

public class PostImagesForm
{
    public IFormFileCollection? Files { get; set; }
}
