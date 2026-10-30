using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Lrn.Dtos;

namespace MSMS.Api.Modules.Lrn.Services;

/// <summary>
/// LRN-03 và LRN-04 (phía đọc): kết quả bài làm (BR-22). Không có bước công bố: học sinh thấy điểm, đáp án đúng, giải thích và
/// phân tích AI ngay sau khi nộp. Phụ huynh xem của con được liên kết; giáo viên phụ trách và tổ trưởng xem của học sinh trong bài.
/// Học sinh chưa nộp thì không bao giờ thấy đáp án.
/// </summary>
public sealed class ResultService(
    MsmsDbContext db, CurrentUser user, IClock clock, LrnAccess access, AssignmentService assignments, AttemptGrader grader)
{
    // ---- giáo viên ----

    /// <summary>Điểm từng học sinh trong danh sách được giao, kèm trạng thái phân tích AI.</summary>
    public async Task<ClassResultDto> ClassResultAsync(int assignmentId)
    {
        await access.LoadForViewAsync(assignmentId);
        await grader.FinalizeExpiredAsync(assignmentId, null);
        var detail = await assignments.GetAsync(assignmentId);

        var recipients = await db.AssignmentRecipient.AsNoTracking().Where(r => r.AssignmentId == assignmentId)
            .Select(r => new
            {
                r.StudentId, r.Student.Student.PersonCode, r.Student.Student.FullName,
                Sub = db.Submission.Where(s => s.AssignmentId == assignmentId && s.StudentId == r.StudentId)
                    .Select(s => new { s.SubmittedAt, s.Score, s.CorrectCount, Analysis = s.AiFeedback != null ? s.AiFeedback.Status : null }).FirstOrDefault(),
            }).ToListAsync();

        var now = clock.Now;
        var rows = recipients
            .Select(r => new ResultRowDto(r.StudentId, r.PersonCode, r.FullName, r.Sub?.SubmittedAt != null, r.Sub?.SubmittedAt, r.Sub?.Score,
                r.Sub?.CorrectCount, r.Sub?.SubmittedAt is { } at ? StaleAware(r.Sub.Analysis, at, now) : null))
            .OrderBy(r => r.StudentName, StringComparer.CurrentCultureIgnoreCase).ToList();
        var scores = rows.Where(r => r.Submitted && r.Score is not null).Select(r => r.Score!.Value).ToList();
        return new ClassResultDto(detail.Assignment, rows.Count, scores.Count,
            scores.Count == 0 ? null : Math.Round(scores.Average(), 2, MidpointRounding.AwayFromZero), rows);
    }

    /// <summary>Kết quả chi tiết của một học sinh cho giáo viên phụ trách hoặc tổ trưởng.</summary>
    public async Task<PracticeResultDto> StudentResultForTeacherAsync(int assignmentId, int studentId)
    {
        var a = await access.LoadForViewAsync(assignmentId);
        if (!await db.AssignmentRecipient.AsNoTracking().AnyAsync(r => r.AssignmentId == assignmentId && r.StudentId == studentId))
            throw ApiException.NotFound("Học sinh không thuộc danh sách được giao của bài này.");
        await grader.FinalizeExpiredAsync(assignmentId, studentId);
        return await BuildResultAsync(a, studentId);
    }

    // ---- học sinh và phụ huynh ----

    /// <summary>Phụ huynh: các con được liên kết, kèm lớp đang học.</summary>
    public async Task<IReadOnlyList<ChildDto>> MyChildrenAsync()
    {
        var links = await db.ParentStudentLink.AsNoTracking().Where(l => l.ParentId == user.PersonId)
            .Select(l => new { l.StudentId, l.Student.Student.PersonCode, l.Student.Student.FullName }).ToListAsync();
        var ids = links.Select(l => l.StudentId).ToList();
        var classes = await db.Enrollment.AsNoTracking().Where(EnrollmentRules.ActiveAt(clock.Today)).Where(e => ids.Contains(e.StudentId))
            .Select(e => new { e.StudentId, e.Class.Name }).ToListAsync();
        return links.OrderBy(l => l.FullName, StringComparer.CurrentCultureIgnoreCase)
            .Select(l => new ChildDto(l.StudentId, l.PersonCode, l.FullName, classes.FirstOrDefault(c => c.StudentId == l.StudentId)?.Name)).ToList();
    }

    /// <summary>Phụ huynh: các bài con đã nộp hoặc đã quá hạn, mới nhất trước. Bài con còn đang làm dở thì chưa hiện.</summary>
    public async Task<IReadOnlyList<ChildResultItemDto>> ChildResultsAsync(int studentId)
    {
        var sid = await access.ResolveStudentAsync(studentId);
        await grader.FinalizeExpiredAsync(null, sid);
        var rows = await db.AssignmentRecipient.AsNoTracking().Where(r => r.StudentId == sid)
            .Select(r => new
            {
                r.AssignmentId, r.Assignment.Title, r.Assignment.SubjectId, SubjectName = r.Assignment.Subject.Name,
                ClassName = r.Assignment.Class.Name, r.Assignment.DueAt, r.Assignment.DurationMinutes,
                QuestionCount = r.Assignment.AssignmentQuestion.Count,
                Sub = r.Assignment.Submission.Where(s => s.StudentId == sid)
                    .Select(s => new { s.StartedAt, s.SubmittedAt, s.Score, s.CorrectCount }).FirstOrDefault(),
            }).ToListAsync();
        var now = clock.Now;
        return rows
            .Select(r => new
            {
                Row = r,
                Done = r.Sub?.SubmittedAt != null
                       || AssignmentRules.IsPastDeadline(AssignmentRules.EffectiveDeadline(r.DueAt, r.DurationMinutes, r.Sub?.StartedAt), now),
            })
            .Where(x => x.Done)
            .OrderByDescending(x => x.Row.Sub?.SubmittedAt ?? x.Row.DueAt)
            .Select(x => new ChildResultItemDto(x.Row.AssignmentId, x.Row.Title, x.Row.SubjectId, x.Row.SubjectName, x.Row.ClassName, x.Row.DueAt,
                x.Row.Sub?.SubmittedAt != null, x.Row.Sub?.SubmittedAt, x.Row.Sub?.Score, x.Row.Sub?.CorrectCount, x.Row.QuestionCount))
            .ToList();
    }

    /// <summary>
    /// Kết quả một bài của học sinh (4.6.16). Chưa nộp mà còn hạn thì chưa có gì để xem; quá hạn mà chưa nộp thì hiện Không nộp,
    /// không kèm câu hỏi và đáp án.
    /// </summary>
    public async Task<PracticeResultDto> ResultAsync(int assignmentId, int? studentId)
    {
        var sid = await access.ResolveStudentAsync(studentId);
        await grader.FinalizeExpiredAsync(assignmentId, sid);
        var a = await db.Assignment.AsNoTracking().Include(x => x.Class).Include(x => x.Subject)
            .FirstOrDefaultAsync(x => x.AssignmentId == assignmentId && x.AssignmentRecipient.Any(r => r.StudentId == sid))
            ?? throw ApiException.NotFound("Không tìm thấy bài tập trong danh sách được giao.");
        var result = await BuildResultAsync(a, sid);
        if (!result.Submitted)
        {
            var started = await db.Submission.AsNoTracking().Where(s => s.AssignmentId == assignmentId && s.StudentId == sid)
                .Select(s => (DateTime?)s.StartedAt).FirstOrDefaultAsync();
            var deadline = AssignmentRules.EffectiveDeadline(a.DueAt, a.DurationMinutes, started);
            if (!AssignmentRules.IsPastDeadline(deadline, clock.Now))
                throw ApiException.BadRequest("Bài này chưa nộp nên chưa có kết quả.");
        }
        return result;
    }

    // ---- dùng chung ----

    /// <summary>Dựng kết quả: điểm, từng câu với lựa chọn và đáp án đúng, phân tích AI. Chưa nộp thì không có câu hỏi.</summary>
    private async Task<PracticeResultDto> BuildResultAsync(Assignment a, int studentId)
    {
        var student = await db.StudentProfile.AsNoTracking().Where(s => s.StudentId == studentId).Select(s => s.Student.FullName).FirstAsync();
        var submission = await db.Submission.AsNoTracking().Include(s => s.AiFeedback)
            .FirstOrDefaultAsync(s => s.AssignmentId == a.AssignmentId && s.StudentId == studentId && s.SubmittedAt != null);
        var lines = await grader.LoadLinesAsync(a.AssignmentId);
        var total = lines.Sum(l => l.Points);
        if (submission is null)
            return new PracticeResultDto(a.AssignmentId, a.Title, a.Subject.Name, a.Class.Name, student, a.DueAt, false, null, null, null, total, [], null);

        var answers = await db.SubmissionAnswer.AsNoTracking().Where(x => x.SubmissionId == submission.SubmissionId)
            .Select(x => new { x.AssignmentQuestionId, x.IsCorrect, x.EarnedPoints, Selected = x.SubmissionAnswerOption.Select(o => o.OptionId).ToList() })
            .ToListAsync();
        var questions = lines.Select(l =>
        {
            var ans = answers.FirstOrDefault(x => x.AssignmentQuestionId == l.AssignmentQuestionId);
            var selected = ans?.Selected ?? [];
            return new ResultQuestionDto(l.AssignmentQuestionId, l.OrderNo, l.Points, l.Question.Content, l.Question.Explanation,
                l.Question.QuestionOption.Count(o => o.IsCorrect) > 1, ans?.IsCorrect ?? false, ans?.EarnedPoints ?? 0m,
                l.Question.QuestionOption.OrderBy(o => o.OptionLabel)
                    .Select(o => new ResultOptionDto(o.OptionId, o.OptionLabel, o.Content, o.IsCorrect, selected.Contains(o.OptionId))).ToList());
        }).ToList();

        return new PracticeResultDto(a.AssignmentId, a.Title, a.Subject.Name, a.Class.Name, student, a.DueAt, true, submission.SubmittedAt,
            submission.Score, submission.CorrectCount, total, questions, await ResolveAnalysisAsync(submission));
    }

    private static string? StaleAware(string? status, DateTime submittedAt, DateTime now) =>
        status == AnalysisStatuses.Pending && now - submittedAt > AnalysisService.StaleAfter ? AnalysisStatuses.Failed : status;

    /// <summary>Phân tích kẹt ở Pending quá <see cref="AnalysisService.StaleAfter"/> (tiến trình dừng giữa chừng) thì coi là Lỗi.</summary>
    private async Task<AnalysisDto?> ResolveAnalysisAsync(Submission submission)
    {
        var f = submission.AiFeedback;
        if (f is null) return null;
        var status = f.Status;
        if (status == AnalysisStatuses.Pending && submission.SubmittedAt is { } at && clock.Now - at > AnalysisService.StaleAfter)
        {
            await db.AiFeedback.Where(x => x.FeedbackId == f.FeedbackId && x.Status == AnalysisStatuses.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, AnalysisStatuses.Failed)
                    .SetProperty(x => x.ErrorMessage, "Phân tích bị gián đoạn."));
            status = AnalysisStatuses.Failed;
        }
        return AnalysisService.ToDto(status, f.AiContent);
    }
}
