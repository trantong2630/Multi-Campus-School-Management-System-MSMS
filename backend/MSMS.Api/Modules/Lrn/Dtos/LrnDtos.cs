namespace MSMS.Api.Modules.Lrn.Dtos;

// ---- LRN-01: Ngân hàng câu hỏi ----

public record OptionInputDto(string Label, string Content, bool IsCorrect);

/// <summary>Lựa chọn của câu hỏi, chỉ giáo viên thấy <c>IsCorrect</c>.</summary>
public record OptionDto(int OptionId, string Label, string Content, bool IsCorrect);

public record QuestionItemDto(
    int QuestionId, int SubjectId, string SubjectName, byte? GradeLevel, string? Topic, string Content,
    string Status, bool IsAiGenerated, bool IsHidden, bool IsMultiple,
    int CreatedByPersonId, string CreatedByName, bool IsMine, bool IsLocked, DateTime CreatedAt);

public record QuestionDetailDto(
    int QuestionId, int SubjectId, string SubjectName, byte? GradeLevel, string? Topic, string Content, string? Explanation,
    string Status, bool IsAiGenerated, bool IsHidden, bool IsMultiple,
    int CreatedByPersonId, string CreatedByName, bool IsMine, bool IsLocked, DateTime CreatedAt,
    IReadOnlyList<OptionDto> Options);

public record SaveQuestionDto(int SubjectId, byte GradeLevel, string? Topic, string? Content, string? Explanation, List<OptionInputDto>? Options);

public record GenerateQuestionsDto(int SubjectId, byte GradeLevel, string? Topic, string? Textbook, int Count);
public record GenerateQuestionsResultDto(IReadOnlyList<QuestionDetailDto> Drafts, int Discarded);

// ---- LRN-02: Giao bài (giáo viên) ----

public record ClassSubjectOptionDto(int ClassId, string ClassName, byte GradeLevel, int SubjectId, string SubjectName);

public record AssignmentLineDto(int QuestionId, decimal Points);

public record CreateAssignmentDto(
    int SubjectId, int ClassId, string? Title, string? Instruction, DateTime? OpenAt, DateTime? DueAt, short? DurationMinutes,
    List<AssignmentLineDto>? Items);

public record AssignmentItemDto(
    int AssignmentId, string Title, int ClassId, string ClassName, int SubjectId, string SubjectName,
    DateTime OpenAt, DateTime DueAt, short? DurationMinutes, int QuestionCount, int RecipientCount, int SubmittedCount,
    bool IsClosed, string AssignedByName, bool IsMine);

public record AssignmentQuestionDto(
    int AssignmentQuestionId, int QuestionId, int OrderNo, decimal Points, string Content, string? Explanation,
    bool IsMultiple, IReadOnlyList<OptionDto> Options);

public record AssignmentDetailDto(
    AssignmentItemDto Assignment, string? Instruction, decimal TotalPoints, IReadOnlyList<AssignmentQuestionDto> Questions);

// ---- LRN-02: Làm bài (học sinh) ----

/// <summary>Đề đúng như học sinh thấy: không có đáp án đúng, không có giải thích.</summary>
public record QuizOptionDto(int OptionId, string Label, string Content);
public record QuizQuestionDto(int AssignmentQuestionId, int OrderNo, decimal Points, string Content, bool IsMultiple, IReadOnlyList<QuizOptionDto> Options);

public record AnswerInputDto(int AssignmentQuestionId, List<int>? OptionIds);
public record SaveAnswersDto(List<AnswerInputDto>? Answers);

/// <summary>Trạng thái bài với học sinh: NotOpen, Open, InProgress (đã bắt đầu hoặc có nháp), Submitted, NotSubmitted (quá hạn mà chưa nộp).</summary>
public record MyAssignmentItemDto(
    int AssignmentId, string Title, int SubjectId, string SubjectName, string ClassName, DateTime OpenAt, DateTime DueAt,
    short? DurationMinutes, int QuestionCount, string State, DateTime? SubmittedAt, decimal? Score, short? CorrectCount);

/// <summary>Bài có thời lượng mà học sinh chưa bấm Bắt đầu thì <c>Questions</c> rỗng và <c>Started</c> là false.</summary>
public record AttemptDto(
    int AssignmentId, string Title, string? Instruction, int SubjectId, string SubjectName, string ClassName, DateTime OpenAt, DateTime DueAt,
    short? DurationMinutes, bool Started, DateTime? StartedAt, DateTime Deadline, DateTime ServerNow, int QuestionCount, decimal TotalPoints,
    DateTime? LastSavedAt, IReadOnlyList<QuizQuestionDto> Questions, IReadOnlyList<AnswerInputDto> Answers);

public record SubmitResultDto(DateTime SubmittedAt);

// ---- LRN-03 / LRN-04: Kết quả và phân tích ----

public record ResultRowDto(
    int StudentId, string StudentCode, string StudentName, bool Submitted, DateTime? SubmittedAt, decimal? Score, short? CorrectCount, string? AnalysisStatus);

public record ClassResultDto(
    AssignmentItemDto Assignment, int RecipientCount, int SubmittedCount, decimal? AverageScore, IReadOnlyList<ResultRowDto> Rows);

public record ChildDto(int StudentId, string StudentCode, string FullName, string? ClassName);

public record ChildResultItemDto(
    int AssignmentId, string Title, int SubjectId, string SubjectName, string ClassName, DateTime DueAt,
    bool Submitted, DateTime? SubmittedAt, decimal? Score, short? CorrectCount, int QuestionCount);

public record ResultOptionDto(int OptionId, string Label, string Content, bool IsCorrect, bool Selected);

public record ResultQuestionDto(
    int AssignmentQuestionId, int OrderNo, decimal Points, string Content, string? Explanation, bool IsMultiple,
    bool? IsCorrect, decimal? EarnedPoints, IReadOnlyList<ResultOptionDto> Options);

public record AnalysisMistakeDto(int QuestionNo, string Why);

/// <summary>Phân tích của MSMS AI cho một bài nộp. Status: Pending (đang phân tích), Done, Failed (để trống, không thử lại).</summary>
public record AnalysisDto(
    string Status, string? Summary, IReadOnlyList<string> Strengths, IReadOnlyList<string> Weaknesses,
    IReadOnlyList<AnalysisMistakeDto> Mistakes, IReadOnlyList<string> Suggestions);

/// <summary>Học sinh chưa nộp: <c>Submitted</c> là false, không có câu hỏi và đáp án.</summary>
public record PracticeResultDto(
    int AssignmentId, string Title, string SubjectName, string ClassName, string StudentName, DateTime DueAt,
    bool Submitted, DateTime? SubmittedAt, decimal? Score, short? CorrectCount, decimal TotalPoints,
    IReadOnlyList<ResultQuestionDto> Questions, AnalysisDto? Analysis);

// ---- LRN-05: Tiến độ giao bài của tổ ----

public record SubjectOptionDto(int SubjectId, string Name);

public record TeacherProgressDto(int PersonId, string Name, int AssignmentCount, int OpenCount);

public record DepartmentItemDto(
    int AssignmentId, string Title, int CampusId, string CampusName, int ClassId, string ClassName, int SubjectId, string SubjectName,
    string AssignedByName, DateTime OpenAt, DateTime DueAt, bool IsClosed, int RecipientCount, int SubmittedCount);

public record DepartmentOverviewDto(
    bool IsHead, IReadOnlyList<SubjectOptionDto> Subjects, IReadOnlyList<TeacherProgressDto> Teachers, IReadOnlyList<DepartmentItemDto> Items);
