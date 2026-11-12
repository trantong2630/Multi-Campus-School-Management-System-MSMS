using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Text;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Lrn.Dtos;
using MSMS.Api.Modules.Scd.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MSMS.Api.Modules.Lrn.Services;

/// <summary>
/// Tạo bản in đề (PDF A4) của một bài đã giao để giáo viên tải về và in: đề gồm các câu theo thứ tự kèm điểm từng câu, đáp án và
/// giải thích ở trang riêng cuối file khi chọn kèm đáp án. Chỉ đọc bài đã giao; quyền như xem kết quả (BR-22): giáo viên đang
/// phụ trách chuyên môn ở (lớp, môn) hoặc tổ trưởng của môn.
/// </summary>
public sealed class ExamPaperService(MsmsDbContext db, LrnAccess access, SubjectResponsibilityService responsibility, CurrentUser user)
{
    private static readonly string[] Fonts = ["Times New Roman", "Arial", "DejaVu Serif", "Liberation Serif"];

    /// <summary>
    /// Phông tiếng Việt lấy từ hệ điều hành (Windows có Times New Roman; Linux cần cài fonts-liberation hoặc DejaVu). Thiếu cả bốn phông thì
    /// tạo file báo lỗi thay vì in chữ lỗi: đề kiểm tra không được in sai chữ.
    /// </summary>
    static ExamPaperService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;
        QuestPDF.Settings.ThrowOnMissingFontFamilies = false;
    }

    public const string Pdf = "pdf";
    public const string Docx = "docx";

    /// <summary>Bản in đề của một bài đã giao (PDF hoặc Word).</summary>
    public async Task<PaperFile> BuildAsync(int assignmentId, bool includeAnswers, string? format)
    {
        var a = await access.LoadForViewAsync(assignmentId);
        var lines = await db.AssignmentQuestion.AsNoTracking().Include(x => x.Question).ThenInclude(q => q.QuestionOption)
            .Where(x => x.AssignmentId == assignmentId).OrderBy(x => x.OrderNo).ToListAsync();
        var data = new Paper(a.Title, a.Subject.Name, a.Class.Name, a.Instruction, a.DurationMinutes,
            lines.Select(l => new PaperLine(l.OrderNo, l.Question.Content, l.Points, l.Question.Explanation,
                l.Question.QuestionOption.OrderBy(o => o.OptionLabel).Select(o => new PaperOption(o.OptionLabel, o.Content, o.IsCorrect)).ToList())).ToList());
        return Package(data, includeAnswers, format);
    }

    /// <summary>
    /// Bản in đề từ một đề đang soạn, chưa giao: không cần lớp hay hạn nộp. Cùng quy tắc với giao bài (câu Sẵn dùng cùng môn đã có độ khó,
    /// thang điểm các phần cộng lại 10) nên đề in ra đúng như đề sẽ giao. Môn phải là môn người gọi đang phụ trách ở ít nhất một lớp.
    /// </summary>
    public async Task<PaperFile> ExportAsync(ExportPaperDto dto)
    {
        var subject = await db.Subject.AsNoTracking().FirstOrDefaultAsync(s => s.SubjectId == dto.SubjectId && s.IsActive)
            ?? throw ApiException.BadRequest("Môn học không tồn tại hoặc đã ngừng hoạt động. Chọn môn khác.");
        var mine = await responsibility.ForTeacherAsync(user.PersonId);
        if (mine.All(p => p.SubjectId != subject.SubjectId))
            throw ApiException.Forbidden($"Chỉ giáo viên đang phụ trách chuyên môn {subject.Name} mới tạo bản in đề của môn này. Liên hệ hiệu phó kiểm tra phụ trách chuyên môn.");

        var title = Guard.Required(dto.Title, "Tên đề", 200);
        var instruction = Guard.Optional(dto.Instruction, "Lời dặn", 500);
        if (dto.DurationMinutes is < 1 or > AssignmentRules.MaxDurationMinutes)
            throw ApiException.BadRequest($"Thời lượng làm bài từ 1 đến {AssignmentRules.MaxDurationMinutes} phút. Để trống nếu không giới hạn.");
        var ids = (dto.Items ?? []).Select(i => i.QuestionId).ToList();
        if (ids.Count == 0) throw ApiException.BadRequest("Chọn ít nhất một câu hỏi cho đề.");
        if (ids.Count > QuizRules.MaxQuestionsPerAssignment) throw ApiException.BadRequest($"Một đề tối đa {QuizRules.MaxQuestionsPerAssignment} câu hỏi.");
        if (ids.Distinct().Count() != ids.Count) throw ApiException.BadRequest("Mỗi câu hỏi chỉ xuất hiện một lần trong đề.");

        var questions = await db.Question.AsNoTracking().Include(q => q.QuestionOption).Where(q => ids.Contains(q.QuestionId)).ToListAsync();
        if (questions.Count != ids.Count) throw ApiException.BadRequest("Có câu hỏi không còn tồn tại. Tải lại ngân hàng câu hỏi rồi chọn lại.");
        if (questions.Any(q => q.SubjectId != subject.SubjectId))
            throw ApiException.BadRequest($"Đề môn {subject.Name} chỉ gồm câu hỏi cùng môn. Bỏ các câu thuộc môn khác.");
        if (questions.Any(q => q.Status != QuestionStatuses.Ready || q.IsHidden))
            throw ApiException.BadRequest("Có câu hỏi chưa Sẵn dùng hoặc đã bị ẩn khỏi ngân hàng. Bỏ các câu đó rồi thử lại.");
        if (questions.Any(q => q.Difficulty is null))
            throw ApiException.BadRequest("Có câu hỏi chưa phân loại độ khó. Chọn độ khó cho các câu đó (hoặc bỏ chúng) rồi thử lại.");

        var points = QuizRules.AssignPoints(ids, questions.ToDictionary(q => q.QuestionId, q => q.Difficulty!),
            (dto.Sections ?? []).Select(s => (s.Difficulty, s.Points)).ToList());
        var byId = questions.ToDictionary(q => q.QuestionId);
        var lines = ids.Select((id, i) => new PaperLine(i + 1, byId[id].Content, points[id], byId[id].Explanation,
            byId[id].QuestionOption.OrderBy(o => o.OptionLabel).Select(o => new PaperOption(o.OptionLabel, o.Content, o.IsCorrect)).ToList())).ToList();
        return Package(new Paper(title, subject.Name, null, instruction, dto.DurationMinutes, lines), dto.WithAnswers, dto.Format);
    }

    /// <summary>Một file bản in đề: nội dung, loại nội dung và tên file.</summary>
    public sealed record PaperFile(byte[] Content, string ContentType, string FileName);

    internal static byte[] RenderDocx(Paper a, bool includeAnswers) => ExamDocxWriter.Render(a, includeAnswers);

    private static PaperFile Package(Paper data, bool includeAnswers, string? format)
    {
        if (string.Equals(format, Docx, StringComparison.OrdinalIgnoreCase))
            return new PaperFile(ExamDocxWriter.Render(data, includeAnswers),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document", FileName(data.Title, "docx"));
        return new PaperFile(Render(data, includeAnswers), "application/pdf", FileName(data.Title, "pdf"));
    }

    internal sealed record PaperOption(string Label, string Content, bool IsCorrect);
    internal sealed record PaperLine(int OrderNo, string Content, decimal Points, string? Explanation, IReadOnlyList<PaperOption> Options);
    internal sealed record Paper(string Title, string Subject, string? ClassName, string? Instruction, short? DurationMinutes, IReadOnlyList<PaperLine> Lines);

    internal static byte[] Render(Paper a, bool includeAnswers)
    {
        var lines = a.Lines;
        var total = lines.Sum(l => l.Points);
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                Setup(page);
                page.Header().ShowOnce().Column(h =>
                {
                    h.Item().AlignCenter().Text(a.Title).Bold().FontSize(15);
                    h.Item().AlignCenter().Text(a.ClassName is null ? $"Môn {a.Subject}" : $"Môn {a.Subject} · Lớp {a.ClassName}").FontSize(11);
                    h.Item().AlignCenter().Text(Meta(a.DurationMinutes, lines.Count, total)).FontSize(11);
                    h.Item().PaddingTop(8).Text("Họ và tên: ..........................................................   Lớp: ..............").FontSize(11);
                    if (!string.IsNullOrWhiteSpace(a.Instruction)) h.Item().PaddingTop(4).Text($"Lời dặn: {a.Instruction}").Italic().FontSize(10.5f);
                    h.Item().PaddingTop(6).LineHorizontal(0.75f);
                });
                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Spacing(9);
                    foreach (var l in lines)
                    {
                        var multiple = l.Options.Count(o => o.IsCorrect) > 1;
                        col.Item().EnsureSpace(90).Column(q =>
                        {
                            q.Spacing(2);
                            q.Item().Text(t =>
                            {
                                t.Span($"Câu {l.OrderNo}").Bold();
                                t.Span($" ({Pts(l.Points)} điểm). ").Bold();
                                if (multiple) t.Span("(Chọn tất cả đáp án đúng) ").Italic();
                                t.Span(l.Content);
                            });
                            foreach (var o in l.Options)
                                q.Item().PaddingLeft(16).Text($"{o.Label}. {o.Content}");
                        });
                    }
                });
                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Trang ").FontSize(9); t.CurrentPageNumber().FontSize(9);
                    t.Span(" / ").FontSize(9); t.TotalPages().FontSize(9);
                });
            });

            if (!includeAnswers) return;
            doc.Page(page =>
            {
                Setup(page);
                page.Header().ShowOnce().Column(h =>
                {
                    h.Item().AlignCenter().Text("ĐÁP ÁN VÀ GIẢI THÍCH").Bold().FontSize(14);
                    h.Item().AlignCenter().Text(a.ClassName is null ? $"{a.Title} · Môn {a.Subject}" : $"{a.Title} · Môn {a.Subject} · Lớp {a.ClassName}").FontSize(10.5f);
                    h.Item().PaddingTop(6).LineHorizontal(0.75f);
                });
                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Spacing(6);
                    foreach (var l in lines)
                    {
                        var correct = string.Join(", ", l.Options.Where(o => o.IsCorrect).Select(o => o.Label));
                        col.Item().EnsureSpace(40).Column(q =>
                        {
                            q.Item().Text(t =>
                            {
                                t.Span($"Câu {l.OrderNo}: ").Bold();
                                t.Span(correct).Bold();
                                t.Span($"   ({Pts(l.Points)} điểm)").FontSize(10);
                            });
                            if (!string.IsNullOrWhiteSpace(l.Explanation))
                                q.Item().PaddingLeft(16).Text(l.Explanation).FontSize(10.5f);
                        });
                    }
                });
                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Đáp án - trang ").FontSize(9); t.CurrentPageNumber().FontSize(9);
                });
            });
        }).GeneratePdf();
    }

    private static void Setup(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(2, Unit.Centimetre);
        page.DefaultTextStyle(t => t.FontFamily(Fonts).FontSize(11.5f));
    }

    private static string Meta(short? duration, int count, decimal total) =>
        $"{count} câu · {Pts(total)} điểm" + (duration is null ? "" : $" · Thời gian làm bài {duration} phút");

    private static string Pts(decimal v) => v.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));

    /// <summary>Tên file bỏ ký tự không hợp lệ; tên trống thì dùng tên mặc định.</summary>
    private static string FileName(string title, string extension)
    {
        // Bỏ dấu tiếng Việt: tiêu đề Content-Disposition chỉ an toàn với ký tự ASCII, có dấu thì trình duyệt đổi thành gạch dưới.
        var plain = TextNormalizer.RemoveDiacritics(title);
        var clean = string.Concat(plain.Where(c => c < 128 && !Path.GetInvalidFileNameChars().Contains(c))).Trim();
        return (clean.Length == 0 ? "de-bai-tap" : clean.Length > 80 ? clean[..80] : clean) + "." + extension;
    }
}
