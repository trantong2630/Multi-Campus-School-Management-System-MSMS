namespace MSMS.Api.Data.Entities;

/// <summary>Một bài (hoặc bài luyện tập, ôn tập) trong mục lục sách; trang tính theo số in trên sách.</summary>
public partial class TextbookLesson
{
    public int LessonId { get; set; }

    public int BookId { get; set; }

    public string Chapter { get; set; } = null!;

    public short SortOrder { get; set; }

    public string Title { get; set; } = null!;

    public short StartPage { get; set; }

    public short EndPage { get; set; }

    public virtual TextbookBook Book { get; set; } = null!;

    public virtual ICollection<Question> Question { get; set; } = new List<Question>();
}
