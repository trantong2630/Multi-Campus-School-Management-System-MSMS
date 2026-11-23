using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MSMS.Api.Common.Ai;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Adm.Ai;
using MSMS.Api.Modules.Adm.Dtos;
using MSMS.Api.Modules.Scd.Services;
using MSMS.Tests.Modules.Iam;

namespace MSMS.Tests.Modules.Adm;

/// <summary>ADM-01 MSMS AI của quản lý: phạm vi, không lộ tên, dừng vòng gọi tool, biểu đồ lấy từ số của tool. Dùng AI giả, không gọi Gemini.</summary>
public class AiChatTests
{
    /// <summary>AI giả chạy theo kịch bản; ghi lại hội thoại nhận được ở mỗi lượt.</summary>
    private sealed class ScriptedAi(params Func<AiChatReply>[] steps) : IAiClient
    {
        public List<(List<AiChatMessage> Messages, bool AllowTools)> Calls { get; } = [];

        public Task<string> GenerateJsonAsync(string systemInstruction, string prompt, IReadOnlyList<AiImage> images, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<AiChatReply> ChatAsync(string systemInstruction, IReadOnlyList<AiChatMessage> messages, IReadOnlyList<AiTool> tools, bool allowTools,
            CancellationToken ct = default)
        {
            Calls.Add((messages.ToList(), allowTools));
            return Task.FromResult(steps[Math.Min(Calls.Count - 1, steps.Length - 1)]());
        }

        /// <summary>Kết quả các tool Backend gửi lại cho AI ở lượt thứ <paramref name="call"/> (đếm từ 0).</summary>
        public List<JsonObject> ToolResults(int call) =>
            Calls[call].Messages.OfType<AiToolResults>().Last().Results.Select(r => r.Result).ToList();
    }

    private static Func<AiChatReply> Tool(string name, string args = "{}") =>
        () => new AiChatReply(null, [new AiToolCall(name, JsonDocument.Parse(args).RootElement.Clone())], null);

    private static Func<AiChatReply> Say(string text) => () => new AiChatReply(text, [], null);

    private static AdmAiChatService Chat(AccountTestFixture f, string role, int personId, IAiClient ai, int rounds = 4)
    {
        var user = AccountTestFixture.User(role, personId);
        return new AdmAiChatService(f.Db, user, new ScopeResolver(f.Db, user, f.Clock), f.Clock, ai,
            Options.Create(new AiOptions { ChatMaxToolRounds = rounds, ChatHistoryTurns = 20 }),
            new SubjectResponsibilityService(f.Db, f.Clock), NullLogger<AdmAiChatService>.Instance);
    }

    /// <summary>Hiệu phó 1 phụ trách phân hiệu 1, Hiệu trưởng 2; Trần Thị Lan (100) lớp 6A phân hiệu 1, Lê Văn Minh (101) lớp 6B phân hiệu 2, cả hai vắng hôm qua.</summary>
    private static AccountTestFixture Seed()
    {
        var f = new AccountTestFixture();
        f.AddAccount(1, AccountTypes.VicePrincipal);
        f.AddAccount(2, AccountTypes.Principal);
        f.Db.CampusLeadership.Add(new CampusLeadership { StaffId = 1, CampusId = 1, StartDate = f.Clock.Today.AddDays(-10) });
        f.AddPerson(100, "Student", "Trần Thị Lan");
        f.AddPerson(101, "Student", "Lê Văn Minh", campusId: 2);
        f.Db.Enrollment.Add(new Enrollment { StudentId = 100, ClassId = 1, StartDate = f.Clock.Today.AddDays(-30) });
        f.Db.Enrollment.Add(new Enrollment { StudentId = 101, ClassId = 2, StartDate = f.Clock.Today.AddDays(-30) });
        f.Db.SaveChanges();
        Absent(f, 1, 100);
        Absent(f, 2, 101);
        return f;
    }

    private static void Absent(AccountTestFixture f, int classId, int studentId)
    {
        var lesson = f.AddLesson(5, classId, days: -1);
        lesson.AttendanceRecord.Add(new AttendanceRecord { StudentId = studentId, Status = "Absent", RecordedByPersonId = 5, RecordedAt = f.Clock.Now });
        f.Db.SaveChanges();
    }

    [Fact]
    public async Task VicePrincipal_AskingAnotherCampus_ToolRefuses()
    {
        using var f = Seed();
        var ai = new ScriptedAi(Tool("attendance_summary", """{"campus_ids":[2]}"""), Say("Không xem được."));
        await Chat(f, AccountTypes.VicePrincipal, 1, ai).AskAsync(new AiChatRequest("Chuyên cần phân hiệu 2?", null));

        var result = Assert.Single(ai.ToolResults(1));
        Assert.Contains("Không có quyền", (string?)result["error"]);
        Assert.Null(result["rows"]);
    }

    [Fact]
    public async Task Tools_SendCodesNotNames_AndOnlyInScopeCodesBecomeNames()
    {
        using var f = Seed();
        var ai = new ScriptedAi(Tool("absent_students", """{"period":"this_week"}"""), Say("HS#100 và HS#101 vắng nhiều nhất."));
        var answer = await Chat(f, AccountTypes.VicePrincipal, 1, ai).AskAsync(new AiChatRequest("Em nào vắng nhiều nhất tuần này?", null));

        var sent = ai.ToolResults(1)[0].ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Assert.Contains("HS#100", sent);
        Assert.DoesNotContain("HS#101", sent);
        Assert.DoesNotContain("Trần Thị Lan", sent);
        Assert.DoesNotContain("HS000100", sent);

        Assert.Contains("Trần Thị Lan", answer.Text);
        Assert.Contains("HS#101", answer.Text);
        Assert.DoesNotContain("Lê Văn Minh", answer.Text);
        Assert.Equal("HS#100 và HS#101 vắng nhiều nhất.", answer.HistoryText);
        Assert.Contains("Học sinh vắng", answer.Steps);
    }

    [Fact]
    public async Task NamesInQuestionAndHistory_AreMaskedBeforeReachingAi()
    {
        using var f = Seed();
        var ai = new ScriptedAi(Say("Được."));
        await Chat(f, AccountTypes.Principal, 2, ai).AskAsync(new AiChatRequest("Còn em trần thị lan thì sao?",
            [new AiChatTurnDto("user", "Em Lê Văn Minh (HS000101) vắng mấy buổi?"), new AiChatTurnDto("ai", "HS#101 vắng 1 buổi.")]));

        var texts = ai.Calls[0].Messages.Select(m => m switch { AiUserText u => u.Text, AiModelText t => t.Text, _ => "" }).ToList();
        Assert.Equal("Em HS#101 (HS#101) vắng mấy buổi?", texts[0]);
        Assert.Equal("HS#101 vắng 1 buổi.", texts[1]);
        Assert.Equal("Còn em HS#100 thì sao?", texts[2]);
    }

    [Fact]
    public async Task ToolLoop_StopsAtLimit_LastRoundForbidsTools()
    {
        using var f = Seed();
        var ai = new ScriptedAi(Tool("attendance_summary"));
        var ex = await Assert.ThrowsAsync<ApiException>(() => Chat(f, AccountTypes.Principal, 2, ai, rounds: 2).AskAsync(new AiChatRequest("Chuyên cần?", null)));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(3, ai.Calls.Count);
        Assert.Equal([true, true, false], ai.Calls.Select(c => c.AllowTools));
    }

    [Fact]
    public async Task Ranking_SkipsStudentsWithFewerThanThreeSubmissions()
    {
        using var f = Seed();
        f.AddPerson(102, "Student", "Phạm Hồng Nhung");
        f.Db.Enrollment.Add(new Enrollment { StudentId = 102, ClassId = 1, StartDate = f.Clock.Today.AddDays(-30) });
        decimal[] lanScores = [7, 8, 9];
        for (var i = 0; i < 3; i++)
        {
            var a = new Assignment { AssignmentId = 10 + i, ClassId = 1, SubjectId = 1, Title = $"Bài {i}", AssignedByPersonId = 5,
                OpenAt = f.Clock.Now.AddDays(-5), DueAt = f.Clock.Now.AddDays(-1), CreatedAt = f.Clock.Now };
            a.AssignmentRecipient.Add(new AssignmentRecipient { StudentId = 100 });
            a.AssignmentRecipient.Add(new AssignmentRecipient { StudentId = 102 });
            a.Submission.Add(new Submission { StudentId = 100, StartedAt = f.Clock.Now.AddDays(-2), SubmittedAt = f.Clock.Now.AddDays(-2), Score = lanScores[i] });
            if (i == 0) a.Submission.Add(new Submission { StudentId = 102, StartedAt = f.Clock.Now.AddDays(-2), SubmittedAt = f.Clock.Now.AddDays(-2), Score = 10 });
            f.Db.Assignment.Add(a);
        }
        f.Db.SaveChanges();

        var ai = new ScriptedAi(Tool("student_ranking", """{"metric":"avg_score"}"""), Say("Xong."));
        await Chat(f, AccountTypes.Principal, 2, ai).AskAsync(new AiChatRequest("Em nào điểm cao nhất?", null));

        var result = ai.ToolResults(1)[0];
        var row = Assert.Single(result["rows"]!.AsArray());
        Assert.Equal("HS#100", (string?)row!["code"]);
        Assert.Equal(8.0, (double?)row["avg_score"]);
        Assert.Equal(1, (int?)result["eligible_students"]);
    }

    [Fact]
    public async Task Chart_IsBuiltFromToolRows_NotFromAiText()
    {
        using var f = Seed();
        var ai = new ScriptedAi(Tool("attendance_summary", """{"group_by":"class","period":"this_week"}"""),
            Tool("show_chart", """{"result_id":1,"metric":"rate","title":"Chuyên cần theo lớp"}"""), Say("Hai lớp đều vắng."));
        var answer = await Chat(f, AccountTypes.Principal, 2, ai).AskAsync(new AiChatRequest("So sánh chuyên cần các lớp tuần này", null));

        var chart = Assert.Single(answer.Charts);
        Assert.Equal("bar", chart.Kind);
        Assert.Equal("%", chart.Unit);
        Assert.Equal(90, chart.Threshold);
        Assert.Equal(["6A", "6B"], chart.Rows.Select(r => r.Label).Order());
        Assert.All(chart.Rows, r => Assert.Equal(0, r.Value));
    }

    [Fact]
    public async Task Chart_AcceptsIntegerMetrics()
    {
        using var f = Seed();
        f.AddAccount(5, AccountTypes.Teacher);
        f.Publish(1, 1, 5);
        var ai = new ScriptedAi(Tool("teaching_load"), Tool("show_chart", """{"result_id":1,"metric":"periods_per_week","title":"Tải dạy"}"""), Say("Xong."));
        var answer = await Chat(f, AccountTypes.Principal, 2, ai).AskAsync(new AiChatRequest("Tải dạy của giáo viên", null));

        var row = Assert.Single(Assert.Single(answer.Charts).Rows);
        Assert.Equal(1, row.Value);
        Assert.Equal("Nguyễn An 5", row.Label);
    }

    [Fact]
    public async Task TeachingLoad_TooFewPeers_HasNoDepartmentComparison_ButHasSummary()
    {
        using var f = Seed();
        f.AddAccount(5, AccountTypes.Teacher);
        f.Publish(1, 1, 5);
        var ai = new ScriptedAi(Tool("teaching_load"), Say("Xong."));
        await Chat(f, AccountTypes.Principal, 2, ai).AskAsync(new AiChatRequest("Tải dạy của giáo viên", null));

        var result = ai.ToolResults(1)[0];
        var row = Assert.Single(result["rows"]!.AsArray())!;
        Assert.Equal(1, (int?)row["department_teachers"]);
        Assert.Null(row["department_average"]);
        Assert.Null(row["above_department_average"]);
        Assert.Equal(1, (int?)result["summary"]!["teachers_with_load"]);
        Assert.Equal(1, (int?)result["summary"]!["max_periods"]);
    }

    /// <summary>Em 103 học 6A rồi chuyển sang 6B, vắng một tiết ở 6A; em 104 vắng ở 6A rồi rời trường.</summary>
    private static AccountTestFixture SeedMoves()
    {
        var f = Seed();
        f.AddPerson(103, "Student", "Đỗ Văn Nam");
        f.AddPerson(104, "Student", "Vũ Thị Hạnh");
        f.Db.Enrollment.Add(new Enrollment { StudentId = 103, ClassId = 1, StartDate = f.Clock.Today.AddDays(-30), EndDate = f.Clock.Today.AddDays(-5) });
        f.Db.Enrollment.Add(new Enrollment { StudentId = 103, ClassId = 2, StartDate = f.Clock.Today.AddDays(-4) });
        f.Db.Enrollment.Add(new Enrollment { StudentId = 104, ClassId = 1, StartDate = f.Clock.Today.AddDays(-30), EndDate = f.Clock.Today.AddDays(-5) });
        var lesson = f.AddLesson(5, 1, days: -6);
        lesson.AttendanceRecord.Add(new AttendanceRecord { StudentId = 103, Status = "Absent", RecordedByPersonId = 5, RecordedAt = f.Clock.Now });
        lesson.AttendanceRecord.Add(new AttendanceRecord { StudentId = 104, Status = "Absent", RecordedByPersonId = 5, RecordedAt = f.Clock.Now });
        f.Db.SaveChanges();
        return f;
    }

    [Fact]
    public async Task StudentProfile_CountsOldClass_AfterMovingClass()
    {
        using var f = SeedMoves();
        var ai = new ScriptedAi(Tool("student_profile", """{"student":"HS#103"}"""), Say("Xong."));
        await Chat(f, AccountTypes.Principal, 2, ai).AskAsync(new AiChatRequest("Tình hình em HS#103", null));

        var result = ai.ToolResults(1)[0];
        Assert.Equal("6B", (string?)result["student"]!["class"]);
        Assert.Equal(1, (int?)result["attendance"]!["marked"]);
        Assert.Equal(1, (int?)result["attendance"]!["absent_unexcused"]);
    }

    [Fact]
    public async Task AbsentStudents_ListsOnlyCurrentStudents_InTheirCurrentClass()
    {
        using var f = SeedMoves();
        var ai = new ScriptedAi(Tool("absent_students"), Say("Xong."));
        await Chat(f, AccountTypes.Principal, 2, ai).AskAsync(new AiChatRequest("Em nào vắng nhiều nhất?", null));

        var rows = ai.ToolResults(1)[0]["rows"]!.AsArray();
        Assert.DoesNotContain(rows, r => (string?)r!["code"] == "HS#104");
        Assert.Equal("6B", (string?)rows.Single(r => (string?)r!["code"] == "HS#103")!["class"]);
    }

    [Fact]
    public async Task FuturePeriod_ToolSaysNotYet()
    {
        using var f = Seed();
        var from = f.Clock.Today.AddDays(5).ToString("yyyy-MM-dd");
        var to = f.Clock.Today.AddDays(9).ToString("yyyy-MM-dd");
        var ai = new ScriptedAi(Tool("attendance_summary", $$"""{"period":"custom","from":"{{from}}","to":"{{to}}"}"""), Say("Xong."));
        await Chat(f, AccountTypes.Principal, 2, ai).AskAsync(new AiChatRequest("Chuyên cần tuần sau?", null));

        Assert.Contains("chưa tới", (string?)ai.ToolResults(1)[0]["error"]);
    }

    [Fact]
    public async Task PendingNow_FollowsTeacherFilter()
    {
        using var f = Seed();
        f.AddAccount(5, AccountTypes.Teacher);
        f.AddAccount(6, AccountTypes.Teacher);
        foreach (var teacher in new[] { 5, 6 })
        {
            var absence = new TeacherAbsence { TeacherId = teacher, FromDate = f.Clock.Today.AddDays(2), ToDate = f.Clock.Today.AddDays(2), CreatedAt = f.Clock.Now };
            f.Db.TeacherAbsence.Add(absence);
            var lesson = f.AddLesson(teacher, 1, days: 2, status: "Pending");
            lesson.TeacherAbsence = absence;
        }
        f.Db.SaveChanges();

        var ai = new ScriptedAi(Tool("teacher_absence_summary", """{"teacher":"GV#5"}"""), Tool("teacher_absence_summary"), Say("Xong."));
        await Chat(f, AccountTypes.Principal, 2, ai).AskAsync(new AiChatRequest("Tiết chờ người dạy", null));

        Assert.Equal(1, (int?)ai.ToolResults(1)[0]["pending_now"]);
        Assert.Equal(2, (int?)ai.ToolResults(2)[0]["pending_now"]);
    }

    [Fact]
    public async Task Practice_ByTeacher_CountsGivenAndOpen_AndFiltersByAssigner()
    {
        using var f = Seed();
        f.AddAccount(5, AccountTypes.Teacher);
        f.AddAccount(6, AccountTypes.Teacher);
        void Give(int id, int teacher, int openDays, int dueDays) => f.Db.Assignment.Add(new Assignment { AssignmentId = id, ClassId = 1, SubjectId = 1,
            Title = $"Bài {id}", AssignedByPersonId = teacher, OpenAt = f.Clock.Now.AddDays(openDays), DueAt = f.Clock.Now.AddDays(dueDays), CreatedAt = f.Clock.Now });
        Give(20, 5, -6, -2);  // GV 5: một bài đã hết hạn
        Give(21, 5, -1, 3);   // GV 5: một bài đang mở
        Give(22, 6, -5, -1);  // GV 6: một bài đã hết hạn
        f.Db.SaveChanges();

        var ai = new ScriptedAi(Tool("practice_summary", """{"group_by":"teacher"}"""), Tool("practice_summary", """{"teacher":"GV#5"}"""), Say("Xong."));
        await Chat(f, AccountTypes.Principal, 2, ai).AskAsync(new AiChatRequest("Tiến độ giao bài theo giáo viên", null));

        var rows = ai.ToolResults(1)[0]["rows"]!.AsArray();
        var five = rows.Single(r => (string?)r!["label"] == "GV#5")!;
        Assert.Equal(2, (int?)five["given"]);
        Assert.Equal(1, (int?)five["open_now"]);
        Assert.Equal(1, (int?)five["assignments"]);
        Assert.Equal(1, (int?)rows.Single(r => (string?)r!["label"] == "GV#6")!["given"]);

        var one = ai.ToolResults(2)[0];
        Assert.Contains("GV#5", (string?)one["scope"]);
        Assert.Equal(2, (int?)Assert.Single(one["rows"]!.AsArray())!["given"]);
    }

    [Fact]
    public async Task AiFailure_ReturnsReadableError()
    {
        using var f = Seed();
        var ai = new ScriptedAi(() => throw new AiException("AI đang hết hạn mức gọi. Chờ một lúc rồi chạy lại."));
        var ex = await Assert.ThrowsAsync<ApiException>(() => Chat(f, AccountTypes.Principal, 2, ai).AskAsync(new AiChatRequest("Chuyên cần?", null)));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("hết hạn mức", ex.Message);
    }

    [Fact]
    public async Task PeopleSearch_IgnoresAccents_AndStaysInScope()
    {
        using var f = Seed();
        var vp = Chat(f, AccountTypes.VicePrincipal, 1, new ScriptedAi(Say("")));
        var lan = Assert.Single(await vp.PeopleAsync("tran thi"));
        Assert.Equal("HS#100", lan.Code);
        Assert.Equal("Trần Thị Lan", lan.Name);
        Assert.Empty(await vp.PeopleAsync("minh"));
    }

    [Fact]
    public async Task Teacher_CannotUseAssistant()
    {
        using var f = Seed();
        f.AddAccount(5, AccountTypes.Teacher);
        var ex = await Assert.ThrowsAsync<ApiException>(() => Chat(f, AccountTypes.Teacher, 5, new ScriptedAi(Say(""))).AskAsync(new AiChatRequest("Chuyên cần?", null)));
        Assert.Equal(403, ex.StatusCode);
    }
}
