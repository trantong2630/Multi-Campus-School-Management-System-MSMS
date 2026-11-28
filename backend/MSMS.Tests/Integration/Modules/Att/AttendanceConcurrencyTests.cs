using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Att;

/// <summary>ATT-07: hai người cùng lưu bảng điểm danh trống một lúc thì không bao giờ sinh bản ghi trùng; người đến sau (nếu chồng nhau) nhận 409.</summary>
[Collection("api")]
public class AttendanceConcurrencyTests(ApiFactory f)
{
    private World w => f.World;

    [Fact]
    public async Task ATT_07_Concurrent_first_saves_are_detected_and_never_duplicate_rows()
    {
        var saw409 = false;
        for (var attempt = 0; attempt < 6 && !saw409; attempt++)
        {
            f.Clock.Reset();
            var s = await w.NewScenarioAsync();
            var date = w.NextMonday();
            var sid = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, date, 1);
            var kids = new List<Who>();
            for (var i = 0; i < 25; i++) kids.Add((await w.NewStudentAsync(s.ClassAId, $"HS song song {i} {World.Uniq()}")).Who);
            var teacher = await f.LoginAsync(s.Absent.Username);
            var admin = await f.LoginAsync(w.SchoolAdmin.Username);
            f.Clock.Set(date.ToDateTime(new TimeOnly(7, 0)));
            try
            {
                object Body(string st) => new { items = kids.Select(k => new { studentId = k.PersonId, status = st, note = (string?)null }).ToArray() };
                var results = await Task.WhenAll(teacher.PutAsync($"/api/att/sessions/{sid}", Body("Present")), admin.PutAsync($"/api/att/sessions/{sid}", Body("Absent")));
                Assert.All(results, r => Assert.True(r.Status is 200 or 409, "trạng thái " + r.Status));
                Assert.Contains(results, r => r.Status == 200);
                Assert.Equal(25, await f.WithDbAsync(db => db.AttendanceRecord.CountAsync(a => a.SessionId == sid)));   // không bao giờ trùng
                var conflict = results.FirstOrDefault(r => r.Status == 409);
                if (conflict is not null)
                {
                    saw409 = true;
                    Assert.Equal("Bảng điểm danh vừa được lưu từ nơi khác. Tải lại bảng rồi lưu lại.", conflict.Message);
                }
            }
            finally { f.Clock.Reset(); }
        }
    }
}
