using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class Question
{
    public int QuestionId { get; set; }

    public int SubjectId { get; set; }

    public byte? GradeLevel { get; set; }

    public string? Topic { get; set; }

    /// <summary>Bài trong sách giáo khoa mà AI đã dựa vào để soạn câu này; trống với câu soạn tay.</summary>
    public int? LessonId { get; set; }

    /// <summary>Độ khó: Recognize, Understand, Apply, HighApply. Null là chưa phân loại (câu có từ trước khi có độ khó).</summary>
    public string? Difficulty { get; set; }

    public string Content { get; set; } = null!;

    public string? Explanation { get; set; }

    public string Status { get; set; } = null!;

    public bool IsAiGenerated { get; set; }

    public bool IsHidden { get; set; }

    public int CreatedByPersonId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Person CreatedByPerson { get; set; } = null!;

    public virtual ICollection<AssignmentQuestion> AssignmentQuestion { get; set; } = new List<AssignmentQuestion>();

    public virtual ICollection<QuestionOption> QuestionOption { get; set; } = new List<QuestionOption>();

    public virtual Subject Subject { get; set; } = null!;

    public virtual TextbookLesson? Lesson { get; set; }
}
