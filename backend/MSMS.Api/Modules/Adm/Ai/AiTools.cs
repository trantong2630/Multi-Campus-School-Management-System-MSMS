using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Ai;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Adm.Services;
using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Api.Modules.Adm.Ai;

/// <summary>
/// Bộ tool chỉ đọc của MSMS AI cho quản lý (ADM-01). Mọi số do Backend tính sẵn (tỷ lệ, chênh lệch, xếp hạng); AI chỉ chọn tool và diễn giải.
/// Người chỉ trả dưới dạng mã HS#id, GV#id kèm các trường trong danh sách trắng (lớp, khối, phân hiệu, giới tính, tổ); không có tên, ngày sinh,
/// CCCD, liên lạc, địa chỉ hay phụ huynh. Phạm vi luôn qua <see cref="AiScope"/>.
/// </summary>
internal sealed class AiTools(MsmsDbContext db, AiScope scope, SubjectResponsibilityService responsibility, IClock clock)
{
    /// <summary>Chỉ xếp hạng điểm, tỷ lệ nộp của học sinh đã có từ ngần này bài, tránh em làm một bài được 10 đứng đầu.</summary>
    public const int MinSubmissions = 3;

    /// <summary>Số dòng tối đa khi trả danh sách người.</summary>
    public const int MaxPeople = 10;

    private const int MaxGroups = 60;

    /// <summary>Chỉ so một giáo viên với trung bình tổ khi tổ có từ ngần này người trong phạm vi; ít hơn thì trung bình gần như là chính người đó.</summary>
    public const int MinDepartmentPeers = 3;

    public const string ShowChart = "show_chart";

    public static string StepLabel(string tool) => tool switch
    {
        "attendance_summary" => "Chuyên cần",
        "absent_students" => "Học sinh vắng",
        "practice_summary" => "Bài tập rèn luyện",
        "student_ranking" => "Xếp hạng học sinh",
        "student_profile" => "Tình hình một học sinh",
        "teacher_absence_summary" => "Giáo viên vắng",
        "teaching_load" => "Tải dạy",
        ShowChart => "Vẽ biểu đồ",
        _ => tool,
    };

    public Task<JsonObject> RunAsync(string tool, JsonElement args) => tool switch
    {
        "attendance_summary" => AttendanceSummaryAsync(args),
        "absent_students" => AbsentStudentsAsync(args),
        "practice_summary" => PracticeSummaryAsync(args),
        "student_ranking" => StudentRankingAsync(args),
        "student_profile" => StudentProfileAsync(args),
        "teacher_absence_summary" => TeacherAbsenceAsync(args),
        "teaching_load" => TeachingLoadAsync(args),
        _ => throw ApiException.BadRequest($"Không có tool \"{tool}\"."),
    };

    // ---------------------------------------------------------------- Chuyên cần

    private sealed record Tally(int ClassId, DateOnly Date, int PeriodNo, string SessionOfDay, string Status, int Count);

    private sealed record Absence(int StudentId, int ClassId, DateOnly Date, int PeriodNo, string SessionOfDay, bool Excused);

    private sealed record Group(string Key, string Label, double Order);

    private async Task<JsonObject> AttendanceSummaryAsync(JsonElement args)
    {
        var f = AiArgs.Filter(args);
        var classes = scope.ClassesFor(f);
        var period = scope.PeriodFor(args);
        var groupBy = AiArgs.Str(args, "group_by") ?? "none";
        var byId = classes.ToDictionary(c => c.Id);
        var keyOf = AttendanceKey(groupBy, byId, scope.Describe(f));

        async Task<Dictionary<string, (Group Group, JsonObject Row)>> Measure(AiPeriod p)
        {
            var (tallies, absences) = await AttendanceAsync(byId.Keys.ToList(), p);
            var groups = new Dictionary<string, (Group, List<Tally>, List<Absence>)>();
            (Group, List<Tally>, List<Absence>) Slot(Group g) =>
                groups.TryGetValue(g.Key, out var s) ? s : groups[g.Key] = (g, [], []);
            // Nhóm theo phân hiệu, khối, lớp: hiện cả nhóm có học sinh mà chưa có điểm danh.
            if (groupBy is "campus" or "grade" or "class")
                foreach (var c in classes.Where(c => c.Students > 0)) Slot(keyOf(c.Id, p.From, 0, ""));
            foreach (var t in tallies) Slot(keyOf(t.ClassId, t.Date, t.PeriodNo, t.SessionOfDay)).Item2.Add(t);
            foreach (var a in absences) Slot(keyOf(a.ClassId, a.Date, a.PeriodNo, a.SessionOfDay)).Item3.Add(a);
            return groups.ToDictionary(g => g.Key, g =>
            {
                var row = AttendanceRow(g.Value.Item2, g.Value.Item3);
                if (groupBy is "campus" or "grade" or "class")
                    row["students"] = classes.Where(c => keyOf(c.Id, p.From, 0, "").Key == g.Key).Sum(c => c.Students);
                return (g.Value.Item1, row);
            });
        }

        var current = await Measure(period);
        var previous = AiArgs.Bool(args, "compare_previous") ? await Measure(period.Previous) : null;
        var rows = current.Values.Select(x =>
        {
            var row = new JsonObject { ["label"] = x.Group.Label };
            foreach (var kv in x.Row) row[kv.Key] = kv.Value?.DeepClone();
            if (previous is not null)
            {
                var prev = previous.TryGetValue(x.Group.Key, out var p) ? (double?)p.Row["rate"] : null;
                row["previous_rate"] = prev;
                row["change"] = Change((double?)x.Row["rate"], prev);
            }
            return (x.Group, Row: row);
        }).ToList();

        var ordered = groupBy is "week" or "weekday" or "period_slot"
            ? rows.OrderBy(r => r.Group.Order)
            : Sort(rows, r => (double?)r.Row["rate"], AiArgs.Str(args, "sort"));
        var result = Result(f, period, groupBy, previous is not null ? period.Previous : null);
        AddRows(result, ordered.Select(r => r.Row), AiArgs.Int(args, "limit") ?? 30);
        result["definition"] = "rate = (có mặt + muộn) / lượt đã điểm danh; absent_excused có đơn nghỉ được duyệt; students_absent = số học sinh có ít nhất một lượt vắng.";
        return result;
    }

    private static Func<int, DateOnly, int, string, Group> AttendanceKey(string groupBy, Dictionary<int, ScopeClass> classes, string scopeLabel) => groupBy switch
    {
        "none" => (_, _, _, _) => new Group("all", scopeLabel, 0),
        "campus" => (c, _, _, _) => new Group($"c{classes[c].CampusId}", classes[c].CampusName, 0),
        "grade" => (c, _, _, _) => new Group($"g{classes[c].Grade}", $"Khối {classes[c].Grade}", classes[c].Grade),
        "class" => (c, _, _, _) => new Group($"l{c}", classes[c].Name, 0),
        "week" => (_, d, _, _) => WeekGroup(d),
        "weekday" => (_, d, _, _) => new Group($"d{(int)d.DayOfWeek}", Weekday(d.DayOfWeek), ((int)d.DayOfWeek + 6) % 7),
        "period_slot" => (_, _, n, s) => new Group($"{s}-{n}", $"{(s == "Morning" ? "Sáng" : "Chiều")} tiết {n}", (s == "Morning" ? 0 : 100) + n),
        _ => throw ApiException.BadRequest($"Không nhóm chuyên cần theo \"{groupBy}\" được."),
    };

    private static JsonObject AttendanceRow(IReadOnlyCollection<Tally> tallies, IReadOnlyCollection<Absence> absences)
    {
        int Of(string status) => tallies.Where(t => t.Status == status).Sum(t => t.Count);
        int present = Of(AttendanceStatuses.Present), late = Of(AttendanceStatuses.Late), absent = Of(AttendanceStatuses.Absent);
        var excused = absences.Count(a => a.Excused);
        var marked = present + late + absent;
        return new JsonObject
        {
            ["rate"] = DashboardSupport.Percent(present + late, marked),
            ["unexcused_rate"] = DashboardSupport.Percent(absent - excused, marked),
            ["marked"] = marked, ["present"] = present, ["late"] = late,
            ["absent_excused"] = excused, ["absent_unexcused"] = absent - excused,
            ["students_absent"] = absences.Select(a => a.StudentId).Distinct().Count(),
        };
    }

    /// <summary>Số lượt điểm danh theo lớp, ngày, tiết, trạng thái và danh sách lượt vắng đã tách có phép theo BR-18 (cùng quy tắc màn Điểm danh).</summary>
    private async Task<(List<Tally> Tallies, List<Absence> Absences)> AttendanceAsync(List<int> classIds, AiPeriod p, int? studentId = null)
    {
        if (classIds.Count == 0) return ([], []);
        var records = db.AttendanceRecord.AsNoTracking()
            .Where(r => classIds.Contains(r.Session.ClassId) && r.Session.Status == AttendanceRules.SessionScheduled
                && r.Session.SessionDate >= p.From && r.Session.SessionDate <= p.To);
        if (studentId is not null) records = records.Where(r => r.StudentId == studentId);
        var tallies = (await records
                .GroupBy(r => new { r.Session.ClassId, r.Session.SessionDate, r.Session.PeriodSlot.PeriodNo, r.Session.PeriodSlot.SessionOfDay, r.Status })
                .Select(g => new { g.Key.ClassId, g.Key.SessionDate, g.Key.PeriodNo, g.Key.SessionOfDay, g.Key.Status, Count = g.Count() })
                .ToListAsync())
            .Select(t => new Tally(t.ClassId, t.SessionDate, t.PeriodNo, t.SessionOfDay, t.Status, t.Count)).ToList();
        var absent = await records.Where(r => r.Status == AttendanceStatuses.Absent)
            .Select(r => new { r.StudentId, r.Session.ClassId, r.Session.SessionDate, r.Session.PeriodSlot.PeriodNo, r.Session.PeriodSlot.SessionOfDay })
            .ToListAsync();
        var students = absent.Select(a => a.StudentId).Distinct().ToList();
        var leaves = students.Count == 0 ? [] : await db.LeaveRequest.AsNoTracking()
            .Where(l => l.Status == AttendanceRules.LeaveApproved && students.Contains(l.StudentId) && l.FromDate <= p.To && l.ToDate >= p.From)
            .Select(l => new ApprovedLeave(l.StudentId, l.FromDate, l.ToDate, l.SessionOfDay, l.SlotIds)).ToListAsync();
        return (tallies, absent.Select(a => new Absence(a.StudentId, a.ClassId, a.SessionDate, a.PeriodNo, a.SessionOfDay,
            AttendanceRules.IsExcused(a.StudentId, a.SessionDate, a.SessionOfDay, a.PeriodNo, leaves))).ToList());
    }

    /// <summary>Số lượt đã điểm danh và số lượt có mặt (kể cả muộn) của từng học sinh.</summary>
    private async Task<Dictionary<int, (int Marked, int Attended)>> StudentAttendanceAsync(List<int> classIds, AiPeriod p)
    {
        if (classIds.Count == 0) return [];
        return (await db.AttendanceRecord.AsNoTracking()
                .Where(r => classIds.Contains(r.Session.ClassId) && r.Session.Status == AttendanceRules.SessionScheduled
                    && r.Session.SessionDate >= p.From && r.Session.SessionDate <= p.To)
                .GroupBy(r => r.StudentId)
                .Select(g => new { g.Key, Marked = g.Count(), Attended = g.Count(r => r.Status != AttendanceStatuses.Absent) })
                .ToListAsync())
            .ToDictionary(x => x.Key, x => (x.Marked, x.Attended));
    }

    private async Task<JsonObject> AbsentStudentsAsync(JsonElement args)
    {
        var f = AiArgs.Filter(args);
        var classes = scope.ClassesFor(f);
        var byId = classes.ToDictionary(c => c.Id);
        var kind = AiArgs.Str(args, "kind") ?? "most_absent";
        var limit = Math.Clamp(AiArgs.Int(args, "limit") ?? MaxPeople, 1, MaxPeople);

        // Chỉ liệt kê em đang học ở lớp trong phạm vi, hiện theo lớp hiện tại.
        var current = await CurrentClassesAsync();
        var classOf = scope.Classes.ToDictionary(c => c.Id);

        if (kind == "streak")
        {
            var streaks = (await AttendanceSupport.StreaksAsync(db, byId.Keys.ToList(), scope.Today)).Where(s => current.ContainsKey(s.Key)).ToList();
            var result = Result(f, null, "student", null);
            result["kind"] = "streak";
            result["definition"] = $"Vắng mọi tiết đã điểm danh trong ngày, từ {AttendanceSupport.StreakDays} ngày học liên tiếp tính tới hôm qua.";
            result["total_students"] = streaks.Count;
            var rows = streaks.OrderByDescending(s => s.Value.Days).ThenBy(s => classOf[current[s.Key]].Name).ThenBy(s => s.Key).ToList();
            AddRows(result, rows.Select(s => StudentRow(s.Key, classOf[current[s.Key]], new JsonObject { ["streak_days"] = s.Value.Days })), limit);
            AddTies(result, rows.Select(s => (double?)s.Value.Days).ToList(), limit);
            return result;
        }
        if (kind is not ("most_absent" or "unexcused")) throw ApiException.BadRequest($"Không có loại \"{kind}\".");

        var period = scope.PeriodFor(args);
        var (_, absences) = await AttendanceAsync(byId.Keys.ToList(), period);
        var marked = await StudentAttendanceAsync(byId.Keys.ToList(), period);
        var perStudent = absences.Where(a => current.ContainsKey(a.StudentId)).GroupBy(a => a.StudentId).Select(g =>
        {
            var m = marked.GetValueOrDefault(g.Key);
            return new
            {
                StudentId = g.Key, Sessions = g.Count(), Excused = g.Count(a => a.Excused),
                Days = g.Select(a => a.Date).Distinct().Count(), Rate = DashboardSupport.Percent(m.Attended, m.Marked),
            };
        }).Where(s => kind != "unexcused" || s.Sessions > s.Excused).ToList();
        Func<int, int, double> key = kind == "unexcused" ? (sessions, excused) => sessions - excused : (sessions, _) => sessions;
        var picked = perStudent.OrderByDescending(s => key(s.Sessions, s.Excused))
            .ThenByDescending(s => kind == "unexcused" ? s.Sessions : s.Sessions - s.Excused).ThenBy(s => s.StudentId).ToList();
        var res = Result(f, period, "student", null);
        res["kind"] = kind;
        res["total_students"] = perStudent.Count;
        AddRows(res, picked.Select(s => StudentRow(s.StudentId, classOf[current[s.StudentId]], new JsonObject
        {
            ["absent_sessions"] = s.Sessions, ["absent_excused"] = s.Excused, ["absent_unexcused"] = s.Sessions - s.Excused,
            ["absent_days"] = s.Days, ["attendance_rate"] = s.Rate,
        })), limit);
        AddTies(res, picked.Select(s => (double?)key(s.Sessions, s.Excused)).ToList(), limit);
        return res;
    }

    /// <summary>Lớp hiện tại (trong phạm vi) của các em đang học.</summary>
    private async Task<Dictionary<int, int>> CurrentClassesAsync()
    {
        var classIds = scope.Classes.Select(c => c.Id).ToList();
        return (await db.Enrollment.AsNoTracking().Where(EnrollmentRules.ActiveAt(scope.Today))
                .Where(e => classIds.Contains(e.ClassId)).Select(e => new { e.StudentId, e.ClassId }).ToListAsync())
            .GroupBy(e => e.StudentId).ToDictionary(g => g.Key, g => g.First().ClassId);
    }

    // ---------------------------------------------------------------- Bài tập rèn luyện

    private sealed record Hw(int Id, int ClassId, int SubjectId, int AssignedBy, DateTime OpenAt, DateTime DueAt, int Recipients, int Submitted, decimal ScoreSum, int Scored);

    /// <summary>Giờ cuối của kỳ tính tới hiện tại (kỳ chứa hôm nay thì dừng ở bây giờ).</summary>
    private DateTime Until(AiPeriod p) => p.ToEnd < scope.Now ? p.ToEnd : scope.Now;

    /// <summary>Bài rèn luyện đã hết hạn nộp trong kỳ (tỷ lệ nộp chỉ tính bài đã hết hạn, như màn Tình hình).</summary>
    private async Task<List<Hw>> ClosedAssignmentsAsync(List<int> classIds, List<int>? subjectIds, AiPeriod p, int? recipientId = null)
    {
        if (classIds.Count == 0) return [];
        var until = Until(p);
        var q = db.Assignment.AsNoTracking().Where(a => classIds.Contains(a.ClassId) && a.DueAt >= p.FromTime && a.DueAt <= until);
        if (subjectIds is not null) q = q.Where(a => subjectIds.Contains(a.SubjectId));
        if (recipientId is not null) q = q.Where(a => a.AssignmentRecipient.Any(r => r.StudentId == recipientId));
        return await HwAsync(q);
    }

    /// <summary>Bài cho tool bài tập: bài hết hạn trong kỳ (để tính tỷ lệ nộp) và bài giao trong kỳ (kể cả chưa tới hạn); lọc thêm người giao nếu có.</summary>
    private async Task<List<Hw>> PracticeItemsAsync(List<int> classIds, List<int>? subjectIds, AiPeriod p, int? teacherId)
    {
        if (classIds.Count == 0) return [];
        var until = Until(p);
        var q = db.Assignment.AsNoTracking().Where(a => classIds.Contains(a.ClassId)
            && ((a.DueAt >= p.FromTime && a.DueAt <= until) || (a.OpenAt >= p.FromTime && a.OpenAt <= until)));
        if (subjectIds is not null) q = q.Where(a => subjectIds.Contains(a.SubjectId));
        if (teacherId is not null) q = q.Where(a => a.AssignedByPersonId == teacherId);
        return await HwAsync(q);
    }

    private static async Task<List<Hw>> HwAsync(IQueryable<Assignment> q) =>
        (await q.Select(a => new
            {
                a.AssignmentId, a.ClassId, a.SubjectId, a.AssignedByPersonId, a.OpenAt, a.DueAt, Recipients = a.AssignmentRecipient.Count,
                Submitted = a.Submission.Count(s => s.SubmittedAt != null),
                ScoreSum = a.Submission.Where(s => s.SubmittedAt != null).Sum(s => s.Score) ?? 0m,
                Scored = a.Submission.Count(s => s.SubmittedAt != null && s.Score != null),
            }).ToListAsync())
        .Select(a => new Hw(a.AssignmentId, a.ClassId, a.SubjectId, a.AssignedByPersonId, a.OpenAt, a.DueAt, a.Recipients, a.Submitted, a.ScoreSum, a.Scored)).ToList();

    private static JsonObject PracticeRow(IReadOnlyCollection<Hw> items) => new()
    {
        ["assignments"] = items.Count,
        ["recipients"] = items.Sum(a => a.Recipients),
        ["submitted"] = items.Sum(a => a.Submitted),
        ["submission_rate"] = DashboardSupport.Percent(items.Sum(a => a.Submitted), items.Sum(a => a.Recipients)),
        ["avg_score"] = Avg(items.Sum(a => a.ScoreSum), items.Sum(a => a.Scored)),
    };

    private async Task<JsonObject> PracticeSummaryAsync(JsonElement args)
    {
        var f = AiArgs.Filter(args);
        var classes = scope.ClassesFor(f);
        var byId = classes.ToDictionary(c => c.Id);
        var subjectIds = scope.SubjectIdsFor(f);
        var period = scope.PeriodFor(args);
        var groupBy = AiArgs.Str(args, "group_by") ?? "none";
        var subjects = scope.Subjects.ToDictionary(s => s.Id);
        var items = await PracticeItemsAsync(byId.Keys.ToList(), subjectIds, period, f.TeacherId);
        // Tỷ lệ nộp, điểm chỉ tính bài đã hết hạn trong kỳ; "đã giao" đếm bài mở trong kỳ, kể cả bài chưa tới hạn.
        List<Hw> Closed(AiPeriod p, IEnumerable<Hw> list) => list.Where(a => a.DueAt >= p.FromTime && a.DueAt <= Until(p)).ToList();

        if (groupBy is "difficulty" or "topic")
        {
            var ids = Closed(period, items).Select(a => a.Id).ToList();
            var answers = db.SubmissionAnswer.AsNoTracking()
                .Where(a => ids.Contains(a.Submission.AssignmentId) && a.Submission.SubmittedAt != null && a.IsCorrect != null);
            var result = Result(f, period, groupBy, null, subjects: true);
            result["definition"] = "correct_rate = số câu trả lời đúng / số câu đã trả lời, trên các bài đã hết hạn trong kỳ.";
            if (groupBy == "difficulty")
            {
                var rows = await answers.GroupBy(a => a.AssignmentQuestion.Question.Difficulty)
                    .Select(g => new { g.Key, Answers = g.Count(), Correct = g.Count(x => x.IsCorrect == true) }).ToListAsync();
                AddRows(result, rows.OrderBy(r => Difficulties.Rank(r.Key)).Select(r => new JsonObject
                {
                    ["label"] = Difficulties.Label(r.Key), ["answers"] = r.Answers, ["correct_rate"] = DashboardSupport.Percent(r.Correct, r.Answers),
                }), 10);
            }
            else
            {
                var rows = await answers.GroupBy(a => new { a.AssignmentQuestion.Question.SubjectId, a.AssignmentQuestion.Question.Topic })
                    .Select(g => new { g.Key.SubjectId, g.Key.Topic, Answers = g.Count(), Correct = g.Count(x => x.IsCorrect == true) }).ToListAsync();
                // Chủ đề quá ít câu trả lời thì tỷ lệ không có ý nghĩa.
                var sorted = Sort(rows.Where(r => r.Answers >= 5 && !string.IsNullOrWhiteSpace(r.Topic)).Select(r => new JsonObject
                {
                    ["label"] = $"{(subjects.TryGetValue(r.SubjectId, out var s) ? s.Name : "Môn khác")} · {r.Topic}",
                    ["answers"] = r.Answers, ["correct_rate"] = DashboardSupport.Percent(r.Correct, r.Answers),
                }), r => (double?)r["correct_rate"], AiArgs.Str(args, "sort"));
                AddRows(result, sorted, AiArgs.Int(args, "limit") ?? 15);
            }
            return result;
        }

        var keyOf = PracticeKey(groupBy, byId, subjects, scope.Describe(f, subjects: true));
        Dictionary<string, (Group Group, JsonObject Row)> Measure(AiPeriod p, IEnumerable<Hw> list) =>
            list.GroupBy(keyOf).ToDictionary(g => g.Key.Key, g =>
            {
                var given = g.Where(a => a.OpenAt >= p.FromTime && a.OpenAt <= Until(p)).ToList();
                var row = new JsonObject { ["given"] = given.Count, ["open_now"] = given.Count(a => a.DueAt > scope.Now) };
                foreach (var kv in PracticeRow(Closed(p, g))) row[kv.Key] = kv.Value?.DeepClone();
                return (g.Key, row);
            });

        var current = Measure(period, items);
        Dictionary<string, (Group Group, JsonObject Row)>? previous = null;
        if (AiArgs.Bool(args, "compare_previous"))
            previous = Measure(period.Previous, await PracticeItemsAsync(byId.Keys.ToList(), subjectIds, period.Previous, f.TeacherId));
        var rowsOut = current.Values.Select(x =>
        {
            var row = new JsonObject { ["label"] = x.Group.Label };
            foreach (var kv in x.Row) row[kv.Key] = kv.Value?.DeepClone();
            if (previous is not null)
            {
                var prev = previous.TryGetValue(x.Group.Key, out var p) ? p.Row : null;
                row["previous_given"] = (int?)prev?["given"];
                row["previous_submission_rate"] = (double?)prev?["submission_rate"];
                row["previous_avg_score"] = (double?)prev?["avg_score"];
                row["submission_rate_change"] = Change((double?)x.Row["submission_rate"], (double?)prev?["submission_rate"]);
                row["avg_score_change"] = Change((double?)x.Row["avg_score"], (double?)prev?["avg_score"], 2);
            }
            return (x.Group, Row: row);
        }).ToList();
        var metric = AiArgs.Str(args, "metric") is "avg_score" or "given" ? AiArgs.Str(args, "metric")! : "submission_rate";
        // given là số nguyên: đọc bằng (int?) rồi đổi, ép thẳng JsonValue<int> sang double sẽ lỗi.
        Func<JsonObject, double?> sortKey = metric == "given" ? r => (int?)r["given"] : r => (double?)r[metric];
        var ordered = groupBy == "week" ? rowsOut.OrderBy(r => r.Group.Order) : Sort(rowsOut, r => sortKey(r.Row), AiArgs.Str(args, "sort"));
        var res = Result(f, period, groupBy, previous is not null ? period.Previous : null, subjects: true);
        res["definition"] = "given = số bài giao (mở) trong kỳ, kể cả bài chưa tới hạn; open_now = trong số đó, bài còn đang mở. "
            + "assignments, submission_rate, avg_score chỉ tính bài đã hết hạn nộp trong kỳ: submission_rate = bài đã nộp / lượt được giao; avg_score là điểm trung bình (thang 10) của bài đã nộp. "
            + "Lọc hay nhóm theo giáo viên là theo người giao bài.";
        AddRows(res, ordered.Select(r => r.Row), AiArgs.Int(args, "limit") ?? 30);
        return res;
    }

    private static Func<Hw, Group> PracticeKey(string groupBy, Dictionary<int, ScopeClass> classes, Dictionary<int, ScopeSubject> subjects, string scopeLabel) => groupBy switch
    {
        "none" => _ => new Group("all", scopeLabel, 0),
        "campus" => a => new Group($"c{classes[a.ClassId].CampusId}", classes[a.ClassId].CampusName, 0),
        "grade" => a => new Group($"g{classes[a.ClassId].Grade}", $"Khối {classes[a.ClassId].Grade}", classes[a.ClassId].Grade),
        "class" => a => new Group($"l{a.ClassId}", classes[a.ClassId].Name, 0),
        "subject" => a => new Group($"s{a.SubjectId}", subjects.TryGetValue(a.SubjectId, out var s) ? s.Name : "Môn khác", 0),
        "teacher" => a => new Group($"t{a.AssignedBy}", AiCodes.Teacher(a.AssignedBy), 0),
        "week" => a => WeekGroup(DateOnly.FromDateTime(a.DueAt)),
        _ => throw ApiException.BadRequest($"Không nhóm bài tập theo \"{groupBy}\" được."),
    };

    // ---------------------------------------------------------------- Học sinh

    private sealed record StudentStats(int StudentId, ScopeClass Class, int Assigned, int Submitted, double? AvgScore, int Marked, double? AttendanceRate)
    {
        public double? SubmissionRate => DashboardSupport.Percent(Submitted, Assigned);
    }

    private async Task<List<StudentStats>> StudentStatsAsync(List<ScopeClass> classes, List<int>? subjectIds, AiPeriod p)
    {
        var byId = classes.ToDictionary(c => c.Id);
        var classIds = byId.Keys.ToList();
        var enrolled = await db.Enrollment.AsNoTracking()
            .Where(EnrollmentRules.ActiveAt(scope.Today)).Where(e => classIds.Contains(e.ClassId))
            .Select(e => new { e.StudentId, e.ClassId }).ToListAsync();
        var hw = (await ClosedAssignmentsAsync(classIds, subjectIds, p)).Select(a => a.Id).ToList();
        var assigned = hw.Count == 0 ? [] : await db.AssignmentRecipient.AsNoTracking().Where(r => hw.Contains(r.AssignmentId))
            .GroupBy(r => r.StudentId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var submitted = hw.Count == 0 ? [] : await db.Submission.AsNoTracking().Where(s => hw.Contains(s.AssignmentId) && s.SubmittedAt != null)
            .GroupBy(s => s.StudentId).Select(g => new { g.Key, Count = g.Count(), Avg = g.Average(s => s.Score) })
            .ToDictionaryAsync(x => x.Key, x => (x.Count, x.Avg));
        var attendance = await StudentAttendanceAsync(classIds, p);
        return enrolled.GroupBy(e => e.StudentId).Select(g =>
        {
            var id = g.Key;
            var sub = submitted.GetValueOrDefault(id);
            var att = attendance.GetValueOrDefault(id);
            return new StudentStats(id, byId[g.First().ClassId], assigned.GetValueOrDefault(id), sub.Count,
                sub.Avg is null ? null : Math.Round((double)sub.Avg.Value, 2), att.Marked, DashboardSupport.Percent(att.Attended, att.Marked));
        }).ToList();
    }

    private async Task<JsonObject> StudentRankingAsync(JsonElement args)
    {
        var f = AiArgs.Filter(args);
        var classes = scope.ClassesFor(f);
        var period = scope.PeriodFor(args);
        var metric = AiArgs.Str(args, "metric") ?? "avg_score";
        var bottom = AiArgs.Str(args, "order") == "bottom";
        var minRate = AiArgs.Number(args, "min_submission_rate");
        var minScore = AiArgs.Number(args, "min_avg_score");
        var minAttendance = AiArgs.Number(args, "min_attendance_rate");
        var limit = Math.Clamp(AiArgs.Int(args, "limit") ?? MaxPeople, 1, MaxPeople);
        var stats = await StudentStatsAsync(classes, scope.SubjectIdsFor(f), period);

        // Chuyên cần chỉ xếp em được điểm danh từ một nửa số tiết thường gặp (trung vị) trở lên, tránh em mới vào lớp vắng một tiết đã đứng cuối.
        var markedCounts = stats.Where(s => s.Marked > 0).Select(s => s.Marked).Order().ToList();
        var minMarked = markedCounts.Count == 0 ? 1 : Math.Max(1, (markedCounts[markedCounts.Count / 2] + 1) / 2);
        Func<StudentStats, double?> value = metric switch
        {
            "avg_score" => s => s.Submitted >= MinSubmissions ? s.AvgScore : null,
            "submission_rate" => s => s.Assigned >= MinSubmissions ? s.SubmissionRate : null,
            "attendance_rate" => s => s.Marked >= minMarked ? s.AttendanceRate : null,
            _ => throw ApiException.BadRequest($"Không xếp hạng theo \"{metric}\" được."),
        };
        var eligible = stats.Where(s => value(s) is not null
            && (minRate is null || (s.Assigned >= MinSubmissions && s.SubmissionRate >= minRate))
            && (minScore is null || (s.Submitted >= MinSubmissions && s.AvgScore >= minScore))
            && (minAttendance is null || (s.Marked >= minMarked && s.AttendanceRate >= minAttendance)))
            .ToList();
        var ordered = (bottom ? eligible.OrderBy(s => value(s)) : eligible.OrderByDescending(s => value(s))).ThenBy(s => s.StudentId).ToList();
        var result = Result(f, period, "student", null, subjects: true);
        result["metric"] = metric;
        result["order"] = bottom ? "bottom" : "top";
        result["students_in_scope"] = stats.Count;
        result["eligible_students"] = eligible.Count;
        result["rule"] = $"Điểm trung bình chỉ xếp học sinh đã nộp từ {MinSubmissions} bài; tỷ lệ nộp chỉ xếp học sinh được giao từ {MinSubmissions} bài; chỉ tính bài đã hết hạn trong kỳ; "
            + $"chuyên cần chỉ xếp học sinh được điểm danh từ {minMarked} tiết. Cùng số thì cùng rank (đồng hạng); same_as_last_not_shown là số em cùng số với dòng cuối nhưng không hiện.";
        var ranks = new List<int>();
        for (var i = 0; i < ordered.Count; i++) ranks.Add(i > 0 && value(ordered[i]) == value(ordered[i - 1]) ? ranks[i - 1] : i + 1);
        AddRows(result, ordered.Select((s, i) => StudentRow(s.StudentId, s.Class, new JsonObject
        {
            ["rank"] = ranks[i], ["avg_score"] = s.AvgScore, ["submitted"] = s.Submitted, ["assigned"] = s.Assigned,
            ["submission_rate"] = s.SubmissionRate, ["attendance_rate"] = s.AttendanceRate,
        })), limit);
        AddTies(result, ordered.Select(value).ToList(), limit);
        return result;
    }

    private async Task<JsonObject> StudentProfileAsync(JsonElement args)
    {
        var studentId = AiArgs.Person(args, "student", "HS") ?? throw ApiException.BadRequest("Cần mã học sinh dạng HS#số.");
        var period = scope.PeriodFor(args);
        var scopeIds = scope.Classes.Select(c => c.Id).ToList();
        var classId = await db.Enrollment.AsNoTracking().Where(EnrollmentRules.ActiveAt(scope.Today))
            .Where(e => e.StudentId == studentId && scopeIds.Contains(e.ClassId)).Select(e => (int?)e.ClassId).FirstOrDefaultAsync()
            ?? throw ApiException.Forbidden("Học sinh này không thuộc phạm vi được xem.");
        var cls = scope.Classes.First(c => c.Id == classId);
        var gender = await db.Person.AsNoTracking().Where(p => p.PersonId == studentId).Select(p => p.Gender).FirstOrDefaultAsync();

        // Số của em lấy theo chính em ở mọi lớp trong phạm vi (em chuyển lớp giữa kỳ vẫn đủ phần ở lớp cũ); số của lớp để so là lớp hiện tại.
        var (mine, myAbsences) = await AttendanceAsync(scopeIds, period, studentId);
        var (classTallies, classAbsences) = await AttendanceAsync([classId], period);
        var streak = (await AttendanceSupport.StreaksAsync(db, [classId], scope.Today)).TryGetValue(studentId, out var st) ? st.Days : 0;

        var hw = await ClosedAssignmentsAsync([classId], null, period);
        var myHw = await ClosedAssignmentsAsync(scopeIds, null, period, recipientId: studentId);
        var given = myHw.Select(a => a.Id).ToList();
        var done = await db.Submission.AsNoTracking().Where(s => s.StudentId == studentId && given.Contains(s.AssignmentId) && s.SubmittedAt != null)
            .Select(s => new { s.AssignmentId, s.Score }).ToListAsync();
        var hwById = myHw.ToDictionary(a => a.Id);
        var subjects = scope.Subjects.ToDictionary(s => s.Id);

        var attendance = AttendanceRow(mine, myAbsences);
        attendance.Remove("students_absent");
        attendance["absent_days"] = myAbsences.Select(a => a.Date).Distinct().Count();
        attendance["streak_days"] = streak;
        attendance["class_rate"] = (double?)AttendanceRow(classTallies, classAbsences)["rate"];

        var scored = done.Where(d => d.Score is not null).ToList();
        var practice = new JsonObject
        {
            ["assigned"] = given.Count, ["submitted"] = done.Count,
            ["submission_rate"] = DashboardSupport.Percent(done.Count, given.Count),
            ["avg_score"] = Avg(scored.Sum(d => d.Score!.Value), scored.Count),
            ["class_submission_rate"] = DashboardSupport.Percent(hw.Sum(a => a.Submitted), hw.Sum(a => a.Recipients)),
            ["class_avg_score"] = Avg(hw.Sum(a => a.ScoreSum), hw.Sum(a => a.Scored)),
            ["by_subject"] = new JsonArray(given.GroupBy(id => hwById[id].SubjectId).Select(g =>
            {
                var subDone = done.Where(d => hwById[d.AssignmentId].SubjectId == g.Key).ToList();
                var subScored = subDone.Where(d => d.Score is not null).ToList();
                return (JsonNode)new JsonObject
                {
                    ["subject"] = subjects.TryGetValue(g.Key, out var s) ? s.Name : "Môn khác",
                    ["assigned"] = g.Count(), ["submitted"] = subDone.Count,
                    ["avg_score"] = Avg(subScored.Sum(d => d.Score!.Value), subScored.Count),
                };
            }).ToArray()),
        };

        // Xu hướng theo tuần trong kỳ (tối đa 10 tuần gần nhất).
        var weeks = Enumerable.Range(0, 10).Select(i => DashboardSupport.WeekStart(period.To).AddDays(-7 * i))
            .Where(w => w.AddDays(6) >= period.From).Reverse().ToList();
        var weekly = new JsonArray(weeks.Select(w =>
        {
            var att = AttendanceRow(mine.Where(t => DashboardSupport.WeekStart(t.Date) == w).ToList(), myAbsences.Where(a => DashboardSupport.WeekStart(a.Date) == w).ToList());
            var ws = scored.Where(d => DashboardSupport.WeekStart(DateOnly.FromDateTime(hwById[d.AssignmentId].DueAt)) == w).ToList();
            return (JsonNode)new JsonObject { ["label"] = $"Tuần {w:dd/MM}", ["attendance_rate"] = (double?)att["rate"], ["avg_score"] = Avg(ws.Sum(d => d.Score!.Value), ws.Count) };
        }).ToArray());

        var result = new JsonObject
        {
            ["student"] = StudentRow(studentId, cls, new JsonObject { ["gender"] = gender switch { "Male" => "Nam", "Female" => "Nữ", _ => null } }),
            ["period"] = period.ToJson(),
            ["attendance"] = attendance,
            ["practice"] = practice,
            ["group_by"] = "week",
            ["rows"] = weekly,
        };
        return result;
    }

    private static JsonObject StudentRow(int studentId, ScopeClass cls, JsonObject extra)
    {
        var row = new JsonObject
        {
            ["label"] = $"{AiCodes.Student(studentId)} ({cls.Name})",
            ["code"] = AiCodes.Student(studentId), ["class"] = cls.Name, ["grade"] = cls.Grade, ["campus"] = cls.CampusName,
        };
        foreach (var kv in extra.ToList()) { extra.Remove(kv.Key); row[kv.Key] = kv.Value; }
        return row;
    }

    // ---------------------------------------------------------------- Giáo viên

    private sealed record ScopeTeacher(int Id, int? DepartmentId);

    /// <summary>Giáo viên trong phạm vi: theo phân hiệu chính (mục 5.1), lọc thêm tổ và mã GV nếu có.</summary>
    private async Task<List<ScopeTeacher>> TeachersAsync(AiFilter f)
    {
        var campusIds = scope.CampusIdsFor(f);
        var q = db.StaffProfile.AsNoTracking().Where(s => s.IsActive && campusIds.Contains(s.PrimaryCampusId)
            && s.Staff.Account != null && s.Staff.Account.AccountType == AccountTypes.Teacher);
        if (f.DepartmentIds.Count > 0) q = q.Where(s => s.DepartmentId != null && f.DepartmentIds.Contains(s.DepartmentId.Value));
        var teachers = await q.Select(s => new ScopeTeacher(s.StaffId, s.DepartmentId)).ToListAsync();
        if (f.TeacherId is { } id)
        {
            teachers = teachers.Where(t => t.Id == id).ToList();
            if (teachers.Count == 0) throw ApiException.Forbidden("Giáo viên này không thuộc phạm vi được xem.");
        }
        return teachers;
    }

    private string DepartmentName(int? id) => scope.Departments.FirstOrDefault(d => d.Id == id)?.Name ?? "Chưa thuộc tổ";

    private async Task<JsonObject> TeacherAbsenceAsync(JsonElement args)
    {
        var f = AiArgs.Filter(args);
        var teachers = await TeachersAsync(f);
        var classIds = scope.ClassesFor(f with { ClassIds = [] }).Select(c => c.Id).ToList();
        var period = scope.PeriodFor(args);
        var groupBy = AiArgs.Str(args, "group_by") ?? "none";

        async Task<Dictionary<string, (Group Group, JsonObject Row)>> Measure(AiPeriod p)
        {
            var days = await OperationsSupport.AbsentDaysAsync(db, teachers.Select(t => t.Id).ToList(), p.From, p.To);
            var all = await OperationsSupport.AffectedAsync(db, classIds, p.From, p.To);
            // Tiết của giáo viên vắng tính cho tổ của người vắng (giáo viên có thể dạy chéo môn tổ khác); tiết hủy không gắn ai vắng thì theo tổ của môn.
            var absentIds = all.Where(a => a.AbsentTeacherId is not null).Select(a => a.AbsentTeacherId!.Value).Distinct().ToList();
            var teacherDept = absentIds.Count == 0 ? [] : await db.StaffProfile.AsNoTracking().Where(s => absentIds.Contains(s.StaffId))
                .ToDictionaryAsync(s => s.StaffId, s => s.DepartmentId);
            int? DeptOf(AffectedSession a) => a.AbsentTeacherId is { } t ? teacherDept.GetValueOrDefault(t) : a.DepartmentId;
            var affected = all
                .Where(a => (f.DepartmentIds.Count == 0 || (DeptOf(a) is { } d && f.DepartmentIds.Contains(d))) && (f.TeacherId is null || a.AbsentTeacherId == f.TeacherId))
                .ToList();
            JsonObject Row(IReadOnlyCollection<ScopeTeacher> members, IReadOnlyCollection<AffectedSession> sessions, bool countTeachers = true)
            {
                var row = new JsonObject();
                if (countTeachers) row["teachers"] = members.Count;
                row["absent_teachers"] = members.Count(m => days.GetValueOrDefault(m.Id) > 0);
                row["absent_days"] = members.Sum(m => days.GetValueOrDefault(m.Id));
                row["affected_sessions"] = sessions.Count;
                row["substituted"] = sessions.Count(s => s.Row.Outcome == OperationsSupport.Outcomes.Substituted);
                row["cancelled"] = sessions.Count(s => s.Row.Outcome == OperationsSupport.Outcomes.Cancelled);
                row["pending"] = sessions.Count(s => s.Row.Outcome == OperationsSupport.Outcomes.Pending);
                return row;
            }
            return groupBy switch
            {
                "none" => new() { ["all"] = (new Group("all", scope.Describe(f with { Grades = [], ClassIds = [] }, subjects: true), 0), Row(teachers, affected)) },
                "department" => teachers.Select(t => t.DepartmentId).Concat(affected.Select(DeptOf)).Distinct()
                    .ToDictionary(d => $"d{d}", d => (new Group($"d{d}", DepartmentName(d), 0),
                        Row(teachers.Where(t => t.DepartmentId == d).ToList(), affected.Where(a => DeptOf(a) == d).ToList()))),
                "teacher" => teachers.ToDictionary(t => $"t{t.Id}", t =>
                {
                    var row = Row([t], affected.Where(a => a.AbsentTeacherId == t.Id).ToList(), countTeachers: false);
                    row.Remove("absent_teachers");
                    row["code"] = AiCodes.Teacher(t.Id);
                    row["department"] = DepartmentName(t.DepartmentId);
                    return (new Group($"t{t.Id}", AiCodes.Teacher(t.Id), 0), row);
                }),
                "week" => Enumerable.Range(0, (p.To.DayNumber - DashboardSupport.WeekStart(p.From).DayNumber) / 7 + 1)
                    .Select(i => DashboardSupport.WeekStart(p.From).AddDays(7 * i))
                    .ToDictionary(w => w.ToString("yyyy-MM-dd"), w =>
                    {
                        var g = WeekGroup(w);
                        var inWeek = affected.Where(a => DashboardSupport.WeekStart(a.Row.Date) == w).ToList();
                        var row = Row([], inWeek, countTeachers: false);
                        row["absent_teachers"] = inWeek.Where(a => a.AbsentTeacherId is not null).Select(a => a.AbsentTeacherId).Distinct().Count();
                        row.Remove("absent_days");
                        return (g, row);
                    }),
                _ => throw ApiException.BadRequest($"Không nhóm giáo viên vắng theo \"{groupBy}\" được."),
            };
        }

        var current = await Measure(period);
        var previous = AiArgs.Bool(args, "compare_previous") && groupBy != "week" ? await Measure(period.Previous) : null;
        var rows = current.Values.Select(x =>
        {
            var row = new JsonObject { ["label"] = x.Group.Label };
            foreach (var kv in x.Row) row[kv.Key] = kv.Value?.DeepClone();
            if (previous is not null)
            {
                var prev = previous.TryGetValue(x.Group.Key, out var p) ? p.Row : null;
                row["previous_affected_sessions"] = (int?)prev?["affected_sessions"];
                row["previous_absent_days"] = (int?)prev?["absent_days"];
            }
            return (x.Group, Row: row);
        }).ToList();

        IEnumerable<JsonObject> ordered = groupBy switch
        {
            "week" => rows.OrderBy(r => r.Group.Order).Select(r => r.Row),
            "teacher" => rows.Select(r => r.Row).Where(r => (int)r["absent_days"]! > 0 || (int)r["affected_sessions"]! > 0)
                .OrderByDescending(r => (int)r["absent_days"]!).ThenByDescending(r => (int)r["affected_sessions"]!),
            _ => rows.Select(r => r.Row).OrderByDescending(r => (int)r["affected_sessions"]!),
        };
        var result = Result(f with { Grades = [], ClassIds = [] }, period, groupBy, previous is not null ? period.Previous : null, subjects: true);
        result["definition"] = "absent_days = số ngày vắng đã khai báo (trừ Chủ nhật); affected_sessions = tiết của giáo viên vắng (dạy thay, hủy, chờ xử lý) và tiết bị hủy ở các lớp trong phạm vi, không tính tiết hủy vì ngày nghỉ; "
            + "theo tổ thì tiết của giáo viên vắng tính cho tổ của người vắng, tiết hủy không có ai vắng tính cho tổ của môn; pending = chưa có người dạy; pending_now = tiết sắp tới chưa có người dạy, cùng bộ lọc giáo viên, tổ.";
        // Tiết sắp tới còn chờ người dạy, lọc cùng giáo viên, tổ như phần trên.
        var pending = db.TeachingSession.AsNoTracking().Where(SessionLockRules.NotEnded(clock))
            .Where(s => s.Status == OperationsSupport.Outcomes.Pending && classIds.Contains(s.ClassId));
        if (f.TeacherId is { } teacherId) pending = pending.Where(s => s.TeacherAbsence != null && s.TeacherAbsence.TeacherId == teacherId);
        if (f.DepartmentIds.Count > 0)
        {
            var depts = f.DepartmentIds.ToList();
            pending = pending.Where(s => s.TeacherAbsence != null
                ? s.TeacherAbsence.Teacher.DepartmentId != null && depts.Contains(s.TeacherAbsence.Teacher.DepartmentId.Value)
                : depts.Contains(s.Subject.DepartmentId));
        }
        result["pending_now"] = classIds.Count == 0 ? 0 : await pending.CountAsync();
        AddRows(result, ordered, groupBy == "teacher" ? Math.Clamp(AiArgs.Int(args, "limit") ?? MaxPeople, 1, MaxPeople) : AiArgs.Int(args, "limit") ?? 30);
        return result;
    }

    private async Task<JsonObject> TeachingLoadAsync(JsonElement args)
    {
        var f = AiArgs.Filter(args);
        var teachers = (await TeachersAsync(f)).ToDictionary(t => t.Id);
        var classIds = scope.ClassesFor(f with { Grades = [], ClassIds = [] }).Select(c => c.Id).ToHashSet();
        var groupBy = AiArgs.Str(args, "group_by") ?? "teacher";
        var teaching = await responsibility.ListAsync(null, null);

        // Chỉ tính tiết ở các lớp trong phạm vi; giáo viên của phân hiệu khác dạy lớp trong phạm vi vẫn không hiện (theo phân hiệu chính).
        var loads = teaching.Teachers.Where(t => teachers.ContainsKey(t.TeacherId))
            .Select(t =>
            {
                var items = t.Items.Where(i => classIds.Contains(i.ClassId)).ToList();
                return new { t.TeacherId, teachers[t.TeacherId].DepartmentId, Periods = items.Sum(i => i.PeriodsPerWeek),
                    Classes = items.Select(i => i.ClassId).Distinct().Count(), Subjects = items.Select(i => i.SubjectName).Distinct().ToList() };
            })
            .Where(x => x.Periods > 0).ToList();
        var peers = loads.GroupBy(l => l.DepartmentId).ToDictionary(g => g.Key ?? 0, g => (Count: g.Count(), Average: Math.Round(g.Average(x => x.Periods), 1)));

        var result = Result(f with { Grades = [], ClassIds = [] }, null, groupBy, null, subjects: true);
        result["semester"] = teaching.SemesterName;
        result["definition"] = "periods_per_week = số tiết mỗi tuần theo thời khóa biểu đã phát hành của học kỳ hiện tại, chỉ tính lớp trong phạm vi. "
            + $"department_average = trung bình của department_teachers giáo viên cùng tổ trong phạm vi đang hỏi (không phải cả tổ toàn trường); để trống khi tổ có dưới {MinDepartmentPeers} người trong phạm vi vì so sánh không có ý nghĩa.";
        if (loads.Count > 0)
        {
            // Số tổng hợp của cả phạm vi để AI không tự tính.
            result["summary"] = new JsonObject
            {
                ["teachers_with_load"] = loads.Count, ["departments"] = loads.Select(l => l.DepartmentId).Distinct().Count(),
                ["total_periods"] = loads.Sum(x => x.Periods), ["average_periods"] = Math.Round(loads.Average(x => x.Periods), 1),
                ["max_periods"] = loads.Max(x => x.Periods), ["min_periods"] = loads.Min(x => x.Periods),
            };
        }
        if (groupBy == "department")
        {
            AddRows(result, loads.GroupBy(l => l.DepartmentId).Select(g => new JsonObject
            {
                ["label"] = DepartmentName(g.Key), ["teachers"] = g.Count(), ["total_periods"] = g.Sum(x => x.Periods),
                ["average_periods"] = Math.Round(g.Average(x => x.Periods), 1), ["max_periods"] = g.Max(x => x.Periods), ["min_periods"] = g.Min(x => x.Periods),
            }).OrderByDescending(r => (double)r["average_periods"]!), 30);
            return result;
        }
        if (groupBy != "teacher") throw ApiException.BadRequest($"Không nhóm tải dạy theo \"{groupBy}\" được.");
        var bottom = AiArgs.Str(args, "order") == "bottom";
        var rows = loads.Select(l =>
        {
            var peer = peers[l.DepartmentId ?? 0];
            var comparable = peer.Count >= MinDepartmentPeers;
            return new JsonObject
            {
                ["label"] = AiCodes.Teacher(l.TeacherId), ["code"] = AiCodes.Teacher(l.TeacherId), ["department"] = DepartmentName(l.DepartmentId),
                ["periods_per_week"] = l.Periods, ["classes"] = l.Classes, ["subjects"] = string.Join(", ", l.Subjects),
                ["department_teachers"] = peer.Count,
                ["department_average"] = comparable ? peer.Average : null,
                ["above_department_average"] = comparable ? Math.Round(l.Periods - peer.Average, 1) : null,
            };
        });
        var minAbove = AiArgs.Number(args, "min_above_average");
        if (minAbove is not null) rows = rows.Where(r => (double?)r["above_department_average"] >= minAbove);
        AddRows(result, bottom ? rows.OrderBy(r => (int)r["periods_per_week"]!) : rows.OrderByDescending(r => (int)r["periods_per_week"]!),
            Math.Clamp(AiArgs.Int(args, "limit") ?? MaxPeople, 1, MaxPeople));
        return result;
    }

    // ---------------------------------------------------------------- Dùng chung

    private JsonObject Result(AiFilter f, AiPeriod? period, string groupBy, AiPeriod? previous, bool subjects = false)
    {
        var result = new JsonObject { ["scope"] = scope.Describe(f, subjects) };
        if (period is not null) result["period"] = period.ToJson();
        if (previous is not null) result["previous_period"] = previous.ToJson();
        result["group_by"] = groupBy;
        return result;
    }

    private static void AddRows(JsonObject result, IEnumerable<JsonObject> rows, int limit)
    {
        var list = rows.ToList();
        var take = Math.Clamp(limit, 1, MaxGroups);
        result["rows"] = new JsonArray(list.Take(take).Select(r => (JsonNode)r).ToArray());
        if (list.Count > take) result["rows_omitted"] = list.Count - take;
    }

    /// <summary>Số dòng không hiện mà cùng số với dòng cuối được hiện (đồng hạng ở mép danh sách).</summary>
    private static void AddTies(JsonObject result, IReadOnlyList<double?> values, int limit)
    {
        var take = Math.Clamp(limit, 1, MaxGroups);
        if (values.Count <= take) return;
        var more = values.Skip(take).Count(v => v == values[take - 1]);
        if (more > 0) result["same_as_last_not_shown"] = more;
    }

    /// <summary>Sắp xếp theo một chỉ số: mặc định thấp lên trước (đáng lo trước), "best_first" cao lên trước, "name" theo tên; nhóm chưa có số xếp cuối.</summary>
    private static IEnumerable<T> Sort<T>(IEnumerable<T> rows, Func<T, double?> metric, string? sort) => sort switch
    {
        "best_first" => rows.OrderBy(r => metric(r) is null).ThenByDescending(metric),
        "name" => rows,
        _ => rows.OrderBy(r => metric(r) is null).ThenBy(metric),
    };

    private static Group WeekGroup(DateOnly d)
    {
        var w = DashboardSupport.WeekStart(d);
        return new Group(w.ToString("yyyy-MM-dd"), $"Tuần {w:dd/MM}", w.DayNumber);
    }

    private static string Weekday(DayOfWeek d) => d switch
    {
        DayOfWeek.Monday => "Thứ Hai", DayOfWeek.Tuesday => "Thứ Ba", DayOfWeek.Wednesday => "Thứ Tư",
        DayOfWeek.Thursday => "Thứ Năm", DayOfWeek.Friday => "Thứ Sáu", DayOfWeek.Saturday => "Thứ Bảy", _ => "Chủ nhật",
    };

    private static double? Change(double? current, double? previous, int digits = 1) =>
        current is null || previous is null ? null : Math.Round(current.Value - previous.Value, digits);

    private static double? Avg(decimal sum, int count) => count == 0 ? null : Math.Round((double)(sum / count), 2);

    // ---------------------------------------------------------------- Khai báo cho AI

    public static IReadOnlyList<AiTool> Declarations() =>
    [
        new("attendance_summary",
            "Chuyên cần học sinh: tỷ lệ chuyên cần, cơ cấu có mặt/muộn/vắng có phép/vắng không phép, số học sinh có nghỉ. Dùng cho báo cáo số nghỉ, xu hướng theo tuần, so sánh lớp/khối/phân hiệu.",
            Params(Filters(), PeriodParams(), CompareParam(),
                ("group_by", Enum("Nhóm kết quả. none = một dòng tổng.", "none", "campus", "grade", "class", "week", "weekday", "period_slot")),
                ("sort", Enum("Thứ tự khi nhóm theo phân hiệu/khối/lớp: worst_first (mặc định, thấp trước), best_first, name.", "worst_first", "best_first", "name")),
                ("limit", Int("Số dòng tối đa, mặc định 30.")))),
        new("absent_students",
            $"Danh sách tối đa {MaxPeople} học sinh (mã HS#) vắng nhiều nhất, vắng không phép nhiều nhất, hoặc đang vắng liền từ {AttendanceSupport.StreakDays} ngày (streak, không cần kỳ).",
            Params(Filters(), PeriodParams(),
                ("kind", Enum("most_absent (mặc định), unexcused, streak.", "most_absent", "unexcused", "streak")),
                ("limit", Int($"Tối đa {MaxPeople}.")))),
        new("practice_summary",
            "Bài tập rèn luyện: số bài đã giao trong kỳ, bài đang mở, tỷ lệ nộp, điểm trung bình (thang 10), so với kỳ trước; lọc hoặc nhóm theo giáo viên giao bài (tiến độ giao bài); "
            + "hoặc tỷ lệ trả lời đúng theo mức nhận thức (difficulty) hay theo chủ đề (topic, chủ đề yếu trước).",
            Params(Filters(subjects: true), PeriodParams(), CompareParam(),
                ("teacher", Str("Mã giáo viên dạng GV#số: chỉ bài do giáo viên này giao.")),
                ("group_by", Enum("Nhóm kết quả. teacher = theo giáo viên giao bài.", "none", "campus", "grade", "class", "subject", "teacher", "week", "difficulty", "topic")),
                ("metric", Enum("Chỉ số dùng để sắp xếp: submission_rate (mặc định), avg_score hoặc given (số bài đã giao).", "submission_rate", "avg_score", "given")),
                ("sort", Enum("worst_first (mặc định), best_first, name.", "worst_first", "best_first", "name")),
                ("limit", Int("Số dòng tối đa, mặc định 30.")))),
        new("student_ranking",
            $"Xếp hạng tối đa {MaxPeople} học sinh (mã HS#) theo điểm trung bình bài tập, tỷ lệ nộp hoặc chuyên cần, kèm điều kiện ghép (ví dụ nộp từ 80% và điểm từ 8). Chỉ xếp em đã có từ {MinSubmissions} bài.",
            Params(Filters(subjects: true), PeriodParams(),
                ("metric", Enum("avg_score (mặc định), submission_rate, attendance_rate.", "avg_score", "submission_rate", "attendance_rate")),
                ("order", Enum("top (cao nhất, mặc định) hoặc bottom (thấp nhất).", "top", "bottom")),
                ("min_submission_rate", Num("Tỷ lệ nộp tối thiểu (%).")),
                ("min_avg_score", Num("Điểm trung bình tối thiểu (thang 10).")),
                ("min_attendance_rate", Num("Chuyên cần tối thiểu (%).")),
                ("limit", Int($"Tối đa {MaxPeople}.")))),
        new("student_profile",
            "Tình hình một học sinh: chuyên cần (kèm của lớp để so), vắng liền, bài tập theo môn, điểm trung bình, xu hướng theo tuần.",
            Params([("student", Str("Mã học sinh dạng HS#số."))], PeriodParams(), required: ["student"])),
        new("teacher_absence_summary",
            "Giáo viên vắng và tiết bị ảnh hưởng (dạy thay, hủy, chờ xử lý). pending_now là số tiết sắp tới chưa có người dạy. Nhóm theo tổ, giáo viên (mã GV#) hoặc tuần.",
            Params(TeacherFilters(), PeriodParams(), CompareParam(),
                ("group_by", Enum("Nhóm kết quả.", "none", "department", "teacher", "week")),
                ("limit", Int($"Số dòng tối đa; theo giáo viên tối đa {MaxPeople}.")))),
        new("teaching_load",
            $"Tải dạy theo thời khóa biểu học kỳ hiện tại: số tiết mỗi tuần của từng giáo viên (mã GV#), summary là số chung của cả phạm vi; so với trung bình tổ chỉ khi tổ có từ {MinDepartmentPeers} người trong phạm vi. Hoặc tổng hợp theo tổ.",
            Params(TeacherFilters(),
                ("group_by", Enum("teacher (mặc định) hoặc department.", "teacher", "department")),
                ("order", Enum("top (nhiều tiết nhất, mặc định) hoặc bottom.", "top", "bottom")),
                ("min_above_average", Num("Chỉ lấy giáo viên dạy vượt trung bình tổ ít nhất ngần này tiết (0 = mọi người vượt trung bình).")),
                ("limit", Int($"Tối đa {MaxPeople}.")))),
        new(ShowChart,
            "Vẽ một biểu đồ từ các dòng (rows) của một kết quả tool đã có. Dùng khi so sánh hoặc xếp hạng từ 3 dòng trở lên, hay xem xu hướng theo tuần. Tối đa 2 biểu đồ mỗi câu trả lời.",
            Params([
                ("result_id", Int("result_id của kết quả tool cần vẽ.")),
                ("metric", Str("Tên trường số trong rows, ví dụ rate, submission_rate, avg_score, affected_sessions, periods_per_week.")),
                ("title", Str("Tiêu đề ngắn của biểu đồ.")),
            ], required: ["result_id", "metric", "title"])),
    ];

    private static (string, JsonObject)[] Filters(bool subjects = false)
    {
        var list = new List<(string, JsonObject)>
        {
            ("campus_ids", IntList("Id phân hiệu trong danh mục.")),
            ("grade_levels", IntList("Khối 6–9.")),
            ("class_ids", IntList("Id lớp trong danh mục.")),
        };
        if (subjects)
        {
            list.Add(("subject_ids", IntList("Id môn trong danh mục.")));
            list.Add(("department_ids", IntList("Id tổ trong danh mục (lấy mọi môn của tổ).")));
        }
        return list.ToArray();
    }

    private static (string, JsonObject)[] TeacherFilters() =>
    [
        ("campus_ids", IntList("Id phân hiệu (giáo viên theo phân hiệu chính).")),
        ("department_ids", IntList("Id tổ trong danh mục.")),
        ("teacher", Str("Mã giáo viên dạng GV#số để xem riêng một người.")),
    ];

    private static (string, JsonObject)[] PeriodParams() =>
    [
        ("period", Enum("Kỳ, mặc định last_4_weeks. custom cần from và to.",
            "today", "this_week", "last_week", "last_4_weeks", "this_month", "last_month", "this_semester", "this_school_year", "custom")),
        ("from", Str("Ngày bắt đầu yyyy-MM-dd khi period = custom.")),
        ("to", Str("Ngày kết thúc yyyy-MM-dd khi period = custom.")),
    ];

    private static (string, JsonObject)[] CompareParam() => [("compare_previous", new JsonObject { ["type"] = "boolean", ["description"] = "Kèm số của kỳ liền trước cùng độ dài và mức thay đổi." })];

    private static JsonObject Params(params object[] parts) => Build(parts, required: null);

    private static JsonObject Build(object[] parts, string[]? required)
    {
        var props = new JsonObject();
        foreach (var part in parts)
        {
            if (part is ValueTuple<string, JsonObject> one) props[one.Item1] = one.Item2;
            else if (part is (string, JsonObject)[] many) foreach (var (k, v) in many) props[k] = v;
        }
        var schema = new JsonObject { ["type"] = "object", ["properties"] = props };
        if (required is not null) schema["required"] = new JsonArray(required.Select(r => (JsonNode)r).ToArray());
        return schema;
    }

    private static JsonObject Params((string, JsonObject)[] first, (string, JsonObject)[] second, string[] required) =>
        Build([first, second], required);

    private static JsonObject Params((string, JsonObject)[] only, string[] required) => Build([only], required);

    private static JsonObject Str(string d) => new() { ["type"] = "string", ["description"] = d };
    private static JsonObject Int(string d) => new() { ["type"] = "integer", ["description"] = d };
    private static JsonObject Num(string d) => new() { ["type"] = "number", ["description"] = d };
    private static JsonObject IntList(string d) => new() { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "integer" }, ["description"] = d };

    private static JsonObject Enum(string d, params string[] values) =>
        new() { ["type"] = "string", ["enum"] = new JsonArray(values.Select(v => (JsonNode)v).ToArray()), ["description"] = d };
}
