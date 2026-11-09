using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Data.Entities;

namespace MSMS.Api.Data;

/// <summary>
/// LRN-01: nạp mục lục sách giáo khoa vào DB từ Data/Textbooks/&lt;mã sách&gt;.json do tools/textbook-import tạo (ảnh trang nằm trên Cloudinary).
/// Chạy ở mọi môi trường khi API khởi động. Chạy lại được: bài đã có (theo thứ tự) chỉ được cập nhật, không xóa, vì câu hỏi có thể đang trỏ tới.
/// </summary>
public static class TextbookSeeder
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private sealed record BookFile(string Code, string Subject, byte Grade, byte Volume, string Title, short PageOffset, short PdfPageCount, List<LessonFile> Lessons);
    private sealed record LessonFile(string Chapter, short Order, string Title, short StartPage, short EndPage);

    public static async Task RunAsync(MsmsDbContext db, string contentRoot, ILogger logger)
    {
        var root = Path.Combine(contentRoot, "Data", "Textbooks");
        if (!Directory.Exists(root)) return;

        foreach (var file in Directory.EnumerateFiles(root, "*.json"))
        {
            BookFile? data;
            try { data = JsonSerializer.Deserialize<BookFile>(await File.ReadAllTextAsync(file), Json); }
            catch (JsonException ex) { logger.LogWarning(ex, "Textbook: {File} không đọc được, bỏ qua", file); continue; }
            if (data is null || data.Lessons is null) continue;

            var subject = await db.Subject.FirstOrDefaultAsync(s => s.Name == data.Subject);
            if (subject is null) { logger.LogWarning("Textbook: không có môn {Subject} cho sách {Code}, bỏ qua", data.Subject, data.Code); continue; }

            var book = await db.TextbookBook.Include(b => b.TextbookLesson).FirstOrDefaultAsync(b => b.Code == data.Code);
            if (book is null)
            {
                book = new TextbookBook { Code = data.Code };
                db.TextbookBook.Add(book);
            }
            book.SubjectId = subject.SubjectId;
            book.GradeLevel = data.Grade;
            book.Volume = data.Volume;
            book.Title = data.Title;
            book.PageOffset = data.PageOffset;
            book.PdfPageCount = data.PdfPageCount;

            foreach (var l in data.Lessons)
            {
                var lesson = book.TextbookLesson.FirstOrDefault(x => x.SortOrder == l.Order);
                if (lesson is null) book.TextbookLesson.Add(lesson = new TextbookLesson { SortOrder = l.Order });
                lesson.Chapter = l.Chapter;
                lesson.Title = l.Title;
                lesson.StartPage = l.StartPage;
                lesson.EndPage = l.EndPage;
            }
            await db.SaveChangesAsync();
            logger.LogInformation("Textbook: {Code} ({Count} bài)", data.Code, data.Lessons.Count);
        }
    }
}
