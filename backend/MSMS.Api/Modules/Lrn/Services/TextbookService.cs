using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Ai;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Storage;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Lrn.Dtos;

namespace MSMS.Api.Modules.Lrn.Services;

/// <summary>
/// LRN-01 (4.6.5, 4.6.6): mục lục sách giáo khoa và ảnh các trang để AI soạn câu hỏi bám sách. Ảnh mỗi trang lưu qua <see cref="IImageStorage"/>
/// (thư mục textbooks, tên <c>&lt;mã sách&gt;-p-&lt;trang PDF&gt;.jpg</c>) do công cụ tools/textbook-import tải lên; số trang với người dùng
/// luôn là số in trên sách, đổi sang trang PDF bằng độ lệch của từng cuốn.
/// </summary>
public sealed class TextbookService(MsmsDbContext db, QuestionService questions, IImageStorage storage)
{
    /// <summary>Số trang tối đa gửi AI mỗi lần; bài dài hơn thì không soạn được từ cả bài.</summary>
    public const int MaxPages = 10;

    /// <summary>Tên file ảnh của một trang trong kho ảnh; công cụ nạp sách đặt tên theo đúng quy ước này.</summary>
    public static string PageFileName(string bookCode, int pdfPage) => $"{bookCode}-p-{pdfPage:000}.jpg";

    public async Task<IReadOnlyList<TextbookLessonDto>> LessonsAsync(int subjectId, byte gradeLevel)
    {
        await questions.EnsureOwnSubjectAsync(subjectId);
        return await db.TextbookLesson.AsNoTracking()
            .Where(l => l.Book.SubjectId == subjectId && l.Book.GradeLevel == gradeLevel)
            .OrderBy(l => l.Book.Volume).ThenBy(l => l.SortOrder)
            .Select(l => new TextbookLessonDto(l.LessonId, l.Book.Title, l.Chapter, l.Title, l.StartPage, l.EndPage))
            .ToListAsync();
    }

    /// <summary>Bài kèm sách của nó, chỉ khi bài thuộc đúng môn và khối đang soạn.</summary>
    public async Task<TextbookLesson> GetLessonAsync(int lessonId, int subjectId, byte gradeLevel) =>
        await db.TextbookLesson.AsNoTracking().Include(l => l.Book)
            .FirstOrDefaultAsync(l => l.LessonId == lessonId && l.Book.SubjectId == subjectId && l.Book.GradeLevel == gradeLevel)
        ?? throw ApiException.BadRequest("Bài học không thuộc môn và khối đã chọn. Chọn lại bài.");

    /// <summary>Đọc ảnh các trang của bài để gửi AI.</summary>
    public async Task<IReadOnlyList<AiImage>> LoadLessonImagesAsync(TextbookLesson lesson, CancellationToken ct)
    {
        var count = lesson.EndPage - lesson.StartPage + 1;
        if (count > MaxPages)
            throw ApiException.BadRequest($"Bài này dài {count} trang, vượt giới hạn {MaxPages} trang mỗi lần. Soạn thủ công hoặc chọn bài ngắn hơn.");
        var images = new List<AiImage>(count);
        for (var page = lesson.StartPage; page <= lesson.EndPage; page++)
            images.Add(new AiImage("image/jpeg", await ReadPageAsync(lesson, page)));
        return images;
    }

    /// <summary>Ảnh một trang trong khoảng trang của bài, để giáo viên đối chiếu nguồn khi duyệt nháp.</summary>
    public async Task<byte[]> GetPageImageAsync(int lessonId, int printedPage)
    {
        var lesson = await db.TextbookLesson.AsNoTracking().Include(l => l.Book).FirstOrDefaultAsync(l => l.LessonId == lessonId)
            ?? throw ApiException.NotFound("Không tìm thấy bài học.");
        if (printedPage < lesson.StartPage || printedPage > lesson.EndPage) throw ApiException.NotFound("Trang không thuộc bài học này.");
        return await ReadPageAsync(lesson, printedPage);
    }

    private async Task<byte[]> ReadPageAsync(TextbookLesson lesson, int printedPage)
    {
        var name = PageFileName(lesson.Book.Code, printedPage + lesson.Book.PageOffset);
        var image = await storage.OpenAsync(ImageFolders.Textbooks, name)
            ?? throw ApiException.BadRequest("Chưa có ảnh trang sách cho bài này. Liên hệ quản trị nạp lại sách.");
        await using var content = image.Content;
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms);
        return ms.ToArray();
    }
}
