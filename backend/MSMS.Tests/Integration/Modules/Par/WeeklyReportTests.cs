using MSMS.Api.Data.Entities;
using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Par;

/// <summary>PAR-03: báo cáo tuần có số liệu thật (MSG-10) và trạng thái bài theo hạn nộp thực tế (MSG-14). Đồng hồ giả được đặt vào tuần cần xem.</summary>
[Collection("api")]
public class WeeklyReportTests(ApiFactory f)
{
    private World w => f.World;
    private static int tick;

    private sealed record Setup(Scenario S, int ClassId, Who Student, Who Parent, ApiClient Teacher, DateOnly Monday);

    private async Task<Setup> ArrangeAsync()
    {
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "WR" + World.Uniq(), 6);
        var (student, _) = await w.NewStudentAsync(cls, "HS báo cáo " + World.Uniq());
        var parent = await w.NewAccountAsync("PH báo cáo " + World.Uniq(), "Parent", "Parent", "Active");
        await f.WithDbAsync(async db =>
        {
            var sem = await db.Semester.Where(x => x.AcademicYear.IsCurrent && x.SemesterNo == 1).Select(x => x.SemesterId).SingleAsync();
            var slot = await db.PeriodSlot.Where(p => p.SessionOfDay == "Morning" && p.PeriodNo == 1).Select(p => p.PeriodSlotId).SingleAsync();
            var v = new TimetableVersion { CampusId = w.CampusAId, SemesterId = sem, Status = "Published", ImportedAt = f.Clock.Now, PublishedAt = f.Clock.Now.AddSeconds(-1000 + Interlocked.Increment(ref tick)) };
            db.TimetableVersion.Add(v);
            await db.SaveChangesAsync();
            db.TimetableVersionClass.Add(new TimetableVersionClass { VersionId = v.VersionId, ClassId = cls });
            db.TimetableEntry.Add(new TimetableEntry { VersionId = v.VersionId, ClassId = cls, DayOfWeek = 2, PeriodSlotId = slot, SubjectId = s.SubjectId, TeacherId = s.Absent.PersonId });
            db.ParentStudentLink.Add(new ParentStudentLink { ParentId = parent.PersonId, StudentId = student.PersonId, Relationship = "Phụ huynh", IsPrimary = true, CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
        });
        return new Setup(s, cls, student, parent, await f.LoginAsync(s.Absent.Username), w.NextMonday());
    }

    private static object Opts() => Enumerable.Range(0, 4).Select(i => new { label = ((char)('A' + i)).ToString(), content = "Đáp án " + (char)('A' + i), isCorrect = i == 0 }).ToArray();

    private async Task<(int AssignmentId, JsonNode Question)> AssignAsync(Setup x, string title, DateTime due, int? duration)
    {
        var q = await x.Teacher.PostAsync("/api/lrn/questions", new { subjectId = x.S.SubjectId, gradeLevel = 6, topic = "Chủ đề", content = "Câu " + World.Uniq(), explanation = "x", options = Opts(), difficulty = "Recognize" });
        Assert.Equal(200, q.Status);
        var r = await x.Teacher.PostAsync("/api/lrn/assignments", new
        {
            subjectId = x.S.SubjectId, classId = x.ClassId, title, instruction = "x", openAt = f.Clock.Now.AddMinutes(-5).ToString("yyyy-MM-ddTHH:mm:ss"), dueAt = due.ToString("yyyy-MM-ddTHH:mm:ss"),
            durationMinutes = duration, items = new[] { new { questionId = q.Body!["questionId"]!.GetValue<int>() } }, sections = new[] { new { difficulty = "Recognize", points = 10m } },
        });
        Assert.Equal(200, r.Status);
        return (r.Body!["assignment"]!["assignmentId"]!.GetValue<int>(), r.Body["questions"]![0]!);
    }

    private static object Answer(JsonNode q) => new { assignmentQuestionId = q["assignmentQuestionId"]!.GetValue<int>(), optionIds = new[] { q["options"]![0]!["optionId"]!.GetValue<int>() } };

    [Fact]
    public async Task MSG_10_The_weekly_report_shows_the_attendance_numbers_and_the_assignments_of_the_week()
    {
        var x = await ArrangeAsync();
        try
        {
            var tuesday = x.Monday.AddDays(1);
            // 4 tiết đã diễn ra: có mặt, muộn, vắng không phép, chưa điểm danh
            var statuses = new[] { "Present", "Late", "Absent", null };
            for (var i = 0; i < 4; i++)
            {
                var sid = await w.NewSessionAsync(x.ClassId, x.S.SubjectId, x.S.Absent.PersonId, tuesday, i + 1);
                if (statuses[i] is { } st)
                    await f.WithDbAsync(async db => { db.AttendanceRecord.Add(new AttendanceRecord { SessionId = sid, StudentId = x.Student.PersonId, Status = st, RecordedByPersonId = x.S.Absent.PersonId, RecordedAt = f.Clock.Now }); await db.SaveChangesAsync(); });
            }
            // một tiết đã hủy không được tính
            await w.NewSessionAsync(x.ClassId, x.S.SubjectId, x.S.Absent.PersonId, tuesday, 5, "Cancelled");
            // bài tập đến hạn trong tuần, đã nộp
            f.Clock.Set(x.Monday.ToDateTime(new TimeOnly(8, 0)));
            var (aid, q) = await AssignAsync(x, "Bài tuần", x.Monday.AddDays(4).ToDateTime(new TimeOnly(17, 0)), null);
            var st1 = await f.LoginAsync(x.Student.Username);
            f.Clock.Set(x.Monday.AddDays(2).ToDateTime(new TimeOnly(9, 0)));
            Assert.Equal(200, (await st1.PostAsync($"/api/lrn/my-assignments/{aid}/submit", new { answers = new[] { Answer(q) } })).Status);
            f.Clock.Set(x.Monday.AddDays(7).ToDateTime(new TimeOnly(8, 0)));
            var parent = await f.LoginAsync(x.Parent.Username);
            var weeks = await parent.GetAsync($"/api/par/weekly-reports/weeks?studentId={x.Student.PersonId}");
            Assert.Equal(200, weeks.Status);
            Assert.NotEmpty(weeks.Body!.AsArray());
            var rep = await parent.GetAsync($"/api/par/weekly-reports/student/{x.Student.PersonId}?weekStart={World.Iso(x.Monday)}");
            Assert.Equal(200, rep.Status);
            var att = rep.Body!["attendance"]!;
            Assert.Equal(4, att["totalSessions"]!.GetValue<int>());
            Assert.Equal(1, att["presentCount"]!.GetValue<int>());
            Assert.Equal(1, att["lateCount"]!.GetValue<int>());
            Assert.Equal(1, att["unexcusedCount"]!.GetValue<int>());
            Assert.Equal(0, att["excusedCount"]!.GetValue<int>());
            Assert.Equal(1, att["unmarkedCount"]!.GetValue<int>());
            var asg = rep.Body["assignments"]!;
            Assert.Equal(1, asg["assignedCount"]!.GetValue<int>());
            Assert.Equal(1, asg["submittedCount"]!.GetValue<int>());
            var item = asg["items"]![0]!;
            Assert.Equal("Submitted", item["status"]!.GetValue<string>());
            Assert.Equal(10m, item["score"]!.GetValue<decimal>());
            Assert.DoesNotContain("feedback", rep.Raw, StringComparison.OrdinalIgnoreCase);   // không còn nhận xét AI trong báo cáo tuần
        }
        finally { f.Clock.Reset(); }
    }

    [Fact]
    public async Task MSG_14_The_weekly_report_judges_an_unsubmitted_assignment_by_the_effective_deadline()
    {
        var x = await ArrangeAsync();
        try
        {
            var friday = x.Monday.AddDays(4);
            var due = friday.ToDateTime(new TimeOnly(17, 0));
            f.Clock.Set(friday.ToDateTime(new TimeOnly(8, 0)));
            var (a1, _) = await AssignAsync(x, "Bài có thời lượng", due, 30);
            var (a2, _) = await AssignAsync(x, "Bài không thời lượng", due, null);
            var st = await f.LoginAsync(x.Student.Username);
            f.Clock.Set(friday.ToDateTime(new TimeOnly(16, 20)));
            Assert.Equal(200, (await st.PostAsync($"/api/lrn/my-assignments/{a1}/start")).Status);
            var parent = await f.LoginAsync(x.Parent.Username);
            async Task<(string R1, string R2, string S1, string S2)> ReadAsync(int h, int m)
            {
                f.Clock.Set(friday.ToDateTime(new TimeOnly(h, m)));
                var rep = await parent.GetAsync($"/api/par/weekly-reports/student/{x.Student.PersonId}?weekStart={World.Iso(x.Monday)}");
                Assert.Equal(200, rep.Status);
                var items = rep.Body!["assignments"]!["items"]!.AsArray();
                string R(int id) => items.Single(i => i!["assignmentId"]!.GetValue<int>() == id)!["status"]!.GetValue<string>();
                var mine = (await st.GetAsync("/api/lrn/my-assignments")).Body!.AsArray();
                string S(int id) => mine.Single(i => i!["assignmentId"]!.GetValue<int>() == id)!["state"]!.GetValue<string>();
                return (R(a1), R(a2), S(a1), S(a2));
            }
            var at1655 = await ReadAsync(16, 55);
            Assert.Equal("Missed", at1655.R1);       // 16:20 + 30 phút = 16:50, đã quá hạn thực tế
            Assert.Equal("Unsubmitted", at1655.R2);  // chưa tới 17:00
            Assert.Equal("NotSubmitted", at1655.S1);  // màn học sinh cùng kết luận
            var at1701 = await ReadAsync(17, 1);
            Assert.Equal("Missed", at1701.R1);
            Assert.Equal("Missed", at1701.R2);
            Assert.Equal("NotSubmitted", at1701.S2);
        }
        finally { f.Clock.Reset(); }
    }
}
