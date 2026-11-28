using MSMS.Api.Data.Entities;
using MSMS.Tests.Integration.Infrastructure;
using static MSMS.Tests.Integration.Infrastructure.Roles;

namespace MSMS.Tests.Integration.Modules.Par;

/// <summary>PAR-02/03: hội thoại phụ huynh - giáo viên và báo cáo tuần. Tên test bắt đầu bằng mã case (MSG_xx).</summary>
[Collection("api")]
public class MessagingTests(ApiFactory f)
{
    private World w => f.World;

    private sealed record Family(int ClassId, Who Student, Who Parent, Who Homeroom, Who Other);

    /// <summary>Lớp có giáo viên chủ nhiệm, một học sinh, một phụ huynh liên kết và một giáo viên không dạy lớp.</summary>
    private async Task<Family> FamilyAsync()
    {
        f.Clock.Reset();
        await w.EnsureAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "MS" + World.Uniq(), 6);
        var hr = await w.NewStaffAsync("GVCN MSG " + World.Uniq(), "Teacher", w.CampusAId, null);
        var other = await w.NewStaffAsync("GV khác MSG " + World.Uniq(), "Teacher", w.CampusAId, null);
        var (student, _) = await w.NewStudentAsync(cls, "HS MSG " + World.Uniq());
        var parent = await w.NewAccountAsync("PH MSG " + World.Uniq(), "Parent", "Parent", "Active");
        await f.WithDbAsync(async db =>
        {
            db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = cls, StaffId = hr.PersonId, StartDate = new DateOnly(2026, 9, 5), EndDate = new DateOnly(2027, 5, 31), CreatedAt = f.Clock.Now });
            db.ParentStudentLink.Add(new ParentStudentLink { ParentId = parent.PersonId, StudentId = student.PersonId, Relationship = "Phụ huynh", IsPrimary = true, CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
        });
        return new Family(cls, student, parent, hr, other);
    }

    private async Task<int> ConversationAsync(Family fam)
    {
        var p = await f.LoginAsync(fam.Parent.Username);
        var r = await p.PostAsync("/api/par/conversations", new { studentId = fam.Student.PersonId, staffId = fam.Homeroom.PersonId });
        Assert.Equal(200, r.Status);
        return r.Body!.GetValue<int>();
    }

    [Fact]
    public async Task MSG_01_And_02_Parent_lists_the_teachers_and_teacher_lists_the_parents()
    {
        var fam = await FamilyAsync();
        var p = await f.LoginAsync(fam.Parent.Username);
        var teachers = await p.GetAsync($"/api/par/conversations/available-teachers?studentId={fam.Student.PersonId}");
        Assert.Equal(200, teachers.Status);
        Assert.Contains(fam.Homeroom.FullName, teachers.Raw);
        Assert.DoesNotContain(fam.Other.FullName, teachers.Raw);
        var stranger = await w.NewAccountAsync("PH lạ MSG " + World.Uniq(), "Parent", "Parent", "Active");
        var denied = await (await f.LoginAsync(stranger.Username)).GetAsync($"/api/par/conversations/available-teachers?studentId={fam.Student.PersonId}");
        Assert.Equal(403, denied.Status);
        Assert.Equal("Không có liên kết với học sinh này.", denied.Message);
        // MSG-02
        var hr = await f.LoginAsync(fam.Homeroom.Username);
        Assert.Contains(fam.Student.FullName, (await hr.GetAsync("/api/par/conversations/my-teaching-students")).Raw);
        Assert.Contains(fam.Parent.FullName, (await hr.GetAsync($"/api/par/conversations/available-parents?studentId={fam.Student.PersonId}")).Raw);
        var notTeaching = await (await f.LoginAsync(fam.Other.Username)).GetAsync($"/api/par/conversations/available-parents?studentId={fam.Student.PersonId}");
        Assert.Equal(403, notTeaching.Status);
        Assert.Equal("Không giảng dạy học sinh này.", notTeaching.Message);
    }

    [Fact]
    public async Task MSG_03_Creating_a_conversation_is_idempotent()
    {
        var fam = await FamilyAsync();
        var a = await ConversationAsync(fam);
        var b = await ConversationAsync(fam);
        Assert.Equal(a, b);
        Assert.Equal(1, await f.WithDbAsync(db => db.Conversation.CountAsync(c => c.StudentId == fam.Student.PersonId)));
        var hrList = await (await f.LoginAsync(fam.Homeroom.Username)).GetAsync("/api/par/conversations");
        Assert.Contains($"\"conversationId\":{a},", hrList.Raw);
    }

    [Fact]
    public async Task MSG_04_Create_conversation_validations()
    {
        var fam = await FamilyAsync();
        var p = await f.LoginAsync(fam.Parent.Username);
        var hr = await f.LoginAsync(fam.Homeroom.Username);
        Assert.Equal("Vui lòng chọn giáo viên.", (await p.PostAsync("/api/par/conversations", new { studentId = fam.Student.PersonId })).Message);
        Assert.Equal("Vui lòng chọn phụ huynh.", (await hr.PostAsync("/api/par/conversations", new { studentId = fam.Student.PersonId })).Message);
        var notTeaching = await (await f.LoginAsync(fam.Other.Username)).PostAsync("/api/par/conversations", new { studentId = fam.Student.PersonId, parentId = fam.Parent.PersonId });
        Assert.Equal(400, notTeaching.Status);
        Assert.Equal("Quan hệ giảng dạy không hợp lệ hoặc đã kết thúc.", notTeaching.Message);
        await f.WithDbAsync(async db => { (await db.Enrollment.SingleAsync(e => e.StudentId == fam.Student.PersonId)).EndDate = f.Clock.Today.AddDays(-1); await db.SaveChangesAsync(); });
        var noClass = await hr.PostAsync("/api/par/conversations", new { studentId = fam.Student.PersonId, parentId = fam.Parent.PersonId });
        Assert.Equal(400, noClass.Status);
        Assert.Equal("Quan hệ giảng dạy không hợp lệ hoặc đã kết thúc.", noClass.Message);   // tạo hội thoại kiểm quan hệ giảng dạy trước
        var parents = await hr.GetAsync($"/api/par/conversations/available-parents?studentId={fam.Student.PersonId}");
        Assert.Equal(400, parents.Status);
        Assert.Equal("Học sinh hiện không thuộc lớp nào.", parents.Message);
    }

    [Fact]
    public async Task MSG_05_And_06_Send_read_and_message_length_rules()
    {
        var fam = await FamilyAsync();
        var id = await ConversationAsync(fam);
        var p = await f.LoginAsync(fam.Parent.Username);
        var hr = await f.LoginAsync(fam.Homeroom.Username);
        Assert.Equal("Nội dung tin nhắn không được để trống.", (await p.PostAsync($"/api/par/conversations/{id}/messages", new { content = "   " })).Message);
        var tooLong = await p.PostAsync($"/api/par/conversations/{id}/messages", new { content = new string('a', 2001) });
        Assert.Equal(400, tooLong.Status);
        Assert.Equal("Nội dung tin nhắn không được vượt quá 2.000 ký tự.", tooLong.Message);
        Assert.Equal(200, (await p.PostAsync($"/api/par/conversations/{id}/messages", new { content = new string('a', 2000) })).Status);
        Assert.Equal(200, (await p.PostAsync($"/api/par/conversations/{id}/messages", new { content = "Chào cô" })).Status);
        Assert.Equal(2, (await hr.GetAsync("/api/par/conversations/unread-count")).Body!.GetValue<int>());
        var read = await hr.GetAsync($"/api/par/conversations/{id}/messages");
        Assert.Equal(200, read.Status);
        Assert.Contains("Chào cô", read.Raw);
        Assert.Equal(0, (await hr.GetAsync("/api/par/conversations/unread-count")).Body!.GetValue<int>());
        Assert.Equal(200, (await hr.PostAsync($"/api/par/conversations/{id}/messages", new { content = "Chào anh chị" })).Status);
    }

    [Fact]
    public async Task MSG_07_Only_members_can_access_a_conversation()
    {
        var fam = await FamilyAsync();
        var id = await ConversationAsync(fam);
        var stranger = await w.NewAccountAsync("PH ngoài MSG " + World.Uniq(), "Parent", "Parent", "Active");
        var r1 = await (await f.LoginAsync(stranger.Username)).GetAsync($"/api/par/conversations/{id}/messages");
        Assert.Equal(403, r1.Status);
        Assert.Equal("Bạn không phải thành viên của cuộc hội thoại này.", r1.Message);
        Assert.Equal(403, (await (await f.LoginAsync(fam.Other.Username)).PostAsync($"/api/par/conversations/{id}/messages", new { content = "x" })).Status);
        var unknown = await (await f.LoginAsync(fam.Parent.Username)).GetAsync("/api/par/conversations/987654321/messages");
        Assert.Equal(404, unknown.Status);
        Assert.Equal("Không tìm thấy cuộc hội thoại.", unknown.Message);
    }

    [Fact]
    public async Task MSG_08_The_conversation_becomes_read_only_when_the_teaching_relation_ends()
    {
        var fam = await FamilyAsync();
        var id = await ConversationAsync(fam);
        var p = await f.LoginAsync(fam.Parent.Username);
        Assert.Equal(200, (await p.PostAsync($"/api/par/conversations/{id}/messages", new { content = "Trước khi kết thúc" })).Status);
        await f.WithDbAsync(async db => { (await db.HomeroomAssignment.SingleAsync(h => h.ClassId == fam.ClassId)).EndDate = f.Clock.Today.AddDays(-1); await db.SaveChangesAsync(); });
        Assert.Contains("Trước khi kết thúc", (await p.GetAsync($"/api/par/conversations/{id}/messages")).Raw);
        Assert.Contains("Trước khi kết thúc", (await (await f.LoginAsync(fam.Homeroom.Username)).GetAsync($"/api/par/conversations/{id}/messages")).Raw);
        var send = await p.PostAsync($"/api/par/conversations/{id}/messages", new { content = "Sau khi kết thúc" });
        Assert.Equal(400, send.Status);
        Assert.Equal("Quan hệ giảng dạy đã kết thúc. Hộp thư đã chuyển sang chế độ chỉ đọc.", send.Message);
    }

    [Fact]
    public async Task MSG_09_The_inbox_is_for_teachers_and_parents_only()
    {
        await f.AssertGetByRoleAsync("/api/par/conversations", Teacher, Parent);
    }

    [Fact]
    public async Task MSG_11_To_13_Weekly_report_week_rules_access_and_homeroom_list()
    {
        var fam = await FamilyAsync();
        var monday = f.Clock.Today; while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(-1);
        var p = await f.LoginAsync(fam.Parent.Username);
        var wed = await p.GetAsync($"/api/par/weekly-reports/student/{fam.Student.PersonId}?weekStart={World.Iso(monday.AddDays(2))}");
        Assert.Equal(400, wed.Status);
        Assert.Equal("Ngày bắt đầu tuần phải là Thứ 2.", wed.Message);
        // MSG-12: ai đọc được
        var ok = $"/api/par/weekly-reports/student/{fam.Student.PersonId}?weekStart={World.Iso(monday)}";
        Assert.Equal(200, (await p.GetAsync(ok)).Status);
        Assert.Equal(200, (await (await f.LoginAsync(fam.Homeroom.Username)).GetAsync(ok)).Status);
        Assert.Equal(200, (await (await f.LoginAsync(fam.Student.Username)).GetAsync(ok)).Status);
        Assert.Equal(403, (await (await f.LoginAsync(fam.Other.Username)).GetAsync(ok)).Status);
        Assert.Equal(403, (await (await f.LoginAsync(w.SchoolAdmin.Username)).GetAsync(ok)).Status);
        var stranger = await w.NewAccountAsync("PH ngoài báo cáo " + World.Uniq(), "Parent", "Parent", "Active");
        Assert.Equal(403, (await (await f.LoginAsync(stranger.Username)).GetAsync(ok)).Status);
        // MSG-13: danh sách học sinh chủ nhiệm
        var list = await (await f.LoginAsync(fam.Homeroom.Username)).GetAsync("/api/par/weekly-reports/homeroom-students");
        Assert.Equal(200, list.Status);
        Assert.Contains(fam.Student.FullName, list.Raw);
        Assert.Equal(403, (await p.GetAsync("/api/par/weekly-reports/homeroom-students")).Status);
    }
}
