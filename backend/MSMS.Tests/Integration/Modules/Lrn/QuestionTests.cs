using MSMS.Api.Data.Entities;
using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Lrn;

/// <summary>LRN-01: ngân hàng câu hỏi, độ khó, duyệt nháp, ma trận đề (BR-20). Tên test bắt đầu bằng mã case (QBK_xx).</summary>
[Collection("api")]
public class QuestionTests(ApiFactory f)
{
    private World w => f.World;
    private static string U() => World.Uniq();

    private sealed record Ctx(Scenario S, ApiClient Teacher, ApiClient Colleague);

    private async Task<Ctx> NewCtxAsync()
    {
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        return new Ctx(s, await f.LoginAsync(s.Absent.Username), await f.LoginAsync(s.Sub.Username));
    }

    private static object Options(int correct = 1, int count = 4) => Enumerable.Range(0, count).Select(i => new { label = ((char)('A' + i)).ToString(), content = "Đáp án " + (char)('A' + i), isCorrect = i < correct }).ToArray();

    private static object Body(Scenario s, string? topic = "Chủ đề 1", string? difficulty = "Recognize", int grade = 6, object? options = null, string? content = null) => new
    {
        subjectId = s.SubjectId, gradeLevel = grade, topic, content = content ?? "Nội dung câu hỏi " + U(), explanation = "Giải thích", options = options ?? Options(), difficulty,
    };

    private async Task<int> NewQuestionAsync(Ctx c, string topic = "Chủ đề 1", string difficulty = "Recognize", int grade = 6)
    {
        var r = await c.Teacher.PostAsync("/api/lrn/questions", Body(c.S, topic, difficulty, grade));
        Assert.Equal(200, r.Status);
        return r.Body!["questionId"]!.GetValue<int>();
    }

    [Fact]
    public async Task QBK_01_Create_a_question_manually()
    {
        var c = await NewCtxAsync();
        var mine = await c.Teacher.GetAsync("/api/lrn/questions/my-subjects");
        Assert.Equal(200, mine.Status);
        Assert.Contains(c.S.SubjectId.ToString(), mine.Raw);
        var r = await c.Teacher.PostAsync("/api/lrn/questions", Body(c.S));
        Assert.Equal(200, r.Status);
        Assert.Equal("Ready", r.Body!["status"]!.GetValue<string>());
        Assert.Equal("Recognize", r.Body["difficulty"]!.GetValue<string>());
        var id = r.Body["questionId"]!.GetValue<int>();
        var list = await c.Teacher.GetAsync($"/api/lrn/questions?subjectId={c.S.SubjectId}");
        Assert.Contains($"\"questionId\":{id},", list.Raw);
    }

    [Fact]
    public async Task QBK_02_Option_set_rules()
    {
        var c = await NewCtxAsync();
        async Task<ApiResult> Post(object o) => await c.Teacher.PostAsync("/api/lrn/questions", Body(c.S, options: o));
        Assert.Equal(400, (await Post(Options(1, 3))).Status);
        Assert.Equal(400, (await Post(Options(1, 5))).Status);
        Assert.Equal(400, (await Post(Options(0))).Status);
        Assert.Equal(400, (await Post(Options(4))).Status);
        Assert.Equal(200, (await Post(Options(1))).Status);
        Assert.Equal(200, (await Post(Options(3))).Status);
        Assert.Equal(400, (await Post(new[] { new { label = "A", content = "  ", isCorrect = true }, new { label = "B", content = "b", isCorrect = false }, new { label = "C", content = "c", isCorrect = false }, new { label = "D", content = "d", isCorrect = false } })).Status);
        Assert.Equal(400, (await Post(new[] { new { label = "A", content = new string('a', 501), isCorrect = true }, new { label = "B", content = "b", isCorrect = false }, new { label = "C", content = "c", isCorrect = false }, new { label = "D", content = "d", isCorrect = false } })).Status);
        Assert.Equal(400, (await c.Teacher.PostAsync("/api/lrn/questions", Body(c.S, content: new string('x', 2001)))).Status);
    }

    [Fact]
    public async Task QBK_03_Grade_topic_and_subject_rules()
    {
        var c = await NewCtxAsync();
        foreach (var g in new[] { 5, 10 })
            Assert.Equal("Khối phải từ 6 đến 9. Chọn lại khối.", (await c.Teacher.PostAsync("/api/lrn/questions", Body(c.S, grade: g))).Message);
        Assert.Equal(400, (await c.Teacher.PostAsync("/api/lrn/questions", Body(c.S, topic: ""))).Status);
        var other = await w.NewScenarioAsync();
        var foreign = await c.Teacher.PostAsync("/api/lrn/questions", Body(other));
        Assert.Equal(403, foreign.Status);
        Assert.StartsWith("Chỉ soạn được câu hỏi cho môn thuộc tổ chuyên môn của mình", foreign.Message);
        await f.WithDbAsync(async db => { (await db.Subject.SingleAsync(x => x.SubjectId == c.S.SubjectId)).IsActive = false; await db.SaveChangesAsync(); });
        var inactive = await c.Teacher.PostAsync("/api/lrn/questions", Body(c.S));
        Assert.Equal(400, inactive.Status);
        Assert.StartsWith("Môn học không tồn tại hoặc đã ngừng hoạt động", inactive.Message);
    }

    [Fact]
    public async Task QBK_05_And_06_Only_the_creator_edits_or_hides_and_a_hidden_question_is_not_listed_for_others()
    {
        var c = await NewCtxAsync();
        var id = await NewQuestionAsync(c);
        var edit = await c.Colleague.PutAsync($"/api/lrn/questions/{id}", Body(c.S));
        Assert.Equal(403, edit.Status);
        Assert.StartsWith("Chỉ người tạo câu hỏi mới", edit.Message);
        Assert.Equal(403, (await c.Colleague.PutAsync($"/api/lrn/questions/{id}/hide")).Status);
        Assert.Equal(403, (await c.Colleague.PutAsync($"/api/lrn/questions/{id}/unhide")).Status);
        Assert.Equal(204, (await c.Teacher.PutAsync($"/api/lrn/questions/{id}/hide")).Status);
        var others = await c.Colleague.GetAsync($"/api/lrn/questions?subjectId={c.S.SubjectId}");
        Assert.DoesNotContain($"\"questionId\":{id},", others.Raw);
        Assert.Equal(204, (await c.Teacher.PutAsync($"/api/lrn/questions/{id}/unhide")).Status);
        Assert.Contains($"\"questionId\":{id},", (await c.Colleague.GetAsync($"/api/lrn/questions?subjectId={c.S.SubjectId}")).Raw);
    }

    [Fact]
    public async Task QBK_11_Difficulty_is_required_and_has_four_levels()
    {
        var c = await NewCtxAsync();
        const string msg = "Chọn độ khó: Nhận biết, Thông hiểu, Vận dụng hoặc Vận dụng cao.";
        Assert.Equal(msg, (await c.Teacher.PostAsync("/api/lrn/questions", Body(c.S, difficulty: null))).Message);
        Assert.Equal(msg, (await c.Teacher.PostAsync("/api/lrn/questions", Body(c.S, difficulty: "Easy"))).Message);
        foreach (var d in new[] { "Recognize", "Understand", "Apply", "HighApply" })
            Assert.Equal(200, (await c.Teacher.PostAsync("/api/lrn/questions", Body(c.S, difficulty: d))).Status);
        var id = await NewQuestionAsync(c);
        Assert.Equal(msg, (await c.Teacher.PutAsync($"/api/lrn/questions/{id}", Body(c.S, difficulty: null))).Message);
    }

    [Fact]
    public async Task QBK_12_Classify_an_old_question_that_has_no_difficulty()
    {
        var c = await NewCtxAsync();
        var id = await f.WithDbAsync(async db =>
        {
            var q = new Question
            {
                SubjectId = c.S.SubjectId, GradeLevel = 6, Topic = "Chủ đề cũ", Content = "Câu cũ " + U(), Status = "Ready", CreatedByPersonId = c.S.Absent.PersonId, CreatedAt = f.Clock.Now,
            };
            foreach (var (label, correct) in new[] { ("A", true), ("B", false), ("C", false), ("D", false) })
                q.QuestionOption.Add(new QuestionOption { OptionLabel = label, Content = "Đáp án " + label, IsCorrect = correct });
            db.Question.Add(q);
            await db.SaveChangesAsync();
            return q.QuestionId;
        });
        var ok = await c.Teacher.PutAsync($"/api/lrn/questions/{id}/difficulty", new { difficulty = "Apply" });
        Assert.Equal(200, ok.Status);
        Assert.Equal("Apply", ok.Body!["difficulty"]!.GetValue<string>());
        var again = await c.Teacher.PutAsync($"/api/lrn/questions/{id}/difficulty", new { difficulty = "Recognize" });
        Assert.Equal(400, again.Status);
        Assert.StartsWith("Câu hỏi đã có độ khó. Chỉ đổi được khi sửa câu chưa nằm trong bài tập nào.", again.Message);
        Assert.Equal(403, (await c.Colleague.PutAsync($"/api/lrn/questions/{id}/difficulty", new { difficulty = "Apply" })).Status);
        var id2 = await f.WithDbAsync(async db =>
        {
            var q = new Question { SubjectId = c.S.SubjectId, GradeLevel = 6, Topic = "Chủ đề cũ", Content = "Câu cũ hai " + U(), Status = "Ready", CreatedByPersonId = c.S.Absent.PersonId, CreatedAt = f.Clock.Now };
            foreach (var (label, correct) in new[] { ("A", true), ("B", false), ("C", false), ("D", false) })
                q.QuestionOption.Add(new QuestionOption { OptionLabel = label, Content = "Đáp án " + label, IsCorrect = correct });
            db.Question.Add(q);
            await db.SaveChangesAsync();
            return q.QuestionId;
        });
        Assert.StartsWith("Chọn độ khó:", (await c.Teacher.PutAsync($"/api/lrn/questions/{id2}/difficulty", new { difficulty = "Easy" })).Message);
    }

    [Fact]
    public async Task QBK_13_Confirm_many_drafts_at_once()
    {
        var c = await NewCtxAsync();
        async Task<int> Draft(int owner, string? difficulty) => await f.WithDbAsync(async db =>
        {
            var q = new Question { SubjectId = c.S.SubjectId, GradeLevel = 6, Topic = "Nháp", Content = "Nháp " + U(), Status = "Draft", CreatedByPersonId = owner, CreatedAt = f.Clock.Now, Difficulty = difficulty, IsAiGenerated = true };
            foreach (var (label, correct) in new[] { ("A", true), ("B", false), ("C", false), ("D", false) })
                q.QuestionOption.Add(new QuestionOption { OptionLabel = label, Content = "Đáp án " + label, IsCorrect = correct });
            db.Question.Add(q);
            await db.SaveChangesAsync();
            return q.QuestionId;
        });
        var d1 = await Draft(c.S.Absent.PersonId, "Apply");
        var d2 = await Draft(c.S.Absent.PersonId, null);
        var d3 = await Draft(c.S.Sub.PersonId, "Apply");
        var r = await c.Teacher.PutAsync("/api/lrn/questions/confirm-drafts", new { ids = new[] { d1, d2, d3 } });
        Assert.Equal(200, r.Status);
        Assert.Equal(1, r.Body!["confirmed"]!.GetValue<int>());
        var failed = r.Body["failed"]!.AsArray();
        Assert.Equal(2, failed.Count);
        Assert.Contains(failed, x => x!["questionId"]!.GetValue<int>() == d2 && x["reason"]!.GetValue<string>().StartsWith("Câu nháp chưa có độ khó"));
        var tooMany = await c.Teacher.PutAsync("/api/lrn/questions/confirm-drafts", new { ids = Enumerable.Range(1, 201).ToArray() });
        Assert.Equal(400, tooMany.Status);
        Assert.Equal("Mỗi lần duyệt tối đa 200 câu. Chọn ít câu hơn.", tooMany.Message);
    }

    [Fact]
    public async Task QBK_14_Question_list_can_be_filtered_by_difficulty()
    {
        var c = await NewCtxAsync();
        var apply = await NewQuestionAsync(c, difficulty: "Apply");
        var recognize = await NewQuestionAsync(c, difficulty: "Recognize");
        var onlyApply = await c.Teacher.GetAsync($"/api/lrn/questions?subjectId={c.S.SubjectId}&difficulty=Apply");
        Assert.Contains($"\"questionId\":{apply},", onlyApply.Raw);
        Assert.DoesNotContain($"\"questionId\":{recognize},", onlyApply.Raw);
        var unknown = await c.Teacher.GetAsync($"/api/lrn/questions?subjectId={c.S.SubjectId}&difficulty=abc");
        Assert.Equal(200, unknown.Status);
        Assert.Contains($"\"questionId\":{apply},", unknown.Raw);
        Assert.Contains($"\"questionId\":{recognize},", unknown.Raw);
    }

    [Fact]
    public async Task QBK_15_To_16_Exam_matrix_counts_and_picking_by_cells()
    {
        var c = await NewCtxAsync();
        foreach (var (topic, d, n) in new[] { ("Bài một", "Recognize", 3), ("BÀI MỘT", "Apply", 2), ("Bài hai", "Recognize", 1) })
            for (var i = 0; i < n; i++) await NewQuestionAsync(c, topic, d, 7);
        var hidden = await NewQuestionAsync(c, "Bài một", "Recognize", 7);
        await c.Teacher.PutAsync($"/api/lrn/questions/{hidden}/hide");
        var m = await c.Teacher.GetAsync($"/api/lrn/questions/matrix?subjectId={c.S.SubjectId}&gradeLevel=7");
        Assert.Equal(200, m.Status);
        var rows = m.Body!.AsArray();
        Assert.Equal(2, rows.Count);   // hai chủ đề (không phân biệt hoa thường), câu ẩn không tính
        var one = rows.Single(r => string.Equals(r!["topic"]!.GetValue<string>(), "Bài một", StringComparison.OrdinalIgnoreCase))!;
        Assert.Equal(3, one["recognize"]!.GetValue<int>());
        Assert.Equal(2, one["apply"]!.GetValue<int>());
        foreach (var g in new[] { 5, 10 })
            Assert.Equal("Khối phải từ 6 đến 9. Chọn lại khối.", (await c.Teacher.GetAsync($"/api/lrn/questions/matrix?subjectId={c.S.SubjectId}&gradeLevel={g}")).Message);
        // QBK-16
        var pick = await c.Teacher.PostAsync("/api/lrn/questions/pick", new { subjectId = c.S.SubjectId, gradeLevel = 7, cells = new[] { new { topic = "Bài một", difficulty = "Recognize", count = 2 }, new { topic = "Bài hai", difficulty = "Recognize", count = 1 } } });
        Assert.Equal(200, pick.Status);
        Assert.Equal(3, pick.Body!["questions"]!.AsArray().Count);
        Assert.Empty(pick.Body["shortages"]!.AsArray());
        var short1 = await c.Teacher.PostAsync("/api/lrn/questions/pick", new { subjectId = c.S.SubjectId, gradeLevel = 7, cells = new[] { new { topic = "Bài hai", difficulty = "Recognize", count = 5 } } });
        Assert.Equal(200, short1.Status);
        Assert.Single(short1.Body!["shortages"]!.AsArray());
        Assert.Equal("Điền số câu vào ít nhất một ô của ma trận.", (await c.Teacher.PostAsync("/api/lrn/questions/pick", new { subjectId = c.S.SubjectId, gradeLevel = 7, cells = new[] { new { topic = "Bài hai", difficulty = "Recognize", count = 0 } } })).Message);
        var tooMany = await c.Teacher.PostAsync("/api/lrn/questions/pick", new { subjectId = c.S.SubjectId, gradeLevel = 7, cells = new[] { new { topic = "Bài một", difficulty = "Recognize", count = 51 } } });
        Assert.Equal(400, tooMany.Status);
        Assert.Equal("Một bài tối đa 50 câu hỏi. Giảm số câu trong ma trận.", tooMany.Message);
    }

    [Fact]
    public async Task QBK_08_AI_draft_request_limits()
    {
        var c = await NewCtxAsync();
        var lesson = await f.WithDbAsync(db => db.TextbookLesson.Select(l => l.LessonId).FirstOrDefaultAsync());
        Assert.NotEqual(0, lesson);
        async Task<ApiResult> Gen(object? levels) => await c.Teacher.PostAsync("/api/lrn/questions/ai-drafts", new { subjectId = c.S.SubjectId, gradeLevel = 6, lessonId = lesson, levels });
        Assert.Equal("Số câu mỗi lần sinh từ 1 đến 10. Chỉnh số câu theo mức độ.", (await Gen(Array.Empty<object>())).Message);
        Assert.Equal("Số câu mỗi lần sinh từ 1 đến 10. Chỉnh số câu theo mức độ.", (await Gen(new[] { new { difficulty = "Apply", count = 0 } })).Message);
        Assert.Equal("Số câu mỗi lần sinh từ 1 đến 10. Chỉnh số câu theo mức độ.", (await Gen(new[] { new { difficulty = "Apply", count = 11 } })).Message);
        Assert.Equal("Số câu theo mức độ không hợp lệ. Nhập lại số câu cho từng mức.", (await Gen(new[] { new { difficulty = "Easy", count = 2 } })).Message);
        Assert.Equal("Số câu theo mức độ không hợp lệ. Nhập lại số câu cho từng mức.", (await Gen(new[] { new { difficulty = "Apply", count = -1 } })).Message);
        Assert.Equal("Mỗi mức độ chỉ ghi số câu một lần.", (await Gen(new[] { new { difficulty = "Apply", count = 1 }, new { difficulty = "Apply", count = 2 } })).Message);
    }
}
