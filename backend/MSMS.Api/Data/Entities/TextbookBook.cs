namespace MSMS.Api.Data.Entities;

/// <summary>Một cuốn sách giáo khoa (một tập) đã nạp để AI soạn câu hỏi bám sách (LRN-01). Ảnh trang nằm ở App_Data/textbooks/&lt;Code&gt;.</summary>
public partial class TextbookBook
{
    public int BookId { get; set; }

    public string Code { get; set; } = null!;

    public int SubjectId { get; set; }

    public byte GradeLevel { get; set; }

    public byte Volume { get; set; }

    public string Title { get; set; } = null!;

    /// <summary>Số trang PDF = số in + PageOffset. Khác nhau theo từng cuốn (bìa, trang phụ).</summary>
    public short PageOffset { get; set; }

    public short PdfPageCount { get; set; }

    public virtual Subject Subject { get; set; } = null!;

    public virtual ICollection<TextbookLesson> TextbookLesson { get; set; } = new List<TextbookLesson>();
}
