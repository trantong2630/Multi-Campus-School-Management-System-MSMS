using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace MSMS.Api.Modules.Lrn.Services;

/// <summary>
/// Dựng bản Word (.docx) của đề từ cùng dữ liệu với bản PDF để giáo viên chỉnh sửa: tiêu đề, các câu kèm điểm và bốn lựa chọn, đáp án và
/// giải thích ở trang riêng cuối file. Chỉ ghi tên phông (Times New Roman), phông hiển thị do máy mở file; không phụ thuộc máy chủ.
/// </summary>
internal static class ExamDocxWriter
{
    private const string FontName = "Times New Roman";
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public static byte[] Render(ExamPaperService.Paper a, bool includeAnswers)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body());
            AddStyles(main);
            var body = main.Document.Body!;
            var total = a.Lines.Sum(l => l.Points);

            body.Append(Center(a.Title, bold: true, size: 30, after: 40));
            body.Append(Center(a.ClassName is null ? $"Môn {a.Subject}" : $"Môn {a.Subject} · Lớp {a.ClassName}", size: 22, after: 20));
            body.Append(Center(Meta(a.DurationMinutes, a.Lines.Count, total), size: 22, after: 160));
            body.Append(Plain("Họ và tên: ..........................................................   Lớp: ..............", after: 80));
            if (!string.IsNullOrWhiteSpace(a.Instruction)) body.Append(Plain($"Lời dặn: {a.Instruction}", italic: true, after: 80));
            body.Append(Rule());

            foreach (var l in a.Lines)
            {
                var multiple = l.Options.Count(o => o.IsCorrect) > 1;
                var head = Para(keepNext: true, before: 140, after: 40);
                head.Append(Run($"Câu {l.OrderNo}", bold: true), Run($" ({Pts(l.Points)} điểm). ", bold: true));
                if (multiple) head.Append(Run("(Chọn tất cả đáp án đúng) ", italic: true));
                head.Append(Run(l.Content));
                body.Append(head);
                for (var i = 0; i < l.Options.Count; i++)
                {
                    var o = l.Options[i];
                    var p = Para(keepNext: i < l.Options.Count - 1, after: 20, left: 360);
                    p.Append(Run($"{o.Label}. {o.Content}"));
                    body.Append(p);
                }
            }

            if (includeAnswers)
            {
                body.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                body.Append(Center("ĐÁP ÁN VÀ GIẢI THÍCH", bold: true, size: 28, after: 40));
                body.Append(Center(a.ClassName is null ? $"{a.Title} · Môn {a.Subject}" : $"{a.Title} · Môn {a.Subject} · Lớp {a.ClassName}", size: 21, after: 120));
                body.Append(Rule());
                foreach (var l in a.Lines)
                {
                    var correct = string.Join(", ", l.Options.Where(o => o.IsCorrect).Select(o => o.Label));
                    var p = Para(keepNext: !string.IsNullOrWhiteSpace(l.Explanation), before: 100, after: 20);
                    p.Append(Run($"Câu {l.OrderNo}: ", bold: true), Run(correct, bold: true), Run($"   ({Pts(l.Points)} điểm)", size: 20));
                    body.Append(p);
                    if (!string.IsNullOrWhiteSpace(l.Explanation))
                    {
                        var e = Para(after: 20, left: 360);
                        e.Append(Run(l.Explanation, size: 21));
                        body.Append(e);
                    }
                }
            }

            var section = new SectionProperties();
            var footerPart = main.AddNewPart<FooterPart>();
            footerPart.Footer = new Footer(PageNumberParagraph());
            section.Append(new FooterReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(footerPart) });
            section.Append(new PageSize { Width = 11906, Height = 16838 });
            section.Append(new PageMargin { Top = 1134, Bottom = 1134, Left = 1134, Right = 1134, Header = 567, Footer = 567 });
            body.Append(section);
            main.Document.Save();
        }
        return ms.ToArray();
    }

    private static string Pts(decimal v) => v.ToString("0.##", Vi);

    private static string Meta(short? duration, int count, decimal total) =>
        $"{count} câu · {Pts(total)} điểm" + (duration is null ? "" : $" · Thời gian làm bài {duration} phút");

    private static void AddStyles(MainDocumentPart main)
    {
        var styles = main.AddNewPart<StyleDefinitionsPart>();
        styles.Styles = new Styles(new DocDefaults(
            new RunPropertiesDefault(new RunPropertiesBaseStyle(
                new RunFonts { Ascii = FontName, HighAnsi = FontName, EastAsia = FontName, ComplexScript = FontName },
                new FontSize { Val = "24" }, new FontSizeComplexScript { Val = "24" },
                new Languages { Val = "vi-VN", EastAsia = "vi-VN", Bidi = "vi-VN" })),
            new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
                new SpacingBetweenLines { After = "0", Line = "276", LineRule = LineSpacingRuleValues.Auto }))));
    }

    private static Paragraph PageNumberParagraph()
    {
        Run Piece(OpenXmlElement child) => new(new RunProperties(new FontSize { Val = "18" }), child);
        return new Paragraph(
            new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
            Run("Trang ", size: 18),
            Piece(new FieldChar { FieldCharType = FieldCharValues.Begin }),
            Piece(new FieldCode(" PAGE ") { Space = SpaceProcessingModeValues.Preserve }),
            Piece(new FieldChar { FieldCharType = FieldCharValues.Separate }),
            Piece(new Text("1")),
            Piece(new FieldChar { FieldCharType = FieldCharValues.End }));
    }

    private static Run Run(string text, bool bold = false, bool italic = false, int? size = null)
    {
        var props = new RunProperties();
        if (bold) props.Append(new Bold());
        if (italic) props.Append(new Italic());
        if (size is not null)
        {
            props.Append(new FontSize { Val = size.Value.ToString() });
            props.Append(new FontSizeComplexScript { Val = size.Value.ToString() });
        }
        return new Run(props, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    /// <summary>Đoạn văn không cho tách giữa hai trang (keepLines); keepNext giữ đoạn dính với đoạn sau để câu hỏi và lựa chọn không rời nhau.</summary>
    private static Paragraph Para(bool keepNext = false, int before = 0, int after = 0, int left = 0)
    {
        var props = new ParagraphProperties();
        if (keepNext) props.Append(new KeepNext());
        props.Append(new KeepLines());
        props.Append(new SpacingBetweenLines { Before = before.ToString(), After = after.ToString() });
        if (left > 0) props.Append(new Indentation { Left = left.ToString() });
        return new Paragraph(props);
    }

    private static Paragraph Center(string text, bool bold = false, int size = 24, int after = 0) =>
        new(new ParagraphProperties(new SpacingBetweenLines { After = after.ToString() }, new Justification { Val = JustificationValues.Center }),
            Run(text, bold, size: size));

    private static Paragraph Plain(string text, bool italic = false, int after = 0) =>
        new(new ParagraphProperties(new SpacingBetweenLines { After = after.ToString() }), Run(text, italic: italic, size: 22));

    private static Paragraph Rule() => new(new ParagraphProperties(
        new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = 6, Space = 1, Color = "000000" }),
        new SpacingBetweenLines { After = "60" }));
}
