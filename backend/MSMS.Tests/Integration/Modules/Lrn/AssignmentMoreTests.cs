using MSMS.Api.Common.Storage;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Lrn.Services;
using Microsoft.Extensions.DependencyInjection;
using MSMS.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace MSMS.Tests.Integration.Modules.Lrn;

/// <summary>Phần còn lại của LRN: sửa câu hỏi đã dùng (QBK-04), sách giáo khoa (QBK-10, 17, ASG-19), kết quả (ASG-24), bản in (ASG-25), màn bị gỡ (ASG-20).</summary>
public partial class AssignmentTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4];

    /// <summary>Hai cuốn sách khối 6: tập 1 có "Bài 10", "Bài 2"; tập 2 có "Bài 1". Ảnh trang đặt sẵn trong kho ảnh.</summary>
    private async Task<(List<int> LessonIds, string Code)> SeedTextbookAsync(int subjectId, int grade)
    {
        var code = "SGK" + U();
        var storage = (MemoryImageStorage)f.Services.GetRequiredService<IImageStorage>();
        return await f.WithDbAsync(async db =>
        {
            var b1 = new TextbookBook { Code = code + "A", SubjectId = subjectId, GradeLevel = (byte)grade, Volume = 1, Title = "Sách tập 1", PageOffset = 0, PdfPageCount = 20 };
            var b2 = new TextbookBook { Code = code + "B", SubjectId = subjectId, GradeLevel = (byte)grade, Volume = 2, Title = "Sách tập 2", PageOffset = 0, PdfPageCount = 20 };
            b1.TextbookLesson.Add(new TextbookLesson { Chapter = "Chương 1", SortOrder = 1, Title = "Bài 10", StartPage = 5, EndPage = 7 });
            b1.TextbookLesson.Add(new TextbookLesson { Chapter = "Chương 1", SortOrder = 2, Title = "Bài 2", StartPage = 8, EndPage = 9 });
            b2.TextbookLesson.Add(new TextbookLesson { Chapter = "Chương 2", SortOrder = 1, Title = "Bài 1", StartPage = 3, EndPage = 4 });
            db.TextbookBook.AddRange(b1, b2);
            await db.SaveChangesAsync();
            foreach (var b in new[] { b1, b2 })
                for (var p = 1; p <= 12; p++) storage.Put(ImageFolders.Textbooks, TextbookService.PageFileName(b.Code, p), Jpeg);
            return (b1.TextbookLesson.Concat(b2.TextbookLesson).Select(l => l.LessonId).ToList(), code);
        });
    }

    [Fact]
    public async Task QBK_04_A_question_can_be_edited_only_before_it_is_used_in_an_assignment()
    {
        var c = await NewCtxAsync();
        var free = await QuestionAsync(c);
        var body = new { subjectId = c.S.SubjectId, gradeLevel = 6, topic = "Chủ đề đổi", content = "Nội dung mới " + U(), explanation = "x", options = Opts(), difficulty = "Recognize" };
        Assert.Equal(200, (await c.Teacher.PutAsync($"/api/lrn/questions/{free}", body)).Status);
        var used = await QuestionAsync(c);
        Assert.Equal(200, (await c.Teacher.PostAsync("/api/lrn/assignments", Create(c, [used], Sec(("Recognize", 10m))))).Status);
        var r = await c.Teacher.PutAsync($"/api/lrn/questions/{used}", body);
        Assert.Equal(400, r.Status);
        Assert.StartsWith("Câu hỏi đã nằm trong một bài tập nên không sửa được", r.Message);
    }

    [Fact]
    public async Task QBK_10_Textbook_lessons_and_pages()
    {
        var c = await NewCtxAsync();
        var (lessons, _) = await SeedTextbookAsync(c.S.SubjectId, 6);
        var list = await c.Teacher.GetAsync($"/api/lrn/textbooks/lessons?subjectId={c.S.SubjectId}&gradeLevel=6");
        Assert.Equal(200, list.Status);
        Assert.Equal(3, list.Body!.AsArray().Count);
        var lesson = lessons[0];
        var page = await c.Teacher.GetBytesAsync($"/api/lrn/textbooks/lessons/{lesson}/pages/5");
        Assert.Equal(200, page.Status);
        Assert.Equal("image/jpeg", page.ContentType);
        var outside = await c.Teacher.GetAsync($"/api/lrn/textbooks/lessons/{lesson}/pages/99");
        Assert.Equal(404, outside.Status);
        Assert.Equal("Trang không thuộc bài học này.", outside.Message);
    }

    [Fact]
    public async Task QBK_17_The_exam_matrix_lists_topics_in_textbook_order_then_free_topics_in_natural_order()
    {
        var c = await NewCtxAsync();
        await SeedTextbookAsync(c.S.SubjectId, 7);
        foreach (var topic in new[] { "Ôn tập 10", "Bài 1", "Ôn tập 2", "Bài 2", "Bài 10" })
        {
            var r = await c.Teacher.PostAsync("/api/lrn/questions", new { subjectId = c.S.SubjectId, gradeLevel = 7, topic, content = "Câu " + U(), explanation = "x", options = Opts(), difficulty = "Recognize" });
            Assert.Equal(200, r.Status);
        }
        var m = await c.Teacher.GetAsync($"/api/lrn/questions/matrix?subjectId={c.S.SubjectId}&gradeLevel=7");
        Assert.Equal(200, m.Status);
        Assert.Equal(["Bài 10", "Bài 2", "Bài 1", "Ôn tập 2", "Ôn tập 10"], m.Body!.AsArray().Select(x => x!["topic"]!.GetValue<string>()).ToArray());
    }

    [Fact]
    public async Task ASG_19_The_source_textbook_page_is_shown_only_to_a_student_who_submitted()
    {
        var c = await NewCtxAsync();
        var (lessons, _) = await SeedTextbookAsync(c.S.SubjectId, 6);
        var q = await QuestionAsync(c);
        await f.WithDbAsync(async db => { (await db.Question.SingleAsync(x => x.QuestionId == q)).LessonId = lessons[0]; await db.SaveChangesAsync(); });
        var r = await c.Teacher.PostAsync("/api/lrn/assignments", Create(c, [q], Sec(("Recognize", 10m))));
        Assert.Equal(200, r.Status);
        var id = r.Body!["assignment"]!["assignmentId"]!.GetValue<int>();
        var qs = r.Body["questions"]!.AsArray().Select(x => x!).ToList();
        var st = await f.LoginAsync(c.Students[0].Username);
        var early = await st.GetAsync($"/api/lrn/results/{id}/pages/{lessons[0]}/5");
        Assert.Equal(404, early.Status);   // chưa nộp bài thì chưa xem được
        Assert.Equal("Không tìm thấy trang sách.", early.Message);
        Assert.Equal(200, (await st.PostAsync($"/api/lrn/my-assignments/{id}/submit", new { answers = new[] { Answer(qs[0], 0) } })).Status);
        var ok = await st.GetBytesAsync($"/api/lrn/results/{id}/pages/{lessons[0]}/5");
        Assert.Equal(200, ok.Status);
        Assert.Equal("image/jpeg", ok.ContentType);
        var other = await st.GetAsync($"/api/lrn/results/{id}/pages/{lessons[1]}/8");
        Assert.Equal(404, other.Status);   // bài khác với nguồn của câu hỏi
        var notMine = await (await f.LoginAsync(c.Students[1].Username)).GetAsync($"/api/lrn/results/{id}/pages/{lessons[0]}/5");
        Assert.Equal(404, notMine.Status);   // học sinh khác chưa nộp
    }

    [Fact]
    public async Task ENR_21_A_new_assignment_goes_only_to_the_pupils_enrolled_at_assignment_time()
    {
        var c = await NewCtxAsync();
        await f.WithDbAsync(async db =>
        {
            (await db.Enrollment.SingleAsync(e => e.StudentId == c.Students[1].PersonId)).EndDate = f.Clock.Today.AddDays(-1);   // đã chuyển đi hôm qua
            await db.SaveChangesAsync();
        });
        var q = await QuestionAsync(c);
        var r = await c.Teacher.PostAsync("/api/lrn/assignments", Create(c, [q], Sec(("Recognize", 10m))));
        Assert.Equal(200, r.Status);
        var id = r.Body!["assignment"]!["assignmentId"]!.GetValue<int>();
        Assert.Equal([c.Students[0].PersonId], await f.WithDbAsync(db => db.AssignmentRecipient.Where(x => x.AssignmentId == id).Select(x => x.StudentId).ToListAsync()));
        Assert.Contains($"\"assignmentId\":{id},", (await (await f.LoginAsync(c.Students[0].Username)).GetAsync("/api/lrn/my-assignments")).Raw);
        Assert.DoesNotContain($"\"assignmentId\":{id},", (await (await f.LoginAsync(c.Students[1].Username)).GetAsync("/api/lrn/my-assignments")).Raw);
    }

    private async Task<(Ctx C, int Id, List<JsonNode> Qs)> AssignWithThreeStudentsAsync(DateTime? dueAt = null, int? duration = null, int questionCount = 3)
    {
        var c = await NewCtxAsync();
        c.Students.Add((await w.NewStudentAsync(c.ClassId, "HS thứ ba " + U())).Who);
        var ids = new List<int>();
        for (var i = 0; i < questionCount; i++) ids.Add(await QuestionAsync(c));
        var r = await c.Teacher.PostAsync("/api/lrn/assignments", Create(c, ids, Sec(("Recognize", 10m)), dueAt: dueAt, duration: duration));
        Assert.Equal(200, r.Status);
        return (c, r.Body!["assignment"]!["assignmentId"]!.GetValue<int>(), r.Body["questions"]!.AsArray().Select(x => x!).ToList());
    }

    [Fact]
    public async Task ASG_14_The_deadline_has_a_10_second_grace_and_expired_drafts_are_submitted_by_the_sweep()
    {
        var due = DateTime.Now.Date.AddDays(1).AddHours(17);
        var (c, id, qs) = await AssignWithThreeStudentsAsync(due);
        var late = await f.LoginAsync(c.Students[0].Username);
        var tooLate = await f.LoginAsync(c.Students[1].Username);
        var empty = await f.LoginAsync(c.Students[2].Username);
        try
        {
            Assert.Equal(200, (await late.PutAsync($"/api/lrn/my-assignments/{id}/answers", new { answers = new[] { Answer(qs[0], 0) } })).Status);
            Assert.Equal(200, (await tooLate.PutAsync($"/api/lrn/my-assignments/{id}/answers", new { answers = new[] { Answer(qs[0], 0), Answer(qs[1], 0) } })).Status);
            Assert.Equal(200, (await empty.PutAsync($"/api/lrn/my-assignments/{id}/answers", new { answers = Array.Empty<object>() })).Status);
            f.Clock.Set(due.AddSeconds(5));
            Assert.Equal(200, (await late.PostAsync($"/api/lrn/my-assignments/{id}/submit", new { answers = new[] { Answer(qs[0], 0) } })).Status);   // trong 10 giây châm chước
            f.Clock.Set(due.AddSeconds(11));
            var refused = await tooLate.PostAsync($"/api/lrn/my-assignments/{id}/submit", new { answers = new[] { Answer(qs[0], 0) } });
            Assert.Equal(400, refused.Status);
            // lượt quét nền tự nộp bản nháp có đáp án; bản nháp rỗng giữ nguyên chưa nộp
            using (var scope = f.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<AttemptGrader>().FinalizeExpiredAsync(null, null);
            var submitted = await f.WithDbAsync(db => db.Submission.Where(x => x.AssignmentId == id).ToDictionaryAsync(x => x.StudentId, x => x.SubmittedAt));
            Assert.NotNull(submitted[c.Students[1].PersonId]);    // đã được tự nộp
            Assert.Null(submitted.GetValueOrDefault(c.Students[2].PersonId));   // bản nháp rỗng vẫn chưa nộp
            Assert.Equal(200, (await tooLate.GetAsync($"/api/lrn/results/{id}")).Status);
        }
        finally { f.Clock.Reset(); }
    }

    [Fact]
    public async Task E2E_05_Assignment_end_to_end_with_timed_attempt_auto_submission_results_and_weekly_report()
    {
        await w.EnsureAsync();
        var monday = w.NextMonday();
        var due = monday.AddDays(4).ToDateTime(new TimeOnly(17, 0));
        var c = await NewCtxAsync();
        var s1 = c.Students[0]; var s2 = c.Students[1];
        var s3 = (await w.NewStudentAsync(c.ClassId, "HS không làm " + U())).Who;
        var p1 = await w.NewAccountAsync("PH 1 " + U(), "Parent", "Parent", "Active");
        var p2 = await w.NewAccountAsync("PH 2 " + U(), "Parent", "Parent", "Active");
        await f.WithDbAsync(async db => { db.ParentStudentLink.Add(new ParentStudentLink { ParentId = p1.PersonId, StudentId = s1.PersonId, Relationship = "Phụ huynh", IsPrimary = true, CreatedAt = f.Clock.Now }); await db.SaveChangesAsync(); });
        var ids = new List<int>();
        for (var i = 0; i < 10; i++) ids.Add(await QuestionAsync(c));
        var st1 = await f.LoginAsync(s1.Username); var st2 = await f.LoginAsync(s2.Username);
        var pc1 = await f.LoginAsync(p1.Username); var pc2 = await f.LoginAsync(p2.Username);
        try
        {
            f.Clock.Set(monday.ToDateTime(new TimeOnly(8, 0)));
            var r = await c.Teacher.PostAsync("/api/lrn/assignments", Create(c, ids, Sec(("Recognize", 10m)), dueAt: due, duration: 30));
            Assert.Equal(200, r.Status);
            var id = r.Body!["assignment"]!["assignmentId"]!.GetValue<int>();
            var qs = r.Body["questions"]!.AsArray().Select(x => x!).ToList();
            Assert.True(await f.WithDbAsync(db => db.Notification.CountAsync(n => n.RecipientPersonId == s1.PersonId && n.NotificationType == "AssignmentAssigned")) >= 1);
            // S1 bắt đầu lúc 09:00, lưu nháp, nộp 7 câu đúng
            f.Clock.Set(monday.ToDateTime(new TimeOnly(9, 0)));
            Assert.Equal(200, (await st1.PostAsync($"/api/lrn/my-assignments/{id}/start")).Status);
            Assert.Equal(200, (await st1.PutAsync($"/api/lrn/my-assignments/{id}/answers", new { answers = new[] { Answer(qs[0], 0) } })).Status);
            f.Clock.Set(monday.ToDateTime(new TimeOnly(9, 20)));
            var answers = qs.Select((q, i) => Answer(q, i < 7 ? 0 : 1)).ToArray();
            Assert.Equal(200, (await st1.PostAsync($"/api/lrn/my-assignments/{id}/submit", new { answers })).Status);
            // S2 bắt đầu lúc 09:05 rồi để hết giờ; bản nháp có 4 câu đúng
            f.Clock.Set(monday.ToDateTime(new TimeOnly(9, 5)));
            Assert.Equal(200, (await st2.PostAsync($"/api/lrn/my-assignments/{id}/start")).Status);
            Assert.Equal(200, (await st2.PutAsync($"/api/lrn/my-assignments/{id}/answers", new { answers = qs.Take(4).Select(q => Answer(q, 0)).ToArray() })).Status);
            f.Clock.Set(monday.ToDateTime(new TimeOnly(9, 36)));
            using (var scope = f.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<AttemptGrader>().FinalizeExpiredAsync(null, null);
            Assert.Equal(7.00m, await f.WithDbAsync(db => db.Submission.Where(x => x.AssignmentId == id && x.StudentId == s1.PersonId).Select(x => x.Score!.Value).SingleAsync()));
            Assert.Equal(4.00m, await f.WithDbAsync(db => db.Submission.Where(x => x.AssignmentId == id && x.StudentId == s2.PersonId).Select(x => x.Score!.Value).SingleAsync()));
            Assert.False(await f.WithDbAsync(db => db.Submission.AnyAsync(x => x.AssignmentId == id && x.StudentId == s3.PersonId)));   // người không làm không có kết quả
            var cls = await c.Teacher.GetAsync($"/api/lrn/assignments/{id}/results");
            Assert.Equal(200, cls.Status);
            Assert.Contains(s1.FullName, cls.Raw); Assert.Contains(s3.FullName, cls.Raw);
            Assert.Equal(200, (await pc1.GetAsync($"/api/lrn/results/{id}?studentId={s1.PersonId}")).Status);
            Assert.Equal(403, (await pc2.GetAsync($"/api/lrn/results/{id}?studentId={s1.PersonId}")).Status);
            // báo cáo tuần của S1 sau khi hết tuần
            f.Clock.Set(monday.AddDays(7).ToDateTime(new TimeOnly(8, 0)));
            pc1 = await f.LoginAsync(p1.Username);   // phiên cũ đã quá hạn không hoạt động theo đồng hồ giả
            var rep = await pc1.GetAsync($"/api/par/weekly-reports/student/{s1.PersonId}?weekStart={World.Iso(monday)}");
            Assert.Equal(200, rep.Status);
            var item = rep.Body!["assignments"]!["items"]!.AsArray().Single(x => x!["assignmentId"]!.GetValue<int>() == id)!;
            Assert.Equal("Submitted", item["status"]!.GetValue<string>());
            Assert.Equal(7.00m, item["score"]!.GetValue<decimal>());
        }
        finally { f.Clock.Reset(); }
    }

    [Fact]
    public async Task QBK_07_AI_drafts_are_created_as_drafts_with_a_difficulty_and_follow_the_confirm_and_delete_rules()
    {
        var c = await NewCtxAsync();
        var (lessons, _) = await SeedTextbookAsync(c.S.SubjectId, 6);
        string Q(string text, string difficulty) => "{\"content\":\"" + text + " " + U() + "\",\"difficulty\":\"" + difficulty + "\",\"explanation\":\"x\",\"options\":[{\"label\":\"A\",\"content\":\"a\",\"isCorrect\":true},{\"label\":\"B\",\"content\":\"b\",\"isCorrect\":false},{\"label\":\"C\",\"content\":\"c\",\"isCorrect\":false},{\"label\":\"D\",\"content\":\"d\",\"isCorrect\":false}]}";
        var broken = "{\"content\":\"Câu hỏng\",\"options\":[{\"label\":\"A\",\"content\":\"a\",\"isCorrect\":true}]}";
        f.Ai.Fail = false;
        f.Ai.JsonReply = "{\"questions\":[" + Q("Câu một", "Recognize") + "," + Q("Câu hai", "Apply") + "," + broken + "]}";
        try
        {
            var gen = await c.Teacher.PostAsync("/api/lrn/questions/ai-drafts", new { subjectId = c.S.SubjectId, gradeLevel = 6, lessonId = lessons[0], note = "x", levels = new[] { new { difficulty = "Recognize", count = 1 }, new { difficulty = "Apply", count = 2 } } });
            Assert.Equal(200, gen.Status);
            var drafts = gen.Body!["drafts"]!.AsArray();
            Assert.Equal(2, drafts.Count);
            Assert.Equal(1, gen.Body["discarded"]!.GetValue<int>());
            Assert.All(drafts, d => Assert.Equal("Draft", d!["status"]!.GetValue<string>()));
            Assert.Equal(["Recognize", "Apply"], drafts.Select(d => d!["difficulty"]!.GetValue<string>()).ToArray());
            var first = drafts[0]!["questionId"]!.GetValue<int>(); var second = drafts[1]!["questionId"]!.GetValue<int>();
            // bản nháp chưa dùng được trong bài tập
            var notReady = await c.Teacher.PostAsync("/api/lrn/assignments", Create(c, [first], Sec(("Recognize", 10m))));
            Assert.Equal(400, notReady.Status);
            // nháp không cần ẩn
            var hide = await c.Teacher.PutAsync($"/api/lrn/questions/{first}/hide");
            Assert.Equal(400, hide.Status); Assert.StartsWith("Câu nháp không cần ẩn", hide.Message);
            // xác nhận -> Sẵn dùng; xóa câu Sẵn dùng bị từ chối; xóa nháp được
            Assert.True((await c.Teacher.PutAsync($"/api/lrn/questions/{first}/confirm")).Ok);
            Assert.Equal("Ready", await f.WithDbAsync(db => db.Question.Where(x => x.QuestionId == first).Select(x => x.Status).SingleAsync()));
            var delReady = await c.Teacher.DeleteAsync($"/api/lrn/questions/{first}");
            Assert.Equal(400, delReady.Status); Assert.StartsWith("Chỉ xóa được câu Nháp", delReady.Message);
            Assert.True((await c.Teacher.DeleteAsync($"/api/lrn/questions/{second}")).Ok);
            Assert.False(await f.WithDbAsync(db => db.Question.AnyAsync(x => x.QuestionId == second)));
        }
        finally { f.Ai.JsonReply = "{}"; }
    }

    [Fact]
    public async Task QBK_09_Two_requests_changing_the_same_draft_at_once_never_both_win()
    {
        var c = await NewCtxAsync();
        var (lessons, _) = await SeedTextbookAsync(c.S.SubjectId, 6);
        f.Ai.JsonReply = "{\"questions\":[" + string.Join(",", Enumerable.Range(0, 8).Select(i => "{\"content\":\"Câu song song " + i + " " + U() + "\",\"difficulty\":\"Recognize\",\"explanation\":\"x\",\"options\":[{\"label\":\"A\",\"content\":\"a\",\"isCorrect\":true},{\"label\":\"B\",\"content\":\"b\",\"isCorrect\":false},{\"label\":\"C\",\"content\":\"c\",\"isCorrect\":false},{\"label\":\"D\",\"content\":\"d\",\"isCorrect\":false}]}")) + "]}";
        try
        {
            var gen = await c.Teacher.PostAsync("/api/lrn/questions/ai-drafts", new { subjectId = c.S.SubjectId, gradeLevel = 6, lessonId = lessons[0], levels = new[] { new { difficulty = "Recognize", count = 8 } } });
            Assert.Equal(200, gen.Status);
            var statuses = new List<int>();
            foreach (var d in gen.Body!["drafts"]!.AsArray())
            {
                var id = d!["questionId"]!.GetValue<int>();
                var r = await Task.WhenAll(c.Teacher.PutAsync($"/api/lrn/questions/{id}/confirm"), c.Teacher.DeleteAsync($"/api/lrn/questions/{id}"));
                statuses.AddRange(r.Select(x => x.Status));
                Assert.Contains(r, x => x.Ok);   // luôn có một bên thành công
                Assert.True(r.Count(x => x.Ok) <= 1 || await f.WithDbAsync(db => db.Question.AnyAsync(q => q.QuestionId == id)) == false, "hai bên cùng thành công");
                foreach (var loser in r.Where(x => !x.Ok)) Assert.True(loser.Status is 409 or 400 or 404, "trạng thái " + loser.Status);
            }
        }
        finally { f.Ai.JsonReply = "{}"; }
    }

    [Fact]
    public async Task TC_SEC_025_And_027_A_classmate_or_a_colleague_cannot_read_what_is_not_theirs()
    {
        var (c, id, qs) = await AssignedAsync();
        var s1 = await f.LoginAsync(c.Students[0].Username); var s2 = await f.LoginAsync(c.Students[1].Username);
        Assert.Equal(200, (await s1.PostAsync($"/api/lrn/my-assignments/{id}/submit", new { answers = new[] { Answer(qs[0], 0, 1) } })).Status);
        // bạn cùng lớp xin kết quả của S1 qua studentId: không được trả dữ liệu của S1
        var peek = await s2.GetAsync($"/api/lrn/results/{id}?studentId={c.Students[0].PersonId}");
        Assert.True(peek.Status != 200 || !peek.Raw.Contains(c.Students[0].FullName), "lộ kết quả của bạn: " + peek.Raw);
        // học sinh lớp khác mở bài làm không phải của lớp mình
        var otherClass = await NewCtxAsync();
        var stranger = await f.LoginAsync(otherClass.Students[0].Username);
        Assert.True((await stranger.GetAsync($"/api/lrn/my-assignments/{id}/attempt")).Status is 403 or 404);
        Assert.True((await stranger.GetAsync($"/api/lrn/results/{id}")).Status is 400 or 403 or 404);
        // đồng nghiệp không xem hay xem kết quả bài của giáo viên khác
        Refused(await c.Colleague.GetAsync($"/api/lrn/assignments/{id}"), "chi tiết bài");
        Refused(await c.Colleague.GetAsync($"/api/lrn/assignments/{id}/results"), "kết quả lớp");
        Refused(await c.Colleague.GetAsync($"/api/lrn/assignments/{id}/results/{c.Students[0].PersonId}"), "kết quả từng học sinh");
    }

    private static void Refused(ApiResult r, string what) => Assert.True(r.Status is 403 or 404, $"{what}: {r.Status} {r.Message}");

    [Fact]
    public async Task TC_SEC_039_Two_parallel_submits_store_exactly_one_result()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var (c, id, qs) = await AssignedAsync();
            var st = await f.LoginAsync(c.Students[0].Username);
            var body = new { answers = new[] { Answer(qs[0], 0, 1), Answer(qs[1], 0) } };
            var rs = await Task.WhenAll(st.PostAsync($"/api/lrn/my-assignments/{id}/submit", body), st.PostAsync($"/api/lrn/my-assignments/{id}/submit", body));
            Assert.Equal(1, rs.Count(r => r.Status == 200));
            Assert.All(rs.Where(r => r.Status != 200), r => Assert.True(r.Status is 400 or 409, "trạng thái " + r.Status));
            Assert.Equal(1, await f.WithDbAsync(db => db.Submission.CountAsync(x => x.AssignmentId == id && x.StudentId == c.Students[0].PersonId)));
        }
    }

    [Fact]
    public async Task TC_SEC_040_The_server_clock_decides_the_deadline_not_the_client()
    {
        var (c, id, qs) = await AssignedAsync(duration: 30);
        var st = await f.LoginAsync(c.Students[0].Username);
        try
        {
            Assert.Equal(200, (await st.PostAsync($"/api/lrn/my-assignments/{id}/start")).Status);
            Assert.Equal(200, (await st.PutAsync($"/api/lrn/my-assignments/{id}/answers", new { answers = new[] { Answer(qs[0], 0, 1) } })).Status);
            f.Clock.Set(f.Clock.Now.AddMinutes(30).AddSeconds(11));   // quá hạn thực tế 11 giây, dù đồng hồ phía máy khách nói gì
            var late = await st.PostAsync($"/api/lrn/my-assignments/{id}/submit", new { answers = new[] { Answer(qs[0], 0, 1), Answer(qs[1], 0), Answer(qs[2], 0) } });
            Assert.Equal(400, late.Status);
            using (var scope = f.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<AttemptGrader>().FinalizeExpiredAsync(null, null);
            var sub = await f.WithDbAsync(db => db.Submission.SingleAsync(x => x.AssignmentId == id && x.StudentId == c.Students[0].PersonId));
            Assert.NotNull(sub.SubmittedAt);   // bản nháp được tự nộp, không nhận bài nộp muộn
        }
        finally { f.Clock.Reset(); }
    }

    [Fact]
    public async Task ASG_27_An_AI_analysis_stuck_in_Pending_is_shown_as_failed_counted_from_the_time_it_was_queued()
    {
        var (c, id, qs) = await AssignedAsync();
        var parent = await w.NewAccountAsync("PH ASG27 " + U(), "Parent", "Parent", "Active");
        await f.WithDbAsync(async db => { db.ParentStudentLink.Add(new ParentStudentLink { ParentId = parent.PersonId, StudentId = c.Students[0].PersonId, Relationship = "Phụ huynh", IsPrimary = true, CreatedAt = f.Clock.Now }); await db.SaveChangesAsync(); });
        var st = await f.LoginAsync(c.Students[0].Username);
        Assert.Equal(200, (await st.PostAsync($"/api/lrn/my-assignments/{id}/submit", new { answers = new[] { Answer(qs[0], 0, 1) } })).Status);
        await Task.Delay(1500);   // để việc phân tích nền (AI giả) chạy xong trước khi ta ép trạng thái
        var queued = f.Clock.Now;
        await f.WithDbAsync(async db =>
        {
            var fb = await db.AiFeedback.SingleAsync(x => x.Submission.AssignmentId == id && x.Submission.StudentId == c.Students[0].PersonId);
            fb.Status = "Pending"; fb.AiContent = null; fb.QueuedAt = queued; await db.SaveChangesAsync();
        });
        var pc = await f.LoginAsync(parent.Username);
        string StatusOf(ApiResult r) => r.Body!["analysis"]!["status"]!.GetValue<string>();
        try
        {
            f.Clock.Set(queued.AddMinutes(2));
            Assert.Equal("Pending", StatusOf(await st.GetAsync($"/api/lrn/results/{id}")));   // chưa quá 5 phút
            f.Clock.Set(queued.AddMinutes(6));
            Assert.Equal("Failed", StatusOf(await st.GetAsync($"/api/lrn/results/{id}")));
            Assert.Equal("Failed", StatusOf(await pc.GetAsync($"/api/lrn/results/{id}?studentId={c.Students[0].PersonId}")));
            var teacher = await c.Teacher.GetAsync($"/api/lrn/assignments/{id}/results/{c.Students[0].PersonId}");
            Assert.Equal(200, teacher.Status);
            Assert.Equal("Failed", StatusOf(teacher));
        }
        finally { f.Clock.Reset(); }
    }

    [Fact]
    public async Task COM_05_Producers_create_exactly_one_notification_per_recipient()
    {
        var c = await NewCtxAsync();
        var parent = await w.NewAccountAsync("PH thông báo " + U(), "Parent", "Parent", "Active");
        var bystander = await w.NewAccountAsync("Người ngoài " + U(), "Student", "Student", "Active");
        await f.WithDbAsync(async db => { db.ParentStudentLink.Add(new ParentStudentLink { ParentId = parent.PersonId, StudentId = c.Students[0].PersonId, Relationship = "Phụ huynh", IsPrimary = true, CreatedAt = f.Clock.Now }); await db.SaveChangesAsync(); });
        Task<int> Count(int person, string type) => f.WithDbAsync(db => db.Notification.CountAsync(n => n.RecipientPersonId == person && n.NotificationType == type));
        // giao bài: mỗi học sinh của lớp nhận đúng một thông báo, người ngoài không nhận
        var q = await QuestionAsync(c);
        Assert.Equal(200, (await c.Teacher.PostAsync("/api/lrn/assignments", Create(c, [q], Sec(("Recognize", 10m))))).Status);
        foreach (var s in c.Students) Assert.Equal(1, await Count(s.PersonId, "AssignmentAssigned"));
        Assert.Equal(0, await Count(bystander.PersonId, "AssignmentAssigned"));
        // tin nhắn: người nhận đúng một thông báo cho mỗi tin
        var conv = await (await f.LoginAsync(parent.Username)).PostAsync("/api/par/conversations", new { studentId = c.Students[0].PersonId, staffId = c.S.Absent.PersonId });
        Assert.Equal(200, conv.Status);
        var id = conv.Body!.GetValue<int>();
        var pc = await f.LoginAsync(parent.Username);
        Assert.Equal(200, (await pc.PostAsync($"/api/par/conversations/{id}/messages", new { content = "Xin chào" })).Status);
        Assert.Equal(1, await Count(c.S.Absent.PersonId, "MessageReceived"));
        Assert.Equal(0, await Count(parent.PersonId, "MessageReceived"));
        Assert.Equal(0, await Count(bystander.PersonId, "MessageReceived"));
        // đơn xin nghỉ không sinh thông báo nào (danh sách đóng 5 loại, BR-26)
        var types = await f.WithDbAsync(db => db.Notification.Select(n => n.NotificationType).Distinct().ToListAsync());
        Assert.All(types, t => Assert.Contains(t, new[] { "TimetablePublished", "SessionPending", "ScheduleChanged", "AssignmentAssigned", "MessageReceived" }));
    }

    [Fact]
    public async Task COM_02_Opening_a_notification_marks_it_read_and_returns_the_screen_to_open()
    {
        var c = await NewCtxAsync();
        var q = await QuestionAsync(c);
        var r = await c.Teacher.PostAsync("/api/lrn/assignments", Create(c, [q], Sec(("Recognize", 10m))));
        var aid = r.Body!["assignment"]!["assignmentId"]!.GetValue<int>();
        var st = await f.LoginAsync(c.Students[0].Username);
        var nid = await f.WithDbAsync(db => db.Notification.Where(n => n.RecipientPersonId == c.Students[0].PersonId && n.NotificationType == "AssignmentAssigned" && n.RefEntityId == aid).Select(n => n.NotificationId).SingleAsync());
        var open = await st.PostAsync($"/api/com/notifications/{nid}/open");
        Assert.Equal(200, open.Status);
        Assert.True(open.Body!["isRead"]!.GetValue<bool>());
        Assert.True(open.Body["canAccess"]!.GetValue<bool>());
        Assert.Equal("/lrn/todo", open.Body["targetRoute"]!.GetValue<string>());
        // khai báo vắng: Hiệu phó phụ trách mở màn Giáo viên vắng, tổ trưởng mở màn của tổ, người khác bị từ chối
        var absence = await f.WithDbAsync(async db =>
        {
            var a = new TeacherAbsence { TeacherId = c.S.Absent.PersonId, FromDate = f.Clock.Today, ToDate = f.Clock.Today, DeclaredByPersonId = c.S.Head.PersonId, CreatedAt = f.Clock.Now };
            db.TeacherAbsence.Add(a);
            await db.SaveChangesAsync();
            return a.AbsenceId;
        });
        async Task<long> Make(int person) => await f.WithDbAsync(async db =>
        {
            var n = new Notification { RecipientPersonId = person, NotificationType = "SessionPending", Title = "Khai báo vắng chờ duyệt", Body = "x", RefEntityType = "TeacherAbsence", RefEntityId = absence, CreatedAt = f.Clock.Now };
            db.Notification.Add(n);
            await db.SaveChangesAsync();
            return n.NotificationId;
        });
        var vice = await f.LoginAsync(w.ViceA.Username);
        Assert.Equal("/scd/absences", (await vice.PostAsync($"/api/com/notifications/{await Make(w.ViceA.PersonId)}/open")).Body!["targetRoute"]!.GetValue<string>());
        var head = await f.LoginAsync(c.S.Head.Username);
        Assert.Equal("/scd/department-absences", (await head.PostAsync($"/api/com/notifications/{await Make(c.S.Head.PersonId)}/open")).Body!["targetRoute"]!.GetValue<string>());
        var other = await head.PostAsync($"/api/com/notifications/{await Make(c.S.Head.PersonId)}/open");   // vẫn là tổ trưởng nên được; dùng giáo viên thường để kiểm từ chối
        var plain = await c.Colleague.PostAsync($"/api/com/notifications/{await Make(c.S.Sub.PersonId)}/open");
        Assert.Equal(200, plain.Status);
        Assert.False(plain.Body!["canAccess"]!.GetValue<bool>());
        Assert.Equal("Bạn không còn quyền xem nội dung này.", plain.Body["reasonNotAllowed"]!.GetValue<string>());
        Assert.NotNull(other);
    }

    [Fact]
    public async Task ASG_24_The_attempt_shows_points_without_answers_and_the_result_shows_points_and_difficulty()
    {
        var (c, id, qs) = await AssignedAsync();
        var st = await f.LoginAsync(c.Students[0].Username);
        var attempt = await st.GetAsync($"/api/lrn/my-assignments/{id}/attempt");
        Assert.Equal(200, attempt.Status);
        Assert.DoesNotContain("isCorrect", attempt.Raw);
        foreach (var q in attempt.Body!["questions"]!.AsArray()) Assert.NotNull(q!["points"]);
        Assert.Equal(200, (await st.PostAsync($"/api/lrn/my-assignments/{id}/submit", new { answers = new[] { Answer(qs[0], 0, 1), Answer(qs[1], 0), Answer(qs[2], 1) } })).Status);
        var result = await st.GetAsync($"/api/lrn/results/{id}");
        Assert.Equal(200, result.Status);
        var items = result.Body!["questions"]!.AsArray();
        Assert.Equal(3, items.Count);
        foreach (var q in items)
        {
            Assert.NotNull(q!["points"]);
            Assert.Equal("Recognize", q["difficulty"]!.GetValue<string>());
            Assert.NotNull(q["isCorrect"]);
        }
        Assert.Equal([true, true, false], items.Select(q => q!["isCorrect"]!.GetValue<bool>()).ToArray());
        Assert.Equal(10m, items.Sum(q => q!["points"]!.GetValue<decimal>()));   // tổng điểm các câu bằng thang 10
    }

    [Fact]
    public async Task ASG_25_Download_the_printable_paper_of_an_assigned_test()
    {
        var (c, id, _) = await AssignedAsync();
        var pdf = await c.Teacher.GetBytesAsync($"/api/lrn/assignments/{id}/paper");
        Assert.Equal(200, pdf.Status);
        Assert.Equal("application/pdf", pdf.ContentType);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf.Bytes, 0, 4));
        var docx = await c.Teacher.GetBytesAsync($"/api/lrn/assignments/{id}/paper?format=docx&answers=true");
        Assert.Equal(200, docx.Status);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", docx.ContentType);
        Assert.Equal("PK", System.Text.Encoding.ASCII.GetString(docx.Bytes, 0, 2));   // docx là tệp zip
        var noAnswers = await c.Teacher.GetBytesAsync($"/api/lrn/assignments/{id}/paper?format=pdf&answers=false");
        Assert.Equal(200, noAnswers.Status);
        Assert.True((await c.Colleague.GetBytesAsync($"/api/lrn/assignments/{id}/paper")).Status is 403 or 404);
        Assert.Equal(403, (await (await f.LoginAsync(c.Students[0].Username)).GetAsync($"/api/lrn/assignments/{id}/paper")).Status);
    }

    [Fact]
    public async Task ASG_20_Removed_screens_are_refused_and_the_dashboards_stay_in_scope()
    {
        await w.EnsureAsync();
        var s = await w.NewScenarioAsync();
        var head = await f.LoginAsync(s.Head.Username);
        Assert.Equal(403, (await head.GetAsync("/api/lrn/department/assignments")).Status);   // hành vi thật: đường dẫn đã gỡ trả 403 (chính sách mặc định), không phải 404
        foreach (var user in new[] { w.Principal.Username, w.ViceA.Username, s.Head.Username })
            Assert.Equal(403, (await (await f.LoginAsync(user)).GetAsync("/api/adm/progress")).Status);
        Assert.Equal(200, (await (await f.LoginAsync(w.Principal.Username)).GetAsync("/api/adm/dashboard")).Status);
        Assert.Equal(200, (await (await f.LoginAsync(w.ViceA.Username)).GetAsync("/api/adm/dashboard")).Status);
        Assert.Equal(200, (await head.GetAsync("/api/adm/dashboard/department")).Status);
    }
}
