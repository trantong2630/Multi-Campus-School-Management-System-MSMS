using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Notifications;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Par.Dtos;
using MSMS.Api.Modules.Par.Services;

namespace MSMS.Tests.Modules.Par;

public class ConversationTests
{
    private sealed class FakeClock(DateOnly today, DateTime now) : IClock
    {
        public DateTime Now { get; set; } = now;
        public DateOnly Today { get; set; } = today;
    }

    private sealed class SimpleHttpContextAccessor(HttpContext context) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = context;
    }

    private static CurrentUser CreateCurrentUser(int personId, string accountType)
    {
        var claims = new[]
        {
            new Claim(CurrentUser.AccountIdClaim, personId.ToString()),
            new Claim(CurrentUser.PersonIdClaim, personId.ToString()),
            new Claim(CurrentUser.TypeClaim, accountType),
            new Claim(CurrentUser.SessionIdClaim, Guid.NewGuid().ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        return new CurrentUser(new SimpleHttpContextAccessor(httpContext));
    }

    private static MsmsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MsmsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new MsmsDbContext(options);
    }

    private const int Campus1 = 1;
    private const int AcademicYear1 = 1;
    private const int Semester1 = 1;

    private const int Class10A = 101;
    private const int Class10B = 102;

    private const int Teacher1StaffId = 10; // GVCN 10A
    private const int Teacher2StaffId = 20; // GV bộ môn Toán 10A
    private const int Teacher3StaffId = 30; // GV chỉ dạy 10B

    private const int Parent1PersonId = 100;
    private const int Student1PersonId = 200; // Học 10A

    private const int Parent2PersonId = 300;
    private const int Student2PersonId = 400; // Học 10B

    private const int SchoolAdminPersonId = 999;

    private static void SeedBaseData(MsmsDbContext db, DateOnly today)
    {
        db.Campus.Add(new Campus { CampusId = Campus1, Code = "CS1", Name = "Cơ sở 1", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.AcademicYear.Add(new AcademicYear { AcademicYearId = AcademicYear1, Name = "2026-2027", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2027, 5, 31), IsCurrent = true });
        db.Semester.Add(new Semester { SemesterId = Semester1, AcademicYearId = AcademicYear1, SemesterNo = 1, Name = "Học kỳ 1", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2027, 1, 15) });

        db.Class.AddRange(
            new Class { ClassId = Class10A, CampusId = Campus1, AcademicYearId = AcademicYear1, Name = "10A", GradeLevel = 10, IsActive = true, CreatedAt = DateTime.UtcNow },
            new Class { ClassId = Class10B, CampusId = Campus1, AcademicYearId = AcademicYear1, Name = "10B", GradeLevel = 10, IsActive = true, CreatedAt = DateTime.UtcNow }
        );

        // Teachers
        foreach (var (id, name, abbr) in new[]
        {
            (Teacher1StaffId, "Thầy Chủ Nhiệm", "CN"),
            (Teacher2StaffId, "Cô Dạy Toán", "TO"),
            (Teacher3StaffId, "Thầy Lớp Khác", "LK")
        })
        {
            db.Person.Add(new Person { PersonId = id, PersonType = "Staff", PersonCode = $"GV{id}", FullName = name, CreatedAt = DateTime.UtcNow });
            db.StaffProfile.Add(new StaffProfile { StaffId = id, PrimaryCampusId = Campus1, TeacherAbbr = abbr, IsActive = true });
        }

        // Parents & Students
        db.Person.AddRange(
            new Person { PersonId = Parent1PersonId, PersonType = "Parent", PersonCode = "PH100", FullName = "Phụ Huynh 1", CreatedAt = DateTime.UtcNow },
            new Person { PersonId = Student1PersonId, PersonType = "Student", PersonCode = "HS200", FullName = "Học Sinh 1", CreatedAt = DateTime.UtcNow },
            new Person { PersonId = Parent2PersonId, PersonType = "Parent", PersonCode = "PH300", FullName = "Phụ Huynh 2", CreatedAt = DateTime.UtcNow },
            new Person { PersonId = Student2PersonId, PersonType = "Student", PersonCode = "HS400", FullName = "Học Sinh 2", CreatedAt = DateTime.UtcNow },
            new Person { PersonId = SchoolAdminPersonId, PersonType = "Staff", PersonCode = "SA999", FullName = "School Administrator", CreatedAt = DateTime.UtcNow }
        );

        db.StudentProfile.AddRange(
            new StudentProfile { StudentId = Student1PersonId, Status = "Studying" },
            new StudentProfile { StudentId = Student2PersonId, Status = "Studying" }
        );

        db.ParentStudentLink.AddRange(
            new ParentStudentLink { LinkId = 1, ParentId = Parent1PersonId, StudentId = Student1PersonId, Relationship = "Bố", IsPrimary = true, CreatedAt = DateTime.UtcNow },
            new ParentStudentLink { LinkId = 2, ParentId = Parent2PersonId, StudentId = Student2PersonId, Relationship = "Mẹ", IsPrimary = true, CreatedAt = DateTime.UtcNow }
        );

        db.Enrollment.AddRange(
            new Enrollment { EnrollmentId = 1, StudentId = Student1PersonId, ClassId = Class10A, StartDate = new DateOnly(2026, 9, 1), EndDate = null, CreatedAt = DateTime.UtcNow },
            new Enrollment { EnrollmentId = 2, StudentId = Student2PersonId, ClassId = Class10B, StartDate = new DateOnly(2026, 9, 1), EndDate = null, CreatedAt = DateTime.UtcNow }
        );

        // Teacher 1 is Homeroom teacher of 10A
        db.HomeroomAssignment.Add(new HomeroomAssignment
        {
            Id = 1,
            ClassId = Class10A,
            StaffId = Teacher1StaffId,
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = null,
            CreatedAt = DateTime.UtcNow
        });

        // Timetable
        const int version1 = 1;
        const int subjectMath = 1;
        db.Subject.Add(new Subject { SubjectId = subjectMath, DepartmentId = 1, Name = "Toán", Abbreviation = "TOAN", IsActive = true });
        db.TimetableVersion.Add(new TimetableVersion
        {
            VersionId = version1,
            CampusId = Campus1,
            SemesterId = Semester1,
            Status = "Published",
            PublishedAt = new DateTime(2026, 9, 1),
            ImportedAt = DateTime.UtcNow
        });
        db.TimetableVersionClass.AddRange(
            new TimetableVersionClass { VersionId = version1, ClassId = Class10A },
            new TimetableVersionClass { VersionId = version1, ClassId = Class10B }
        );
        db.PeriodSlot.Add(new PeriodSlot { PeriodSlotId = 1, SessionOfDay = "Morning", PeriodNo = 1, StartTime = new TimeOnly(7, 30), EndTime = new TimeOnly(8, 15), IsActive = true });

        // Teacher 2 teaches Math in 10A
        db.TimetableEntry.Add(new TimetableEntry
        {
            EntryId = 1,
            VersionId = version1,
            ClassId = Class10A,
            DayOfWeek = 2,
            PeriodSlotId = 1,
            SubjectId = subjectMath,
            TeacherId = Teacher2StaffId
        });

        // Teacher 3 teaches Math in 10B
        db.TimetableEntry.Add(new TimetableEntry
        {
            EntryId = 2,
            VersionId = version1,
            ClassId = Class10B,
            DayOfWeek = 3,
            PeriodSlotId = 1,
            SubjectId = subjectMath,
            TeacherId = Teacher3StaffId
        });

        db.SaveChanges();
    }

    [Fact]
    public async Task Test1_ParentCannotCreateConversation_WithTeacherNotTeachingChild()
    {
        using var db = CreateDbContext();
        var today = new DateOnly(2026, 10, 24);
        var clock = new FakeClock(today, new DateTime(2026, 10, 24, 8, 0, 0));
        SeedBaseData(db, today);

        // Parent 1 cố gắng tạo hội thoại về Student 1 với Teacher 3 (chỉ dạy 10B, không dạy 10A)
        var parentUser = CreateCurrentUser(Parent1PersonId, AccountTypes.Parent);
        var notifier = new NotificationSender(db, clock);
        var service = new ConversationService(db, clock, parentUser, notifier);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            service.CreateOrGetConversationAsync(new CreateConversationDto(Student1PersonId, Teacher3StaffId, null)));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("không hợp lệ", ex.Message);
    }

    [Fact]
    public async Task Test2_TeacherCannotCreateConversation_WithParentOfOtherClass()
    {
        using var db = CreateDbContext();
        var today = new DateOnly(2026, 10, 24);
        var clock = new FakeClock(today, new DateTime(2026, 10, 24, 8, 0, 0));
        SeedBaseData(db, today);

        // Teacher 3 (chỉ dạy 10B) cố tạo hội thoại với Parent 1 của Student 1 (lớp 10A)
        var teacherUser = CreateCurrentUser(Teacher3StaffId, AccountTypes.Teacher);
        var notifier = new NotificationSender(db, clock);
        var service = new ConversationService(db, clock, teacherUser, notifier);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            service.CreateOrGetConversationAsync(new CreateConversationDto(Student1PersonId, null, Parent1PersonId)));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("không hợp lệ", ex.Message);
    }

    [Fact]
    public async Task Test3_BothParentAndTeacherCanCreateOrGetConversation_ReusesSingleConversationId()
    {
        using var db = CreateDbContext();
        var today = new DateOnly(2026, 10, 24);
        var clock = new FakeClock(today, new DateTime(2026, 10, 24, 8, 0, 0));
        SeedBaseData(db, today);

        var parentUser = CreateCurrentUser(Parent1PersonId, AccountTypes.Parent);
        var teacherUser = CreateCurrentUser(Teacher1StaffId, AccountTypes.Teacher);
        var notifier = new NotificationSender(db, clock);

        var parentService = new ConversationService(db, clock, parentUser, notifier);
        var teacherService = new ConversationService(db, clock, teacherUser, notifier);

        // Phụ huynh bấm tạo trước
        var convIdFromParent = await parentService.CreateOrGetConversationAsync(
            new CreateConversationDto(Student1PersonId, Teacher1StaffId, null));
        Assert.True(convIdFromParent > 0);

        // Giáo viên bấm tạo sau
        var convIdFromTeacher = await teacherService.CreateOrGetConversationAsync(
            new CreateConversationDto(Student1PersonId, null, Parent1PersonId));

        // Cả hai cùng nhận được đúng 1 ConversationId
        Assert.Equal(convIdFromParent, convIdFromTeacher);

        // Chỉ tồn tại đúng 1 record trong DB
        var totalConvs = await db.Conversation.CountAsync(c =>
            c.ParentId == Parent1PersonId && c.StaffId == Teacher1StaffId && c.StudentId == Student1PersonId);
        Assert.Equal(1, totalConvs);
    }

    [Fact]
    public async Task Test4_TeachingRelationshipEnded_ConversationBecomesReadOnly_BothDeniedFromSending()
    {
        using var db = CreateDbContext();
        var today = new DateOnly(2026, 10, 24);
        var clock = new FakeClock(today, new DateTime(2026, 10, 24, 8, 0, 0));
        SeedBaseData(db, today);

        var parentUser = CreateCurrentUser(Parent1PersonId, AccountTypes.Parent);
        var teacherUser = CreateCurrentUser(Teacher1StaffId, AccountTypes.Teacher);
        var notifier = new NotificationSender(db, clock);

        var parentService = new ConversationService(db, clock, parentUser, notifier);
        var teacherService = new ConversationService(db, clock, teacherUser, notifier);

        var convId = await parentService.CreateOrGetConversationAsync(
            new CreateConversationDto(Student1PersonId, Teacher1StaffId, null));

        // Khi quan hệ còn hiệu lực: gửi tin thành công
        var firstMsg = await parentService.SendMessageAsync(convId, "Tin nhắn khi còn giảng dạy");
        Assert.NotNull(firstMsg);

        // HẾT QUAN HỆ GIẢNG DẠY: GVCN kết thúc trước ngày hôm nay, và Teacher 1 không có tiết TKB 10A
        var homeroom = await db.HomeroomAssignment.FirstAsync(h => h.StaffId == Teacher1StaffId && h.ClassId == Class10A);
        homeroom.EndDate = today.AddDays(-1);
        await db.SaveChangesAsync();

        // 1. Phụ huynh cố gửi tin tiếp -> Bị từ chối 400
        var exParent = await Assert.ThrowsAsync<ApiException>(() =>
            parentService.SendMessageAsync(convId, "Phụ huynh gửi tin khi đã hết quan hệ"));
        Assert.Equal(400, exParent.StatusCode);
        Assert.Contains("chỉ đọc", exParent.Message);

        // 2. Giáo viên cố gửi tin tiếp -> Bị từ chối 400
        var exTeacher = await Assert.ThrowsAsync<ApiException>(() =>
            teacherService.SendMessageAsync(convId, "Giáo viên gửi tin khi đã hết quan hệ"));
        Assert.Equal(400, exTeacher.StatusCode);
        Assert.Contains("chỉ đọc", exTeacher.Message);

        // 3. Kiểm tra cờ IsReadOnly động khi lấy danh sách hội thoại
        var conversations = await parentService.GetMyConversationsAsync();
        var targetConv = Assert.Single(conversations, c => c.ConversationId == convId);
        Assert.True(targetConv.IsReadOnly);

        // 4. Lịch sử tin nhắn cũ vẫn xem được
        var messages = await parentService.GetMessagesAsync(convId);
        Assert.Single(messages);
        Assert.Equal("Tin nhắn khi còn giảng dạy", messages[0].Content);
    }

    [Fact]
    public async Task Test5_SchoolAdmin_BlockedFromAllMailboxApis_403Forbidden()
    {
        using var db = CreateDbContext();
        var today = new DateOnly(2026, 10, 24);
        var clock = new FakeClock(today, new DateTime(2026, 10, 24, 8, 0, 0));
        SeedBaseData(db, today);

        // Tạo sẵn 1 hội thoại giữa PH1 và GV1
        var parentUser = CreateCurrentUser(Parent1PersonId, AccountTypes.Parent);
        var notifier = new NotificationSender(db, clock);
        var parentService = new ConversationService(db, clock, parentUser, notifier);
        var convId = await parentService.CreateOrGetConversationAsync(new CreateConversationDto(Student1PersonId, Teacher1StaffId, null));

        // School Admin cố gắng truy cập mọi API
        var adminUser = CreateCurrentUser(SchoolAdminPersonId, AccountTypes.SchoolAdmin);
        var adminService = new ConversationService(db, clock, adminUser, notifier);

        var ex1 = await Assert.ThrowsAsync<ApiException>(() => adminService.GetAvailableTeachersAsync(Student1PersonId));
        Assert.Equal(403, ex1.StatusCode);

        var ex2 = await Assert.ThrowsAsync<ApiException>(() => adminService.GetMyTeachingStudentsAsync());
        Assert.Equal(403, ex2.StatusCode);

        var ex3 = await Assert.ThrowsAsync<ApiException>(() => adminService.GetAvailableParentsAsync(Student1PersonId));
        Assert.Equal(403, ex3.StatusCode);

        var ex4 = await Assert.ThrowsAsync<ApiException>(() => adminService.CreateOrGetConversationAsync(new CreateConversationDto(Student1PersonId, Teacher1StaffId, null)));
        Assert.Equal(403, ex4.StatusCode);

        var ex5 = await Assert.ThrowsAsync<ApiException>(() => adminService.GetMyConversationsAsync());
        Assert.Equal(403, ex5.StatusCode);

        var ex6 = await Assert.ThrowsAsync<ApiException>(() => adminService.GetTotalUnreadCountAsync());
        Assert.Equal(403, ex6.StatusCode);

        var ex7 = await Assert.ThrowsAsync<ApiException>(() => adminService.GetMessagesAsync(convId));
        Assert.Equal(403, ex7.StatusCode);

        var ex8 = await Assert.ThrowsAsync<ApiException>(() => adminService.SendMessageAsync(convId, "Tin nhắn từ admin"));
        Assert.Equal(403, ex8.StatusCode);
    }

    [Fact]
    public async Task Test6_Outsider_BlockedFromViewingOrSendingMessages_403Forbidden()
    {
        using var db = CreateDbContext();
        var today = new DateOnly(2026, 10, 24);
        var clock = new FakeClock(today, new DateTime(2026, 10, 24, 8, 0, 0));
        SeedBaseData(db, today);

        var parent1User = CreateCurrentUser(Parent1PersonId, AccountTypes.Parent);
        var notifier = new NotificationSender(db, clock);
        var parent1Service = new ConversationService(db, clock, parent1User, notifier);
        var convId = await parent1Service.CreateOrGetConversationAsync(new CreateConversationDto(Student1PersonId, Teacher1StaffId, null));

        // Người ngoài cuộc: Parent 2 (không thuộc hội thoại này)
        var outsiderUser = CreateCurrentUser(Parent2PersonId, AccountTypes.Parent);
        var outsiderService = new ConversationService(db, clock, outsiderUser, notifier);

        // 1. Xem tin nhắn của người khác -> 403 Forbidden
        var exView = await Assert.ThrowsAsync<ApiException>(() => outsiderService.GetMessagesAsync(convId));
        Assert.Equal(403, exView.StatusCode);

        // 2. Gửi tin nhắn vào hội thoại người khác -> 403 Forbidden
        var exSend = await Assert.ThrowsAsync<ApiException>(() => outsiderService.SendMessageAsync(convId, "Tin nhắn lạ"));
        Assert.Equal(403, exSend.StatusCode);

        // 3. Gọi hội thoại không tồn tại -> 404 NotFound
        var exNotFound = await Assert.ThrowsAsync<ApiException>(() => outsiderService.GetMessagesAsync(999999));
        Assert.Equal(404, exNotFound.StatusCode);
    }

    [Fact]
    public async Task Test7_SendMessage_UpdatesLastMessageAt_CreatesNotification_And_GetMessages_UpdatesReadAt()
    {
        using var db = CreateDbContext();
        var today = new DateOnly(2026, 10, 24);
        var sendTime = new DateTime(2026, 10, 24, 9, 15, 0);
        var clock = new FakeClock(today, sendTime);
        SeedBaseData(db, today);

        var parentUser = CreateCurrentUser(Parent1PersonId, AccountTypes.Parent);
        var teacherUser = CreateCurrentUser(Teacher1StaffId, AccountTypes.Teacher);
        var notifier = new NotificationSender(db, clock);

        var parentService = new ConversationService(db, clock, parentUser, notifier);
        var teacherService = new ConversationService(db, clock, teacherUser, notifier);

        var convId = await parentService.CreateOrGetConversationAsync(new CreateConversationDto(Student1PersonId, Teacher1StaffId, null));

        // 1. Phụ huynh gửi tin nhắn cho giáo viên
        var sentMessage = await parentService.SendMessageAsync(convId, "Em chào thầy, em muốn hỏi về bài kiểm tra");

        Assert.Equal("Em chào thầy, em muốn hỏi về bài kiểm tra", sentMessage.Content);
        Assert.True(sentMessage.IsMine);
        Assert.Null(sentMessage.ReadAt);
        Assert.Equal(sendTime, sentMessage.SentAt);

        // 2. CSDL kiểm tra Conversation.LastMessageAt được cập nhật
        var convInDb = await db.Conversation.FirstAsync(c => c.ConversationId == convId);
        Assert.Equal(sendTime, convInDb.LastMessageAt);

        // 3. Bản ghi Notification được tạo cho Giáo viên
        var notification = await db.Notification.FirstOrDefaultAsync(n =>
            n.RecipientPersonId == Teacher1StaffId && n.RefEntityId == convId);
        Assert.NotNull(notification);
        Assert.Equal("MessageReceived", notification.NotificationType);
        Assert.Equal("PAR", notification.RefEntityType);
        Assert.Contains("Phụ Huynh 1", notification.Title);

        // 4. Giáo viên có 1 tin chưa đọc
        var unreadCountBefore = await teacherService.GetTotalUnreadCountAsync();
        Assert.Equal(1, unreadCountBefore);

        // 5. Giáo viên mở xem tin nhắn -> Tự động đánh dấu ReadAt
        var readTime = new DateTime(2026, 10, 24, 9, 30, 0);
        clock.Now = readTime;

        var messages = await teacherService.GetMessagesAsync(convId);
        Assert.Single(messages);
        Assert.False(messages[0].IsMine); // Tin này của Phụ huynh gửi tới
        Assert.Equal(readTime, messages[0].ReadAt);

        // 6. CSDL đã cập nhật ReadAt
        var msgInDb = await db.Message.FirstAsync(m => m.MessageId == sentMessage.MessageId);
        Assert.Equal(readTime, msgInDb.ReadAt);

        // 7. Unread count của Giáo viên giảm về 0
        var unreadCountAfter = await teacherService.GetTotalUnreadCountAsync();
        Assert.Equal(0, unreadCountAfter);
    }
}
