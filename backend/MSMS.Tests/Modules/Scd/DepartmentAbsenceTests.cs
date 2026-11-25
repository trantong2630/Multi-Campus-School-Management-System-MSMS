using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MSMS.Api.Common.Audit;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Email;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Notifications;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Sch.Services;
using MSMS.Api.Modules.Scd.Dtos;
using MSMS.Api.Modules.Scd.Services;
using static MSMS.Tests.Modules.Sch.SchTestSupport;

namespace MSMS.Tests.Modules.Scd;

/// <summary>
/// Tổ trưởng khai báo giáo viên vắng cho giáo viên của tổ (mọi phân hiệu) và quyết định từng tiết bằng đề xuất (dạy thay hoặc hủy); hiệu phó của phân hiệu
/// có tiết chỉ duyệt, và chỉ duyệt được khi tổ trưởng đã quyết định hết mọi tiết của khai báo (BR-16). Hiệu phó không khai báo vắng.
/// Tổ Toán (tổ 1) có tổ trưởng; tổ Văn (tổ 2) thì không.
/// </summary>
public class DepartmentAbsenceTests
{
    private const int Campus1 = 1, Campus2 = 2, Year1 = 1, Class6A = 11, Class7A = 12;
    private const int Head = 60, Vice1 = 70, Vice2 = 71, MathA = 50, MathB = 51, LitTeacher = 52, NotHead = 61, Dept1 = 1, Dept2 = 2, MathSubject = 1, LitSubject = 2;
    private const int Slot1 = 1, Slot2 = 2;
    private static readonly DateOnly Monday = D(2026, 10, 26);

    /// <summary>Ghi lại thư đã gửi; tín hiệu mỗi lần gửi để test chờ tác vụ gửi nền.</summary>
    private sealed class RecordingSender : IEmailSender
    {
        public readonly ConcurrentQueue<(string To, string Subject, string Body)> Sent = new();
        public readonly SemaphoreSlim Signal = new(0);

        public Task<bool> SendAsync(string recipient, string subject, string body, CancellationToken ct = default)
        {
            Sent.Enqueue((recipient, subject, body));
            Signal.Release();
            return Task.FromResult(true);
        }

        public async Task<(string To, string Subject, string Body)> NextAsync()
        {
            Assert.True(await Signal.WaitAsync(TimeSpan.FromSeconds(5)), "Không có email nào được gửi.");
            Sent.TryDequeue(out var mail);
            return mail;
        }

        /// <summary>Chờ ngắn để chắc tác vụ gửi nền (nếu có) đã chạy, rồi khẳng định không có thư.</summary>
        public async Task AssertNoneAsync()
        {
            Assert.False(await Signal.WaitAsync(TimeSpan.FromMilliseconds(300)), "Không được gửi email.");
            Assert.Empty(Sent);
        }
    }

    private sealed class Env
    {
        public required MsmsDbContext Db { get; init; }
        public required IClock Clock { get; init; }
        public RecordingSender Mail { get; } = new();
        public TeacherAbsenceService For(string accountType, int personId)
        {
            var user = UserOf(accountType, personId);
            var provider = new ServiceCollection()
                .AddSingleton(Db).AddSingleton(user).AddSingleton(Clock).AddLogging().AddSingleton<IEmailSender>(Mail)
                .AddScoped<ScopeResolver>().AddScoped<AuditWriter>().AddScoped<NotificationSender>().AddScoped<ScheduleSupport>()
                .AddScoped<AssignmentEmailNotifier>()
                .BuildServiceProvider();
            return ActivatorUtilities.CreateInstance<TeacherAbsenceService>(provider);
        }

        public TeacherAbsenceService HeadSvc => For(AccountTypes.Teacher, Head);
        public TeacherAbsenceService Vice1Svc => For(AccountTypes.VicePrincipal, Vice1);
        public TeacherAbsenceService Vice2Svc => For(AccountTypes.VicePrincipal, Vice2);

        public TeachingSession S(long id) => Db.TeachingSession.AsNoTracking().Single(s => s.SessionId == id);
    }

    private static void AddTeacher(MsmsDbContext db, int id, string name, int? department)
    {
        AddStaff(db, id, name);
        db.StaffProfile.Find(id)!.DepartmentId = department;
        db.Account.Add(new Account
        {
            PersonId = id, Username = $"gv{id}", PasswordHash = "x", AccountType = AccountTypes.Teacher, Status = AccountStatuses.Active, CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
    }

    private static TeachingSession Session(MsmsDbContext db, int classId, int subjectId, int? teacherId, int slotId, string status = "Scheduled")
    {
        var s = new TeachingSession
        {
            ClassId = classId, SubjectId = subjectId, TeacherId = teacherId, SessionDate = Monday, PeriodSlotId = slotId,
            Status = status, Source = "Timetable", CreatedAt = DateTime.UtcNow,
        };
        db.TeachingSession.Add(s);
        db.SaveChanges();
        return s;
    }

    /// <summary>Giáo viên Toán A dạy lớp 6A (phân hiệu 1, tiết 1) và 7A (phân hiệu 2, tiết 2); Toán B rảnh; giáo viên Văn dạy 6A tiết 2.</summary>
    private static (Env Env, long Campus1Session, long Campus2Session, long LitSession) Create()
    {
        var db = NewDb();
        var clock = ClockAt();
        AddCampus(db, Campus1, "Phân hiệu 1");
        AddCampus(db, Campus2, "Phân hiệu 2");
        AddYear(db, Year1, "2026-2027", D(2026, 9, 5), D(2027, 5, 31), current: true);
        db.Semester.Add(new Semester { AcademicYearId = Year1, SemesterNo = 1, Name = "Học kỳ 1", StartDate = D(2026, 9, 5), EndDate = D(2027, 1, 18) });
        AddClass(db, Class6A, Campus1, Year1, "6A");
        AddClass(db, Class7A, Campus2, Year1, "7A", 7);
        db.SubjectDepartment.AddRange(new SubjectDepartment { DepartmentId = Dept1, Name = "Tổ Toán", IsActive = true },
            new SubjectDepartment { DepartmentId = Dept2, Name = "Tổ Văn", IsActive = true });
        db.Subject.AddRange(new Subject { SubjectId = MathSubject, DepartmentId = Dept1, Name = "Toán", Abbreviation = "TOAN", IsActive = true },
            new Subject { SubjectId = LitSubject, DepartmentId = Dept2, Name = "Ngữ văn", Abbreviation = "VAN", IsActive = true });
        db.PeriodSlot.AddRange(
            new PeriodSlot { PeriodSlotId = Slot1, SessionOfDay = "Morning", PeriodNo = 1, StartTime = new TimeOnly(7, 0), EndTime = new TimeOnly(7, 45), IsActive = true },
            new PeriodSlot { PeriodSlotId = Slot2, SessionOfDay = "Morning", PeriodNo = 2, StartTime = new TimeOnly(7, 50), EndTime = new TimeOnly(8, 35), IsActive = true });
        db.SaveChanges();

        AddStaff(db, Vice1, "Hiệu phó 1", Campus1);
        AddStaff(db, Vice2, "Hiệu phó 2", Campus2);
        foreach (var (campus, vice) in new[] { (Campus1, Vice1), (Campus2, Vice2) })
        {
            db.Account.Add(new Account { PersonId = vice, Username = $"hp{vice}", PasswordHash = "x", AccountType = AccountTypes.VicePrincipal, Status = AccountStatuses.Active, CreatedAt = DateTime.UtcNow });
            db.CampusLeadership.Add(new CampusLeadership { CampusId = campus, StaffId = vice, StartDate = D(2026, 9, 5), CreatedAt = DateTime.UtcNow });
        }
        AddTeacher(db, Head, "Tổ trưởng Toán", Dept1);
        AddTeacher(db, NotHead, "Giáo viên Toán không phải tổ trưởng", Dept1);
        AddTeacher(db, MathA, "Toán A", Dept1);
        AddTeacher(db, MathB, "Toán B", Dept1);
        AddTeacher(db, LitTeacher, "Giáo viên Văn", Dept2);
        db.DepartmentHead.Add(new DepartmentHead { DepartmentId = Dept1, StaffId = Head, StartDate = D(2026, 9, 5), CreatedAt = DateTime.UtcNow });
        db.SaveChanges();

        var s1 = Session(db, Class6A, MathSubject, MathA, Slot1);
        var s2 = Session(db, Class7A, MathSubject, MathA, Slot2);
        var lit = Session(db, Class6A, LitSubject, LitTeacher, Slot2);
        return (new Env { Db = db, Clock = clock }, s1.SessionId, s2.SessionId, lit.SessionId);
    }

    private static DeclareAbsenceRequest Declare(int teacherId) => new(teacherId, Monday, Monday, null, "Ốm");
    private static ProposeSessionRequest Sub(int teacherId) => new("Substitute", teacherId);
    private static readonly ProposeSessionRequest Cancel = new("Cancel", null);
    private static readonly ProposeSessionRequest Clear = new("Clear", null);

    private static async Task<int> StatusOf(Func<Task> act)
    {
        var ex = await Assert.ThrowsAsync<ApiException>(act);
        return ex.StatusCode;
    }

    private static async Task<string> MessageOf(Func<Task> act) => (await Assert.ThrowsAsync<ApiException>(act)).Message;

    // ---------------------------------------------------------------- Khai báo (tổ trưởng)

    [Fact(DisplayName = "SCD | Tổ trưởng | Khai vắng giáo viên của tổ | tiết ở mọi phân hiệu chuyển Chờ xử lý, chưa có đề xuất nào")]
    public async Task Head_Declares_ForOwnDepartmentTeacher_AllCampuses()
    {
        var (env, s1, s2, _) = Create();

        var result = await env.HeadSvc.DeclareAsync(Declare(MathA));

        Assert.Equal(2, result.PendingCount);
        Assert.All(new[] { s1, s2 }, id => Assert.Equal("Pending", env.S(id).Status));
        Assert.All(new[] { s1, s2 }, id => Assert.Null(env.S(id).TeacherId));
        Assert.All(new[] { s1, s2 }, id => Assert.Null(env.S(id).ProposedAction));
        Assert.Equal(Head, (await env.Db.TeacherAbsence.SingleAsync()).DeclaredByPersonId);
    }

    [Fact(DisplayName = "SCD | Hiệu phó | Không còn khai báo vắng | chỉ tổ trưởng khai, hiệu phó bị từ chối")]
    public async Task Vice_CannotDeclare()
    {
        var (env, s1, _, _) = Create();

        Assert.Equal(403, await StatusOf(() => env.Vice1Svc.DeclareAsync(Declare(MathA))));

        Assert.Equal("Scheduled", env.S(s1).Status);
        Assert.Empty(env.Db.TeacherAbsence);
    }

    [Fact(DisplayName = "SCD | Tổ trưởng | Khai vắng giáo viên tổ khác | bị từ chối, không đổi lịch")]
    public async Task Head_CannotDeclare_ForOtherDepartment()
    {
        var (env, _, _, lit) = Create();

        Assert.Equal(403, await StatusOf(() => env.HeadSvc.DeclareAsync(Declare(LitTeacher))));

        Assert.Equal("Scheduled", env.S(lit).Status);
        Assert.Empty(env.Db.TeacherAbsence);
    }

    [Fact(DisplayName = "SCD | Tổ trưởng | Giáo viên không giữ trách nhiệm tổ trưởng | không khai vắng, không đề xuất được")]
    public async Task NonHead_Teacher_IsRejected()
    {
        var (env, s1, _, _) = Create();
        var svc = env.For(AccountTypes.Teacher, NotHead);

        Assert.Equal(403, await StatusOf(() => svc.DeclareAsync(Declare(MathA))));
        Assert.Equal(403, await StatusOf(() => svc.ListForDepartmentAsync()));
        Assert.Equal(403, await StatusOf(() => svc.ProposeAsync(s1, Sub(MathB))));
    }

    [Fact(DisplayName = "SCD | Tổ trưởng | Danh sách giáo viên chọn khi khai | chỉ giáo viên của tổ mình")]
    public async Task Head_Teachers_AreOnlyFromOwnDepartment()
    {
        var (env, _, _, _) = Create();

        var ids = (await env.HeadSvc.DepartmentTeachersAsync()).Select(t => t.TeacherId).Order().ToList();

        Assert.Equal(new[] { MathA, MathB, NotHead, Head }.Order(), ids);
    }

    // ---------------------------------------------------------------- Quyết định từng tiết (đề xuất)

    [Fact(DisplayName = "SCD | Tổ trưởng | Đề xuất dạy thay | lưu đề xuất, tiết vẫn Chờ xử lý, hiệu phó thấy đề xuất, có ghi nhật ký")]
    public async Task Head_ProposesSubstitute_ViceSeesIt()
    {
        var (env, s1, _, _) = Create();
        await env.HeadSvc.DeclareAsync(Declare(MathA));

        await env.HeadSvc.ProposeAsync(s1, Sub(MathB));

        var session = env.S(s1);
        Assert.Equal("Pending", session.Status);
        Assert.Equal("Substitute", session.ProposedAction);
        Assert.Equal(MathB, session.ProposedTeacherId);
        Assert.Null(session.TeacherId);
        var pending = (await env.Vice1Svc.ListPendingAsync(Campus1, null, null, null)).Single();
        Assert.Equal("Substitute", pending.ProposedAction);
        Assert.Equal("Toán B", pending.ProposedTeacherName);
        var log = await env.Db.AuditLog.SingleAsync(a => a.Action == AuditActions.ProposeSession);
        Assert.Equal(AuditCategories.ScheduleAdjust, log.Category);
        Assert.Equal(Head, log.ActorPersonId);
        Assert.Contains("Dạy thay", log.Detail!);
    }

    [Fact(DisplayName = "SCD | Tổ trưởng | Đề xuất hủy tiết | lưu đề xuất hủy, không có giáo viên")]
    public async Task Head_ProposesCancel()
    {
        var (env, s1, _, _) = Create();
        await env.HeadSvc.DeclareAsync(Declare(MathA));

        await env.HeadSvc.ProposeAsync(s1, Sub(MathB));
        await env.HeadSvc.ProposeAsync(s1, Cancel);

        var session = env.S(s1);
        Assert.Equal("Pending", session.Status);
        Assert.Equal("Cancel", session.ProposedAction);
        Assert.Null(session.ProposedTeacherId);
    }

    [Fact(DisplayName = "SCD | Tổ trưởng | Đề xuất sai | thiếu giáo viên hoặc hành động lạ bị từ chối")]
    public async Task Propose_RejectsMissingTeacherAndUnknownAction()
    {
        var (env, s1, _, _) = Create();
        await env.HeadSvc.DeclareAsync(Declare(MathA));

        Assert.Equal(400, await StatusOf(() => env.HeadSvc.ProposeAsync(s1, new ProposeSessionRequest("Substitute", null))));
        Assert.Equal(400, await StatusOf(() => env.HeadSvc.ProposeAsync(s1, new ProposeSessionRequest("Approve", null))));
        Assert.Null(env.S(s1).ProposedAction);
    }

    [Fact(DisplayName = "SCD | Tổ trưởng | Đề xuất | giáo viên khác tổ, bận cùng tiết hoặc đang vắng bị từ chối")]
    public async Task Propose_RejectsOtherDepartmentBusyOrAbsentTeachers()
    {
        var (env, s1, _, _) = Create();
        await env.HeadSvc.DeclareAsync(Declare(MathA));
        Session(env.Db, Class7A, MathSubject, MathB, Slot1);   // Toán B đã có tiết cùng khung giờ với tiết 1

        Assert.Equal(400, await StatusOf(() => env.HeadSvc.ProposeAsync(s1, Sub(LitTeacher))));   // khác tổ
        Assert.Equal(400, await StatusOf(() => env.HeadSvc.ProposeAsync(s1, Sub(MathB))));        // bận
        Assert.Equal(400, await StatusOf(() => env.HeadSvc.ProposeAsync(s1, Sub(MathA))));        // đang vắng
        Assert.Null(env.S(s1).ProposedAction);
    }

    [Fact(DisplayName = "SCD | Tổ trưởng | Đề xuất cho tiết môn của tổ khác hoặc tiết chưa Chờ xử lý | bị từ chối")]
    public async Task Propose_RejectsOtherDepartmentSessionAndNonPendingSession()
    {
        var (env, s1, _, lit) = Create();

        Assert.Equal(403, await StatusOf(() => env.HeadSvc.ProposeAsync(lit, Sub(MathB))));   // môn Văn thuộc tổ khác
        Assert.Equal(400, await StatusOf(() => env.HeadSvc.ProposeAsync(s1, Sub(MathB))));    // tiết còn Đã xếp lịch
    }

    [Fact(DisplayName = "SCD | Tổ trưởng | Bỏ đề xuất | Clear xóa đề xuất")]
    public async Task Propose_Clear_RemovesTheProposal()
    {
        var (env, s1, _, _) = Create();
        await env.HeadSvc.DeclareAsync(Declare(MathA));
        await env.HeadSvc.ProposeAsync(s1, Sub(MathB));

        await env.HeadSvc.ProposeAsync(s1, Clear);

        Assert.Null(env.S(s1).ProposedAction);
        Assert.Null(env.S(s1).ProposedTeacherId);
    }

    [Fact(DisplayName = "SCD | Tổ trưởng | Danh sách tiết Chờ xử lý của tổ | chỉ môn của tổ mình, ở mọi phân hiệu, gợi ý chỉ giáo viên cùng tổ")]
    public async Task Head_Pending_AndSuggestions_AreScopedToDepartment()
    {
        var (env, s1, s2, lit) = Create();
        await env.HeadSvc.DeclareAsync(Declare(MathA));
        env.Db.TeachingSession.Find(lit)!.Status = "Pending";   // tiết Văn cũng Chờ xử lý nhưng thuộc tổ khác
        env.Db.TeachingSession.Find(lit)!.TeacherId = null;
        env.Db.SaveChanges();

        var pending = await env.HeadSvc.ListPendingForDepartmentAsync();
        var suggestions = await env.HeadSvc.DepartmentSuggestionsAsync(s1);

        Assert.Equal(new[] { s1, s2 }.Order(), pending.Select(p => p.SessionId).Order());
        Assert.Equal(["Phân hiệu 1", "Phân hiệu 2"], pending.Select(p => p.CampusName).Order());
        Assert.DoesNotContain(suggestions, t => t.TeacherId == LitTeacher);
        Assert.Contains(suggestions, t => t.TeacherId == MathB);
        Assert.Equal(403, await StatusOf(() => env.HeadSvc.DepartmentSuggestionsAsync(lit)));
    }

    [Fact(DisplayName = "SCD | Tổ trưởng | Thông tin để quyết định | danh sách tiết kèm lý do và khoảng ngày vắng, gợi ý kèm số tiết giáo viên dạy hôm đó")]
    public async Task Head_SeesAbsenceContext_AndDayLoadInSuggestions()
    {
        var (env, s1, _, _) = Create();
        await env.HeadSvc.DeclareAsync(Declare(MathA));
        Session(env.Db, Class7A, MathSubject, MathB, Slot2);   // Toán B đã có một tiết khác trong ngày (khung giờ khác tiết 1)

        var pending = (await env.HeadSvc.ListPendingForDepartmentAsync()).Single(p => p.SessionId == s1);
        var suggestions = await env.HeadSvc.DepartmentSuggestionsAsync(s1);

        Assert.Equal("Ốm", pending.AbsenceReason);
        Assert.Equal((Monday, Monday), (pending.AbsenceFrom, pending.AbsenceTo));
        Assert.Equal(1, suggestions.Single(t => t.TeacherId == MathB).DaySessionCount);
        Assert.Equal(0, suggestions.Single(t => t.TeacherId == NotHead).DaySessionCount);
    }

    [Fact(DisplayName = "SCD | Tổ trưởng | Quyết định xong tiết cuối | hiệu phó mỗi phân hiệu có tiết được báo chờ duyệt, đổi quyết định thì không báo lại")]
    public async Task LastDecision_NotifiesVicesOnce()
    {
        var (env, s1, s2, _) = Create();
        await env.HeadSvc.DeclareAsync(Declare(MathA));

        await env.HeadSvc.ProposeAsync(s1, Sub(MathB));
        Assert.Empty(env.Db.Notification.Where(n => n.NotificationType == NotificationTypes.SessionPending));   // mới 1/2 tiết

        await env.HeadSvc.ProposeAsync(s2, Cancel);
        var told = await env.Db.Notification.Where(n => n.NotificationType == NotificationTypes.SessionPending).ToListAsync();
        Assert.Equal([Vice1, Vice2], told.Select(n => n.RecipientPersonId).Order());
        Assert.All(told, n => Assert.Equal("Khai báo vắng chờ duyệt", n.Title));

        await env.HeadSvc.ProposeAsync(s1, Cancel);   // đổi quyết định
        Assert.Equal(2, await env.Db.Notification.CountAsync(n => n.NotificationType == NotificationTypes.SessionPending));
    }

    // ---------------------------------------------------------------- Hiệu phó duyệt

    [Fact(DisplayName = "SCD | Hiệu phó duyệt | Tổ trưởng chưa quyết định hết | không duyệt được, nói rõ đã quyết định bao nhiêu tiết")]
    public async Task Approve_IsBlockedUntilEverySessionIsDecided()
    {
        var (env, s1, _, _) = Create();
        var declared = await env.HeadSvc.DeclareAsync(Declare(MathA));
        await env.HeadSvc.ProposeAsync(s1, Sub(MathB));   // tiết ở phân hiệu 2 chưa quyết định

        var message = await MessageOf(() => env.Vice1Svc.ApproveAsync(declared.AbsenceId));

        Assert.Contains("1/2", message);
        Assert.Equal("Pending", env.S(s1).Status);
        var row = (await env.Vice1Svc.ListAsync(Campus1)).Single();
        Assert.Equal(2, row.AwaitingCount);
        Assert.Equal(1, row.DecidedCount);
        Assert.False(row.CanApprove);
    }

    [Fact(DisplayName = "SCD | Hiệu phó duyệt | Đã quyết định hết | áp dạy thay và hủy ở phân hiệu mình, phân hiệu khác do hiệu phó đó duyệt")]
    public async Task Approve_AppliesOwnCampus_ThenOtherViceApprovesTheRest()
    {
        var (env, s1, s2, _) = Create();
        var declared = await env.HeadSvc.DeclareAsync(Declare(MathA));
        await env.HeadSvc.ProposeAsync(s1, Sub(MathB));
        await env.HeadSvc.ProposeAsync(s2, Cancel);
        Assert.True((await env.Vice1Svc.ListAsync(Campus1)).Single().CanApprove);

        var first = await env.Vice1Svc.ApproveAsync(declared.AbsenceId);

        Assert.Equal((1, 0, 1), (first.SubstitutedCount, first.CancelledCount, first.OtherCampusPendingCount));
        var applied = env.S(s1);
        Assert.Equal("Scheduled", applied.Status);
        Assert.Equal(MathB, applied.TeacherId);
        Assert.Null(applied.ProposedAction);
        Assert.Equal("Pending", env.S(s2).Status);   // phân hiệu 2 chưa được duyệt
        Assert.True(await env.Db.Notification.AnyAsync(n => n.RecipientPersonId == MathB && n.NotificationType == NotificationTypes.ScheduleChanged));
        Assert.Equal(403, await StatusOf(() => env.Vice1Svc.ApproveAsync(declared.AbsenceId)));   // phân hiệu 1 hết tiết chờ

        var second = await env.Vice2Svc.ApproveAsync(declared.AbsenceId);

        Assert.Equal((0, 1, 0), (second.SubstitutedCount, second.CancelledCount, second.OtherCampusPendingCount));
        Assert.Equal("Cancelled", env.S(s2).Status);
        var logs = await env.Db.AuditLog.Where(a => a.Action == AuditActions.ApproveAbsence).ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.All(logs, l => Assert.Equal(AuditCategories.ScheduleAdjust, l.Category));
    }

    [Fact(DisplayName = "SCD | Hiệu phó duyệt | Một đề xuất không còn hợp lệ | không áp tiết nào, nói rõ tiết lỗi để xử lý tay")]
    public async Task Approve_IsAllOrNothing_WhenAProposalBecomesInvalid()
    {
        var (env, s1, s2, _) = Create();
        var declared = await env.HeadSvc.DeclareAsync(Declare(MathA));
        await env.HeadSvc.ProposeAsync(s1, Sub(MathB));
        await env.HeadSvc.ProposeAsync(s2, Cancel);
        Session(env.Db, Class7A, MathSubject, MathB, Slot1);   // sau khi đề xuất, Toán B có tiết khác cùng khung giờ với tiết 1

        var message = await MessageOf(() => env.Vice1Svc.ApproveAsync(declared.AbsenceId));

        Assert.Contains("không còn hợp lệ", message);
        Assert.Contains("Toán B", message);
        Assert.Equal("Pending", env.S(s1).Status);
        Assert.Equal("Substitute", env.S(s1).ProposedAction);
    }

    [Fact(DisplayName = "SCD | Hiệu phó duyệt | Xử lý tay một tiết rồi duyệt | tiết đã xử lý bỏ khỏi khai báo, các tiết còn lại duyệt được")]
    public async Task Vice_ResolvesOneByHand_ThenApprovesTheRest()
    {
        var (env, s1, s2, _) = Create();
        var declared = await env.HeadSvc.DeclareAsync(Declare(MathA));
        await env.HeadSvc.ProposeAsync(s2, Cancel);   // tổ trưởng chưa quyết định tiết 1

        Assert.Contains("1/2", await MessageOf(() => env.Vice2Svc.ApproveAsync(declared.AbsenceId)));
        await env.Vice1Svc.ResolveAsync(s1, new ResolvePendingRequest("Substitute", NotHead));   // hiệu phó phân hiệu 1 xử lý tay
        var result = await env.Vice2Svc.ApproveAsync(declared.AbsenceId);

        Assert.Equal(NotHead, env.S(s1).TeacherId);
        Assert.Equal(1, result.CancelledCount);
        Assert.Equal("Cancelled", env.S(s2).Status);
    }

    [Fact(DisplayName = "SCD | Hiệu phó xử lý tay | Hủy tiết hoặc chọn người dạy thay | xóa đề xuất của tổ trưởng")]
    public async Task Vice_Resolve_ClearsTheProposal()
    {
        var (env, s1, s2, _) = Create();
        await env.HeadSvc.DeclareAsync(Declare(MathA));
        await env.HeadSvc.ProposeAsync(s1, Sub(MathB));
        await env.HeadSvc.ProposeAsync(s2, Cancel);

        await env.Vice1Svc.ResolveAsync(s1, new ResolvePendingRequest("Substitute", NotHead));
        await env.Vice2Svc.ResolveAsync(s2, new ResolvePendingRequest("Cancel", null));

        Assert.Null(env.S(s1).ProposedAction);
        Assert.Null(env.S(s1).ProposedTeacherId);
        Assert.Null(env.S(s2).ProposedAction);
        Assert.Equal("Cancelled", env.S(s2).Status);
    }

    [Fact(DisplayName = "SCD | Hiệu phó duyệt | Tổ trưởng và giáo viên khác không duyệt được")]
    public async Task OnlyVice_CanApprove()
    {
        var (env, s1, s2, _) = Create();
        var declared = await env.HeadSvc.DeclareAsync(Declare(MathA));
        await env.HeadSvc.ProposeAsync(s1, Sub(MathB));
        await env.HeadSvc.ProposeAsync(s2, Cancel);

        Assert.Equal(403, await StatusOf(() => env.HeadSvc.ApproveAsync(declared.AbsenceId)));
        Assert.Equal("Pending", env.S(s1).Status);
    }

    // ---------------------------------------------------------------- Danh sách và xóa

    [Fact(DisplayName = "SCD | Tổ trưởng | Danh sách khai báo của tổ | đếm tiết ở mọi phân hiệu và tiến độ quyết định; xóa khai báo trả tiết và bỏ đề xuất")]
    public async Task Head_List_CountsAllCampuses_AndDeleteRestoresAndClearsProposals()
    {
        var (env, s1, s2, _) = Create();
        var declared = await env.HeadSvc.DeclareAsync(Declare(MathA));
        await env.HeadSvc.ProposeAsync(s1, Sub(MathB));

        var row = (await env.HeadSvc.ListForDepartmentAsync()).Single();
        Assert.Equal(2, row.PendingCount);
        Assert.Equal((2, 1), (row.AwaitingCount, row.DecidedCount));
        Assert.False(row.CanApprove);   // chỉ hiệu phó duyệt
        Assert.True(row.CanDelete);

        var result = await env.HeadSvc.DeleteAsync(declared.AbsenceId);

        Assert.Equal(2, result.RestoredCount);
        Assert.All(new[] { s1, s2 }, id => Assert.Equal("Scheduled", env.S(id).Status));
        Assert.Equal(MathA, env.S(s1).TeacherId);
        Assert.Null(env.S(s1).ProposedAction);
        Assert.Null(env.S(s1).ProposedTeacherId);
        Assert.Empty(env.Db.TeacherAbsence);
    }

    [Fact(DisplayName = "SCD | Tổ trưởng | Khai báo không do mình khai | thấy nhưng không xóa được")]
    public async Task Head_CannotDelete_AbsenceDeclaredBySomeoneElse()
    {
        var (env, _, _, _) = Create();
        var absence = new TeacherAbsence { TeacherId = MathA, FromDate = Monday, ToDate = Monday, DeclaredByPersonId = Vice1, CreatedAt = DateTime.UtcNow };
        env.Db.TeacherAbsence.Add(absence);
        env.Db.SaveChanges();

        var row = (await env.HeadSvc.ListForDepartmentAsync()).Single();
        Assert.False(row.CanDelete);
        Assert.Equal(403, await StatusOf(() => env.HeadSvc.DeleteAsync(absence.AbsenceId)));
        Assert.Equal(403, await StatusOf(() => env.Vice1Svc.DeleteAsync(absence.AbsenceId)));   // hiệu phó không xóa khai báo
    }

    // ---------------------------------------------------------------- Email và lịch dạy khi duyệt

    private static void GiveEmail(Env env, int teacherId, string email) { env.Db.Person.Find(teacherId)!.Email = email; env.Db.SaveChanges(); }

    [Fact(DisplayName = "SCD | Duyệt | Email | giáo viên dạy thay nhận đúng một email như khi hiệu phó xử lý tay, thông báo trong ứng dụng vẫn có")]
    public async Task Approve_EmailsTheSubstitute_LikeAManualResolve()
    {
        var (env, s1, s2, _) = Create();
        GiveEmail(env, MathB, " b@truong.edu.vn ");
        var declared = await env.HeadSvc.DeclareAsync(Declare(MathA));
        await env.HeadSvc.ProposeAsync(s1, Sub(MathB));
        await env.HeadSvc.ProposeAsync(s2, Cancel);
        await env.Mail.AssertNoneAsync();   // khai báo và đề xuất chưa gửi email nào

        await env.Vice1Svc.ApproveAsync(declared.AbsenceId);

        var mail = await env.Mail.NextAsync();
        Assert.Equal("b@truong.edu.vn", mail.To);   // email hồ sơ được trim
        Assert.StartsWith("[MSMS] Phân công dạy thay: Toán lớp 6A", mail.Subject);
        Assert.Contains("Bạn được phân công dạy thay tiết sau:", mail.Body);
        Assert.Contains("07:00–07:45", mail.Body);
        await env.Mail.AssertNoneAsync();   // tiết hủy ở phân hiệu 2 chưa được duyệt, và hủy tiết chờ không có email
        Assert.True(await env.Db.Notification.AnyAsync(n => n.RecipientPersonId == MathB && n.NotificationType == NotificationTypes.ScheduleChanged));
    }

    [Fact(DisplayName = "SCD | Duyệt | Email | hai tiết dạy thay cho hai giáo viên thì mỗi người nhận đúng một thư của tiết mình, hủy tiết không gửi email")]
    public async Task Approve_EmailsEachSubstituteOnce_AndNothingForCancellations()
    {
        var (env, s1, s2, _) = Create();
        GiveEmail(env, MathB, "b@truong.edu.vn");
        GiveEmail(env, NotHead, "c@truong.edu.vn");
        var declared = await env.HeadSvc.DeclareAsync(Declare(MathA));
        await env.HeadSvc.ProposeAsync(s1, Sub(MathB));
        await env.HeadSvc.ProposeAsync(s2, Sub(NotHead));
        await env.Vice1Svc.ApproveAsync(declared.AbsenceId);
        await env.Vice2Svc.ApproveAsync(declared.AbsenceId);

        var first = await env.Mail.NextAsync();
        var second = await env.Mail.NextAsync();
        var byRecipient = new[] { first, second }.ToDictionary(m => m.To);

        Assert.Equal(2, byRecipient.Count);
        Assert.Contains("lớp 6A", byRecipient["b@truong.edu.vn"].Subject);
        Assert.Contains("lớp 7A", byRecipient["c@truong.edu.vn"].Subject);
        await env.Mail.AssertNoneAsync();
    }

    [Fact(DisplayName = "SCD | Duyệt | Email | duyệt thất bại (đề xuất không còn hợp lệ) hoặc bị chặn thì không gửi email nào")]
    public async Task FailedOrBlockedApproval_SendsNoEmail()
    {
        var (env, s1, s2, _) = Create();
        GiveEmail(env, MathB, "b@truong.edu.vn");
        var declared = await env.HeadSvc.DeclareAsync(Declare(MathA));
        await env.HeadSvc.ProposeAsync(s1, Sub(MathB));

        await Assert.ThrowsAsync<ApiException>(() => env.Vice1Svc.ApproveAsync(declared.AbsenceId));   // tiết 2 chưa quyết định
        await env.HeadSvc.ProposeAsync(s2, Cancel);
        Session(env.Db, Class7A, MathSubject, MathB, Slot1);   // Toán B bận cùng khung giờ với tiết 1
        await Assert.ThrowsAsync<ApiException>(() => env.Vice1Svc.ApproveAsync(declared.AbsenceId));    // đề xuất không còn hợp lệ

        await env.Mail.AssertNoneAsync();
        Assert.Equal("Pending", env.S(s1).Status);
    }

    [Fact(DisplayName = "SCD | Duyệt | Email | giáo viên dạy thay không có email trong hồ sơ thì bỏ qua email, duyệt vẫn thành công")]
    public async Task Approve_SkipsTeachersWithoutEmail()
    {
        var (env, s1, s2, _) = Create();
        var declared = await env.HeadSvc.DeclareAsync(Declare(MathA));
        await env.HeadSvc.ProposeAsync(s1, Sub(MathB));
        await env.HeadSvc.ProposeAsync(s2, Cancel);

        await env.Vice1Svc.ApproveAsync(declared.AbsenceId);

        await env.Mail.AssertNoneAsync();
        Assert.Equal(MathB, env.S(s1).TeacherId);
    }

    [Fact(DisplayName = "SCD | Duyệt | Lịch dạy | tiết sau khi duyệt giống hệt tiết hiệu phó xử lý tay: trạng thái, giáo viên, nguồn, ghi chú, người sửa, khai báo gốc")]
    public async Task ApprovedSession_MatchesAManuallyResolvedSession()
    {
        var (envA, a1, a2, _) = Create();
        var (envB, b1, b2, _) = Create();
        var declaredA = await envA.HeadSvc.DeclareAsync(Declare(MathA));
        await envB.HeadSvc.DeclareAsync(Declare(MathA));
        await envA.HeadSvc.ProposeAsync(a1, Sub(NotHead));
        await envA.HeadSvc.ProposeAsync(a2, Cancel);

        await envA.Vice1Svc.ApproveAsync(declaredA.AbsenceId);
        await envB.Vice1Svc.ResolveAsync(b1, new ResolvePendingRequest("Substitute", NotHead));

        var approved = envA.S(a1);
        var manual = envB.S(b1);
        Assert.Equal((manual.Status, manual.TeacherId, manual.Source, manual.ChangeNote, manual.UpdatedByPersonId, manual.TeacherAbsenceId is not null),
            (approved.Status, approved.TeacherId, approved.Source, approved.ChangeNote, approved.UpdatedByPersonId, approved.TeacherAbsenceId is not null));
        Assert.Equal("Dạy thay: Giáo viên Toán không phải tổ trưởng", approved.ChangeNote);
        Assert.Equal(Vice1, approved.UpdatedByPersonId);
        Assert.Null(approved.ProposedAction);
    }

    [Fact(DisplayName = "SCD | Lịch dạy cũ | Tiết Chờ xử lý vì lý do khác (không có khai báo vắng) | tổ trưởng không thấy, không quyết định; hiệu phó vẫn xử lý tay như trước")]
    public async Task PendingWithoutAbsence_IsLeftToTheVice()
    {
        var (env, s1, _, _) = Create();
        var s = env.Db.TeachingSession.Find(s1)!;
        s.Status = "Pending";   // ví dụ tài khoản giáo viên bị khóa: tiết Chờ xử lý, chưa có khai báo vắng
        s.TeacherId = null;
        env.Db.SaveChanges();

        Assert.Empty(await env.HeadSvc.ListPendingForDepartmentAsync());
        Assert.Equal(400, await StatusOf(() => env.HeadSvc.ProposeAsync(s1, Sub(MathB))));
        await env.Vice1Svc.ResolveAsync(s1, new ResolvePendingRequest("Substitute", MathB));
        Assert.Equal(MathB, env.S(s1).TeacherId);
    }

    [Fact(DisplayName = "SCD | Lịch dạy cũ | Giáo viên bận hoặc vắng vẫn không được xếp dạy thay khi hiệu phó xử lý tay, như trước khi có đề xuất")]
    public async Task ManualResolve_StillEnforcesBusyAndAbsentRules()
    {
        var (env, s1, _, _) = Create();
        await env.HeadSvc.DeclareAsync(Declare(MathA));
        Session(env.Db, Class7A, MathSubject, MathB, Slot1);   // Toán B bận cùng khung giờ với tiết 1

        Assert.Equal(400, await StatusOf(() => env.Vice1Svc.ResolveAsync(s1, new ResolvePendingRequest("Substitute", MathB))));
        Assert.Equal(400, await StatusOf(() => env.Vice1Svc.ResolveAsync(s1, new ResolvePendingRequest("Substitute", MathA))));   // đang vắng
        Assert.Equal("Pending", env.S(s1).Status);
    }
}
