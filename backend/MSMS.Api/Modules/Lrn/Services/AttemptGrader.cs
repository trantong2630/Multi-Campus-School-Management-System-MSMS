using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;

namespace MSMS.Api.Modules.Lrn.Services;

/// <summary>
/// Chốt một bài nộp (BR-21): lưu câu trả lời, chấm ngay, tạo dòng phân tích AI và đẩy việc phân tích đi. Dùng chung cho nộp bài
/// của học sinh và tự nộp khi hết hạn thực tế. Hai đường đua nhau thì chỉ một đường lật được <c>SubmittedAt</c>.
/// </summary>
public sealed class AttemptGrader(MsmsDbContext db, IClock clock, AnalysisJob analysis)
{
    public Task<List<AssignmentQuestion>> LoadLinesAsync(int assignmentId) =>
        db.AssignmentQuestion.AsNoTracking().Include(x => x.Question).ThenInclude(q => q.QuestionOption)
            .Where(x => x.AssignmentId == assignmentId).OrderBy(x => x.OrderNo).ToListAsync();

    /// <summary>Thay toàn bộ nháp bằng trạng thái hiện tại. Gọi trong transaction của người gọi.</summary>
    public async Task ReplaceAnswersAsync(long submissionId, Dictionary<int, List<int>> answers)
    {
        await db.SubmissionAnswerOption.Where(o => o.Answer.SubmissionId == submissionId).ExecuteDeleteAsync();
        await db.SubmissionAnswer.Where(x => x.SubmissionId == submissionId).ExecuteDeleteAsync();
        foreach (var (assignmentQuestionId, optionIds) in answers.Where(x => x.Value.Count > 0))
        {
            var answer = new SubmissionAnswer { SubmissionId = submissionId, AssignmentQuestionId = assignmentQuestionId };
            foreach (var optionId in optionIds) answer.SubmissionAnswerOption.Add(new SubmissionAnswerOption { OptionId = optionId });
            db.SubmissionAnswer.Add(answer);
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    /// <summary>
    /// Lật bài sang đã nộp tại <paramref name="submittedAt"/>, lưu <paramref name="answers"/> nếu có (null là giữ nháp đang có), chấm điểm
    /// và tạo dòng phân tích Pending, tất cả trong một transaction. Trả false nếu bài đã được nộp ở nơi khác.
    /// </summary>
    public async Task<bool> FinalizeAsync(long submissionId, int assignmentId, DateTime submittedAt, Dictionary<int, List<int>>? answers)
    {
        var lines = await LoadLinesAsync(assignmentId);
        await using var tx = await db.Database.BeginTransactionAsync();
        // Cập nhật có điều kiện giữ khóa hàng tới hết transaction: một đường nộp khác phải chờ rồi thấy bài đã nộp.
        var flipped = await db.Submission.Where(s => s.SubmissionId == submissionId && s.SubmittedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.SubmittedAt, submittedAt).SetProperty(x => x.LastSavedAt, submittedAt));
        if (flipped == 0) return false;

        if (answers is not null) await ReplaceAnswersAsync(submissionId, answers);
        await GradeAsync(submissionId, lines);
        db.AiFeedback.Add(new AiFeedback { SubmissionId = submissionId, Status = AnalysisStatuses.Pending });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        db.ChangeTracker.Clear();

        analysis.Enqueue(submissionId);
        return true;
    }

    /// <summary>
    /// Tự nộp bản nháp đã quá hạn thực tế (BR-21). Không có job nền: chạy lúc bài được truy cập sau hạn. Chỉ nháp có ít nhất một
    /// câu trả lời mới được nộp và chấm; nháp rỗng thì để là Không nộp. Bài nộp lúc hạn thực tế.
    /// </summary>
    public async Task FinalizeExpiredAsync(int? assignmentId, int? studentId)
    {
        var now = clock.Now;
        var q = db.Submission.AsNoTracking().Where(s => s.SubmittedAt == null && s.SubmissionAnswer.Any(a => a.SubmissionAnswerOption.Any()));
        if (assignmentId is not null) q = q.Where(s => s.AssignmentId == assignmentId);
        if (studentId is not null) q = q.Where(s => s.StudentId == studentId);
        var rows = await q.Select(s => new { s.SubmissionId, s.AssignmentId, s.StartedAt, s.Assignment.DueAt, s.Assignment.DurationMinutes }).ToListAsync();
        foreach (var r in rows)
        {
            var deadline = AssignmentRules.EffectiveDeadline(r.DueAt, r.DurationMinutes, r.StartedAt);
            if (!AssignmentRules.IsPastDeadline(deadline, now)) continue;
            await FinalizeAsync(r.SubmissionId, r.AssignmentId, deadline, null);
        }
    }

    /// <summary>
    /// Ghi kết quả từng câu cho mọi câu của bài (câu bỏ trống tính là sai) rồi tính điểm:
    /// tổng điểm câu đúng / tổng điểm bài × 10, làm tròn 2 chữ số thập phân (BR-21).
    /// </summary>
    private async Task GradeAsync(long submissionId, List<AssignmentQuestion> lines)
    {
        var stored = await db.SubmissionAnswer.Include(x => x.SubmissionAnswerOption).Where(x => x.SubmissionId == submissionId).ToListAsync();
        decimal earned = 0;
        short correctCount = 0;
        foreach (var line in lines)
        {
            var correctIds = line.Question.QuestionOption.Where(o => o.IsCorrect).Select(o => o.OptionId).ToList();
            var answer = stored.FirstOrDefault(x => x.AssignmentQuestionId == line.AssignmentQuestionId);
            if (answer is null)
            {
                answer = new SubmissionAnswer { SubmissionId = submissionId, AssignmentQuestionId = line.AssignmentQuestionId };
                db.SubmissionAnswer.Add(answer);
            }
            var selected = answer.SubmissionAnswerOption.Select(o => o.OptionId).ToList();
            var isCorrect = QuizRules.IsAnswerCorrect(selected, correctIds);
            answer.IsCorrect = isCorrect;
            answer.EarnedPoints = isCorrect ? line.Points : 0m;
            if (isCorrect) { earned += line.Points; correctCount++; }
        }
        var score = QuizRules.ScoreOutOfTen(earned, lines.Sum(l => l.Points));
        await db.SaveChangesAsync();
        await db.Submission.Where(s => s.SubmissionId == submissionId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Score, score).SetProperty(x => x.CorrectCount, correctCount));
    }
}
