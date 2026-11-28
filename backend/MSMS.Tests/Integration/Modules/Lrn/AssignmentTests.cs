using System.Net.Http.Json;
using MSMS.Api.Data.Entities;
using MSMS.Tests.Integration.Infrastructure;
using static MSMS.Tests.Integration.Infrastructure.Roles;

namespace MSMS.Tests.Integration.Modules.Lrn;

/// <summary>LRN-02/03: giao bài, làm bài, chấm điểm theo phần độ khó (BR-14, BR-20, BR-21), bản in đề. Tên test bắt đầu bằng mã case (ASG_xx).</summary>
[Collection("api")]
public partial class AssignmentTests(ApiFactory f)
{
    private World w => f.World;
    private static string U() => World.Uniq();

    private sealed record Ctx(Scenario S, int ClassId, ApiClient Teacher, ApiClient Colleague, List<Who> Students);

    /// <summary>Giáo viên "Vắng" phụ trách môn của tổ ở lớp A theo thời khóa biểu đã phát hành (dựng thẳng trong DB), lớp có 2 học sinh.</summary>
    private async Task<Ctx> NewCtxAsync(int grade = 6)
    {
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "AS" + U(), (byte)grade);
        var students = new List<Who>();
        for (var i = 0; i < 2; i++) students.Add((await w.NewStudentAsync(cls, $"HS giao bài {i} {U()}")).Who);
        await PublishFor(cls, s.SubjectId, s.Absent.PersonId);
        return new Ctx(s, cls, await f.LoginAsync(s.Absent.Username), await f.LoginAsync(s.Sub.Username), students);
    }

    private static int publishTick;

    private async Task PublishFor(int classId, int subjectId, int teacherId)
    {
        await f.WithDbAsync(async db =>
        {
            var sem = await db.Semester.Where(x => x.AcademicYear.IsCurrent && x.SemesterNo == 1).Select(x => x.SemesterId).SingleAsync();
            var slot = await db.PeriodSlot.Where(p => p.SessionOfDay == "Morning" && p.PeriodNo == 1).Select(p => p.PeriodSlotId).SingleAsync();
            var v = new TimetableVersion { CampusId = w.CampusAId, SemesterId = sem, Status = "Published", ImportedAt = f.Clock.Now, PublishedAt = f.Clock.Now.AddSeconds(-1000 + Interlocked.Increment(ref publishTick)) };
            db.TimetableVersion.Add(v);
            await db.SaveChangesAsync();
            db.TimetableVersionClass.Add(new TimetableVersionClass { VersionId = v.VersionId, ClassId = classId });
            db.TimetableEntry.Add(new TimetableEntry { VersionId = v.VersionId, ClassId = classId, DayOfWeek = 2, PeriodSlotId = slot, SubjectId = subjectId, TeacherId = teacherId });
            await db.SaveChangesAsync();
        });
    }

    private static object Opts(int correct = 1) => Enumerable.Range(0, 4).Select(i => new { label = ((char)('A' + i)).ToString(), content = "Đáp án " + (char)('A' + i), isCorrect = i < correct }).ToArray();

    private async Task<int> QuestionAsync(Ctx c, string difficulty = "Recognize", int grade = 6, int correct = 1)
    {
        var r = await c.Teacher.PostAsync("/api/lrn/questions", new { subjectId = c.S.SubjectId, gradeLevel = grade, topic = "Chủ đề", content = "Câu hỏi " + U(), explanation = "x", options = Opts(correct), difficulty });
        Assert.Equal(200, r.Status);
        return r.Body!["questionId"]!.GetValue<int>();
    }

    private static string Iso(DateTime d) => d.ToString("yyyy-MM-ddTHH:mm:ss");

    private object Create(Ctx c, IEnumerable<int> questions, object? sections = null, DateTime? openAt = null, DateTime? dueAt = null, int? duration = null, int? classId = null, string title = "Bài tập kiểm thử") => new
    {
        subjectId = c.S.SubjectId, classId = classId ?? c.ClassId, title, instruction = "Làm cẩn thận",
        openAt = Iso(openAt ?? f.Clock.Now.AddMinutes(-5)), dueAt = Iso(dueAt ?? f.Clock.Now.AddDays(2)), durationMinutes = duration,
        items = questions.Select(q => new { questionId = q }).ToArray(), sections,
    };

    private static object Sec(params (string Difficulty, decimal Points)[] s) => s.Select(x => new { difficulty = x.Difficulty, points = x.Points }).ToArray();

    // ------------------------------------------------------------------ Giao bài

    [Fact]
    public async Task ASG_01_And_23_Create_an_assignment_with_section_points_split_fairly()
    {
        var c = await NewCtxAsync();
        var cs = await c.Teacher.GetAsync("/api/lrn/assignments/class-subjects");
        Assert.Equal(200, cs.Status);
        Assert.Contains($"\"classId\":{c.ClassId},", cs.Raw);
        var rec = new List<int>(); var app = new List<int>();
        for (var i = 0; i < 3; i++) rec.Add(await QuestionAsync(c, "Recognize"));
        for (var i = 0; i < 2; i++) app.Add(await QuestionAsync(c, "Apply"));
        var r = await c.Teacher.PostAsync("/api/lrn/assignments", Create(c, rec.Concat(app), Sec(("Recognize", 3.34m), ("Apply", 6.66m))));
        Assert.Equal(200, r.Status);
        Assert.Equal(10m, r.Body!["totalPoints"]!.GetValue<decimal>());
        var qs = r.Body["questions"]!.AsArray();
        var recPoints = qs.Where(q => q!["difficulty"]!.GetValue<string>() == "Recognize").Select(q => q!["points"]!.GetValue<decimal>()).OrderByDescending(x => x).ToList();
        Assert.Equal(new[] { 1.12m, 1.11m, 1.11m }, recPoints);
        Assert.All(qs.Where(q => q!["difficulty"]!.GetValue<string>() == "Apply"), q => Assert.Equal(3.33m, q!["points"]!.GetValue<decimal>()));
        Assert.Equal(2, r.Body["assignment"]!["recipientCount"]!.GetValue<int>());
        // thông báo "Bài tập mới" cho học sinh
        foreach (var st in c.Students)
            Assert.True(await f.WithDbAsync(db => db.Notification.CountAsync(n => n.RecipientPersonId == st.PersonId && n.NotificationType == "AssignmentAssigned")) >= 1);
        var sections = r.Body["sections"]!.AsArray();
        Assert.Equal("Recognize", sections[0]!["difficulty"]!.GetValue<string>());   // từ dễ đến khó
    }

    [Fact]
    public async Task ASG_02_And_03_Only_the_responsible_teacher_may_assign_and_responsibility_follows_the_newest_timetable()
    {
        var c = await NewCtxAsync();
        var q = await QuestionAsync(c);
        var denied = await c.Colleague.PostAsync("/api/lrn/assignments", Create(c, [q], Sec(("Recognize", 10m))));
        Assert.Equal(403, denied.Status);
        Assert.StartsWith("Chỉ giáo viên đang phụ trách chuyên môn", denied.Message);
        // thời khóa biểu mới giao lớp cho giáo viên khác
        await PublishFor(c.ClassId, c.S.SubjectId, c.S.Sub.PersonId);
        var q2 = await QuestionAsync(new Ctx(c.S, c.ClassId, c.Colleague, c.Teacher, c.Students));
        Assert.Equal(200, (await c.Colleague.PostAsync("/api/lrn/assignments", Create(c, [q2], Sec(("Recognize", 10m))))).Status);
        Assert.Equal(403, (await c.Teacher.PostAsync("/api/lrn/assignments", Create(c, [q], Sec(("Recognize", 10m))))).Status);
    }

    [Fact]
    public async Task ASG_04_Question_selection_rules()
    {
        var c = await NewCtxAsync();
        var ok = await QuestionAsync(c);
        var other = await w.NewScenarioAsync();
        var foreign = (await (await f.LoginAsync(other.Absent.Username)).PostAsync("/api/lrn/questions", new { subjectId = other.SubjectId, gradeLevel = 6, topic = "x", content = "Khác môn " + U(), options = Opts(), difficulty = "Apply" })).Body!["questionId"]!.GetValue<int>();
        var hidden = await QuestionAsync(c);
        await c.Teacher.PutAsync($"/api/lrn/questions/{hidden}/hide");
        async Task<ApiResult> Post(IEnumerable<int> items) => await c.Teacher.PostAsync("/api/lrn/assignments", Create(c, items, Sec(("Recognize", 10m))));
        Assert.StartsWith("Bài tập môn", (await Post([ok, foreign])).Message);
        Assert.StartsWith("Có câu hỏi chưa Sẵn dùng hoặc đã bị ẩn", (await Post([ok, hidden])).Message);
        Assert.StartsWith("Có câu hỏi không còn tồn tại", (await Post([ok, 987654321])).Message);
        Assert.Equal("Chọn ít nhất một câu hỏi cho bài tập.", (await Post([])).Message);
        Assert.StartsWith("Một bài tối đa 50 câu hỏi", (await Post(Enumerable.Range(1, 51).ToList())).Message);
        Assert.StartsWith("Mỗi câu hỏi chỉ xuất hiện một lần trong bài", (await Post([ok, ok])).Message);
    }

    [Fact]
    public async Task ASG_05_Time_rules()
    {
        var c = await NewCtxAsync();
        var q = await QuestionAsync(c);
        var sec = Sec(("Recognize", 10m));
        Assert.Equal("Chọn hạn nộp bài.", (await c.Teacher.PostAsync("/api/lrn/assignments", new { subjectId = c.S.SubjectId, classId = c.ClassId, title = "t", items = new[] { new { questionId = q } }, sections = sec })).Message);
        Assert.StartsWith("Hạn nộp phải lớn hơn thời điểm hiện tại", (await c.Teacher.PostAsync("/api/lrn/assignments", Create(c, [q], sec, dueAt: f.Clock.Now.AddMinutes(-1)))).Message);
        Assert.StartsWith("Hạn nộp phải sau thời điểm mở bài", (await c.Teacher.PostAsync("/api/lrn/assignments", Create(c, [q], sec, openAt: f.Clock.Now.AddDays(3), dueAt: f.Clock.Now.AddDays(2)))).Message);
        foreach (var d in new[] { 0, 601 })
            Assert.StartsWith("Thời lượng làm bài từ 1 đến 600 phút", (await c.Teacher.PostAsync("/api/lrn/assignments", Create(c, [q], sec, duration: d))).Message);
        Assert.Equal(200, (await c.Teacher.PostAsync("/api/lrn/assignments", Create(c, [q], sec, duration: 600))).Status);
        Assert.Equal(200, (await c.Teacher.PostAsync("/api/lrn/assignments", Create(c, [q], sec, duration: null))).Status);
    }

    [Fact]
    public async Task ASG_06_A_class_without_students_cannot_receive_an_assignment()
    {
        var c = await NewCtxAsync();
        var empty = await w.NewClassAsync(w.CampusAId, "AE" + U(), 6);
        await PublishFor(empty, c.S.SubjectId, c.S.Absent.PersonId);
        var q = await QuestionAsync(c);
        var r = await c.Teacher.PostAsync("/api/lrn/assignments", Create(c, [q], Sec(("Recognize", 10m)), classId: empty));
        Assert.Equal(400, r.Status);
        Assert.Equal("Lớp chưa có học sinh đang học. Xếp học sinh vào lớp trước khi giao bài.", r.Message);
    }

    [Fact]
    public async Task ASG_22_Section_points_must_be_complete_and_add_up_to_10()
    {
        var c = await NewCtxAsync();
        var a = await QuestionAsync(c, "Recognize"); var b = await QuestionAsync(c, "Apply");
        async Task<ApiResult> Post(object sections) => await c.Teacher.PostAsync("/api/lrn/assignments", Create(c, [a, b], sections));
        Assert.StartsWith("Nhập điểm cho phần", (await Post(Sec(("Recognize", 10m)))).Message);
        Assert.StartsWith("Tổng điểm các phần phải bằng 10, hiện là 9,5", (await Post(Sec(("Recognize", 4.5m), ("Apply", 5m)))).Message.Replace('.', ','));
        Assert.StartsWith("Tổng điểm các phần phải bằng 10", (await Post(Sec(("Recognize", 5.5m), ("Apply", 5m)))).Message);
        Assert.StartsWith("Điểm phần", (await Post(Sec(("Recognize", 0m), ("Apply", 10m)))).Message);
        Assert.StartsWith("Điểm phần", (await Post(Sec(("Recognize", 2.555m), ("Apply", 7.445m)))).Message);
        Assert.Equal(200, (await Post(Sec(("Recognize", 5m), ("Apply", 5m)))).Status);
    }

    [Fact]
    public async Task ASG_28_An_assignment_contains_only_questions_of_the_grade_of_the_class()
    {
        var c = await NewCtxAsync();
        var q6 = await QuestionAsync(c, grade: 6);
        var q7 = await QuestionAsync(c, grade: 7);
        var q0 = await f.WithDbAsync(async db =>
        {
            var q = new Question { SubjectId = c.S.SubjectId, GradeLevel = null, Topic = "Cũ", Content = "Câu chưa ghi khối " + U(), Status = "Ready", CreatedByPersonId = c.S.Absent.PersonId, CreatedAt = f.Clock.Now, Difficulty = "Recognize" };
            foreach (var (label, correct) in new[] { ("A", true), ("B", false), ("C", false), ("D", false) }) q.QuestionOption.Add(new QuestionOption { OptionLabel = label, Content = "Đáp án " + label, IsCorrect = correct });
            db.Question.Add(q);
            await db.SaveChangesAsync();
            return q.QuestionId;
        });
        var sec = Sec(("Recognize", 10m));
        Assert.Equal(200, (await c.Teacher.PostAsync("/api/lrn/assignments", Create(c, [q6], sec))).Status);
        var bad = await c.Teacher.PostAsync("/api/lrn/assignments", Create(c, [q7], sec));
        Assert.Equal(400, bad.Status);
        Assert.Contains("học khối 6 nên chỉ giao được câu hỏi khối 6. Bỏ các câu thuộc khối khác.", bad.Message);
        Assert.Equal(200, (await c.Teacher.PostAsync("/api/lrn/assignments", Create(c, [q6, q0], sec))).Status);
        Assert.Equal(200, (await c.Teacher.PostAsync("/api/lrn/assignments", Create(c, [q0], sec))).Status);
    }

    // ------------------------------------------------------------------ Học sinh làm bài

    private async Task<(Ctx C, int AssignmentId, List<JsonNode> Questions)> AssignedAsync(int questionCount = 3, int duration = 0, DateTime? openAt = null)
    {
        var c = await NewCtxAsync();
        var ids = new List<int>();
        for (var i = 0; i < questionCount; i++) ids.Add(await QuestionAsync(c, "Recognize", correct: i == 0 ? 2 : 1));   // câu 1 có hai đáp án đúng
        var r = await c.Teacher.PostAsync("/api/lrn/assignments", Create(c, ids, Sec(("Recognize", 10m)), openAt: openAt, duration: duration == 0 ? null : duration));
        Assert.Equal(200, r.Status);
        return (c, r.Body!["assignment"]!["assignmentId"]!.GetValue<int>(), r.Body["questions"]!.AsArray().Select(x => x!).ToList());
    }

    private static object Answer(JsonNode q, params int[] optionIndexes) => new { assignmentQuestionId = q["assignmentQuestionId"]!.GetValue<int>(), optionIds = optionIndexes.Select(i => q["options"]![i]!["optionId"]!.GetValue<int>()).ToArray() };

    [Fact]
    public async Task ASG_07_And_10_And_11_And_12_Student_opens_saves_a_draft_submits_and_is_scored_by_exact_option_sets()
    {
        var (c, id, qs) = await AssignedAsync();
        var st = await f.LoginAsync(c.Students[0].Username);
        var list = await st.GetAsync("/api/lrn/my-assignments");
        Assert.Equal(200, list.Status);
        Assert.Contains($"\"assignmentId\":{id},", list.Raw);
        var attempt = await st.GetAsync($"/api/lrn/my-assignments/{id}/attempt");
        Assert.Equal(200, attempt.Status);
        Assert.DoesNotContain("isCorrect", attempt.Raw);   // đáp án đúng không gửi cho học sinh
        var attemptQs = attempt.Body!["questions"]!.AsArray();
        // dùng id option từ bản giáo viên (có isCorrect) để biết đáp án; đáp án đúng là A (và B ở câu 1)
        var q1 = qs[0]; var q2 = qs[1]; var q3 = qs[2];
        // ASG-10: lưu nháp
        var draft = await st.PutAsync($"/api/lrn/my-assignments/{id}/answers", new { answers = new[] { Answer(q1, 0, 1) } });
        Assert.Equal(200, draft.Status);
        Assert.Contains("optionIds", (await st.GetAsync($"/api/lrn/my-assignments/{id}/attempt")).Raw);
        // ASG-11: A+B đúng, câu 2 đúng, câu 3 chọn sai -> 2/3 x 10 điểm = 6.67 (3.34 + 3.33)
        var submit = await st.PostAsync($"/api/lrn/my-assignments/{id}/submit", new { answers = new[] { Answer(q1, 0, 1), Answer(q2, 0), Answer(q3, 1) } });
        Assert.Equal(200, submit.Status);
        var result = await st.GetAsync($"/api/lrn/results/{id}");
        Assert.Equal(200, result.Status);
        var score = result.Body!["score"]?.GetValue<decimal>() ?? result.Body["totalScore"]?.GetValue<decimal>() ?? -1;
        Assert.Equal(6.67m, score);
        Assert.Equal(2, attemptQs.Count - 1);
        // ASG-12: nộp lần hai và lưu nháp sau khi nộp
        var again = await st.PostAsync($"/api/lrn/my-assignments/{id}/submit", new { answers = new[] { Answer(q1, 0, 1) } });
        Assert.Equal(400, again.Status);
        Assert.Equal("Đã nộp bài này, mỗi bài chỉ nộp một lần.", again.Message);
        var late = await st.PutAsync($"/api/lrn/my-assignments/{id}/answers", new { answers = new[] { Answer(q1, 0) } });
        Assert.Equal(400, late.Status);   // 409 chỉ khi hai yêu cầu tranh nhau ở cùng thời điểm
        Assert.Equal("Đã nộp bài này, mỗi bài chỉ nộp một lần.", late.Message);
    }

    [Fact]
    public async Task ASG_13_Answer_payload_validation()
    {
        var (c, id, qs) = await AssignedAsync();
        var st = await f.LoginAsync(c.Students[0].Username);
        var q2 = qs[1];
        Assert.StartsWith("Có câu trả lời không thuộc bài", (await st.PutAsync($"/api/lrn/my-assignments/{id}/answers", new { answers = new[] { new { assignmentQuestionId = 987654321, optionIds = new[] { 1 } } } })).Message);
        Assert.StartsWith("Mỗi câu chỉ gửi một lần", (await st.PutAsync($"/api/lrn/my-assignments/{id}/answers", new { answers = new[] { Answer(q2, 0), Answer(q2, 1) } })).Message);
        Assert.StartsWith("Có lựa chọn không thuộc câu hỏi", (await st.PutAsync($"/api/lrn/my-assignments/{id}/answers", new { answers = new[] { new { assignmentQuestionId = q2["assignmentQuestionId"]!.GetValue<int>(), optionIds = new[] { 987654321 } } } })).Message);
        Assert.Contains("chỉ chọn một đáp án.", (await st.PutAsync($"/api/lrn/my-assignments/{id}/answers", new { answers = new[] { Answer(q2, 0, 1) } })).Message);
    }

    [Fact]
    public async Task ASG_08_And_09_And_15_Not_open_timed_and_outside_the_roster()
    {
        var (c, id, _) = await AssignedAsync(openAt: DateTime.UtcNow.AddDays(1));
        var st = await f.LoginAsync(c.Students[0].Username);
        var notOpen = await st.PostAsync($"/api/lrn/my-assignments/{id}/start");
        Assert.Equal(400, notOpen.Status);
        Assert.StartsWith("Bài chưa mở. Mở lúc", notOpen.Message);
        // ASG-15: học sinh khác lớp
        var stranger = await w.NewAccountAsync("HS ngoài danh sách " + U(), "Student", "Student", "Active");
        var r = await (await f.LoginAsync(stranger.Username)).GetAsync($"/api/lrn/my-assignments/{id}/attempt");
        Assert.Equal(404, r.Status);
        Assert.Equal("Không tìm thấy bài tập trong danh sách được giao của tài khoản này.", r.Message);
        // ASG-09: bài có thời lượng phải bấm Bắt đầu
        var (c2, id2, qs2) = await AssignedAsync(duration: 30);
        var st2 = await f.LoginAsync(c2.Students[0].Username);
        var before = await st2.PostAsync($"/api/lrn/my-assignments/{id2}/submit", new { answers = new[] { Answer(qs2[0], 0, 1) } });
        Assert.Equal(400, before.Status);
        Assert.Equal("Bài có giới hạn thời gian. Bấm Bắt đầu rồi mới làm bài.", before.Message);
        var started = await st2.PostAsync($"/api/lrn/my-assignments/{id2}/start");
        Assert.Equal(200, started.Status);
        var first = started.Body!["startedAt"]!.GetValue<DateTime>();
        var second = await st2.PostAsync($"/api/lrn/my-assignments/{id2}/start");
        Assert.Equal(first, second.Body!["startedAt"]!.GetValue<DateTime>());
    }

    [Fact]
    public async Task ASG_17_And_18_Result_visible_only_after_submitting_and_parents_see_linked_children_only()
    {
        var (c, id, qs) = await AssignedAsync();
        var st = await f.LoginAsync(c.Students[0].Username);
        var before = await st.GetAsync($"/api/lrn/results/{id}");
        Assert.Equal(400, before.Status);
        Assert.Equal("Bài này chưa nộp nên chưa có kết quả.", before.Message);
        await st.PostAsync($"/api/lrn/my-assignments/{id}/submit", new { answers = new[] { Answer(qs[0], 0, 1) } });
        Assert.Equal(200, (await st.GetAsync($"/api/lrn/results/{id}")).Status);
        // ASG-18
        var parent = await w.NewAccountAsync("PH giao bài " + U(), "Parent", "Parent", "Active");
        await f.WithDbAsync(async db => { db.ParentStudentLink.Add(new ParentStudentLink { ParentId = parent.PersonId, StudentId = c.Students[0].PersonId, Relationship = "Phụ huynh", IsPrimary = true, CreatedAt = f.Clock.Now }); await db.SaveChangesAsync(); });
        var pc = await f.LoginAsync(parent.Username);
        Assert.Equal(200, (await pc.GetAsync($"/api/lrn/results/{id}?studentId={c.Students[0].PersonId}")).Status);
        Assert.Equal("Chọn học sinh cần xem.", (await pc.GetAsync($"/api/lrn/results/{id}")).Message);
        var stranger = await w.NewAccountAsync("PH lạ giao bài " + U(), "Parent", "Parent", "Active");
        var denied = await (await f.LoginAsync(stranger.Username)).GetAsync($"/api/lrn/results/{id}?studentId={c.Students[0].PersonId}");
        Assert.Equal(403, denied.Status);
        Assert.Equal("Chỉ xem được kết quả của con đã liên kết với tài khoản.", denied.Message);
    }

    [Fact]
    public async Task ASG_16_Only_the_responsible_teacher_views_class_results()
    {
        var (c, id, qs) = await AssignedAsync();
        var st = await f.LoginAsync(c.Students[0].Username);
        await st.PostAsync($"/api/lrn/my-assignments/{id}/submit", new { answers = new[] { Answer(qs[0], 0, 1) } });
        Assert.Equal(200, (await c.Teacher.GetAsync($"/api/lrn/assignments/{id}/results")).Status);
        Assert.Equal(200, (await c.Teacher.GetAsync($"/api/lrn/assignments/{id}/results/{c.Students[0].PersonId}")).Status);
        var stranger = await w.NewAccountAsync("HS ngoài kết quả " + U(), "Student", "Student", "Active");
        var notIn = await c.Teacher.GetAsync($"/api/lrn/assignments/{id}/results/{stranger.PersonId}");
        Assert.Equal(404, notIn.Status);
        Assert.Equal("Học sinh không thuộc danh sách được giao của bài này.", notIn.Message);
        var head = await f.LoginAsync(c.S.Head.Username);   // tổ trưởng nhưng không phụ trách lớp này
        Assert.True((await head.GetAsync($"/api/lrn/assignments/{id}/results/{c.Students[0].PersonId}")).Status is 403 or 404);
    }

    [Fact]
    public async Task ASG_21_Role_matrix()
    {
        var roles = await f.LoginAllAsync();
        foreach (var role in new[] { Student, Parent, Admin, Vice })
            Assert.Equal(403, (await roles[role].GetAsync("/api/lrn/assignments/class-subjects")).Status);
        foreach (var role in new[] { Teacher, Parent })
            Assert.Equal(403, (await roles[role].GetAsync("/api/lrn/my-assignments")).Status);
    }

    // ------------------------------------------------------------------ Bản in đề

    [Fact]
    public async Task ASG_26_And_29_Export_a_paper_with_validations_and_one_grade_only()
    {
        var c = await NewCtxAsync();
        var q6 = await QuestionAsync(c, "Recognize", 6);
        var q6b = await QuestionAsync(c, "Apply", 6);
        var q7 = await QuestionAsync(c, "Recognize", 7);
        var url = "/api/lrn/papers/export";
        object Body(IEnumerable<int> items, object? sections = null, int? duration = null) => new { subjectId = c.S.SubjectId, title = "Đề kiểm tra", durationMinutes = duration, items = items.Select(q => new { questionId = q }).ToArray(), sections, format = "pdf", withAnswers = false };
        var ok = await c.Teacher.Http.PostAsync(url, JsonContent.Create(Body([q6, q6b], Sec(("Recognize", 5m), ("Apply", 5m)))));
        Assert.Equal(200, (int)ok.StatusCode);
        Assert.Equal("application/pdf", ok.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Chọn ít nhất một câu hỏi cho đề.", (await c.Teacher.PostAsync(url, Body([]))).Message);
        Assert.StartsWith("Một đề tối đa 50 câu hỏi", (await c.Teacher.PostAsync(url, Body(Enumerable.Range(1, 51).ToList()))).Message);
        Assert.StartsWith("Mỗi câu hỏi chỉ xuất hiện một lần trong đề", (await c.Teacher.PostAsync(url, Body([q6, q6]))).Message);
        Assert.StartsWith("Thời lượng làm bài từ 1 đến 600 phút", (await c.Teacher.PostAsync(url, Body([q6], Sec(("Recognize", 10m)), 0))).Message);
        // ASG-29
        var mixed = await c.Teacher.PostAsync(url, Body([q6, q7], Sec(("Recognize", 10m))));
        Assert.Equal(400, mixed.Status);
        Assert.Equal("Đề chỉ gồm câu hỏi cùng một khối. Bỏ các câu thuộc khối khác.", mixed.Message);
        var roles = await f.LoginAllAsync();
        foreach (var role in new[] { Student, Parent, Admin, Vice })
            Assert.Equal(403, (await roles[role].PostAsync(url, Body([q6]))).Status);
    }
}
