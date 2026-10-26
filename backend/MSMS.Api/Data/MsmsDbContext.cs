using System;
using System.Collections.Generic;
using MSMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MSMS.Api.Data;

public partial class MsmsDbContext : DbContext
{
    public MsmsDbContext(DbContextOptions<MsmsDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AcademicYear> AcademicYear { get; set; }

    public virtual DbSet<Account> Account { get; set; }

    public virtual DbSet<AccountSession> AccountSession { get; set; }

    public virtual DbSet<AiFeedback> AiFeedback { get; set; }

    public virtual DbSet<Assignment> Assignment { get; set; }

    public virtual DbSet<AssignmentRecipient> AssignmentRecipient { get; set; }

    public virtual DbSet<AttendanceRecord> AttendanceRecord { get; set; }

    public virtual DbSet<AuditLog> AuditLog { get; set; }

    public virtual DbSet<Campus> Campus { get; set; }

    public virtual DbSet<CampusLeadership> CampusLeadership { get; set; }

    public virtual DbSet<Class> Class { get; set; }

    public virtual DbSet<Conversation> Conversation { get; set; }

    public virtual DbSet<DepartmentHead> DepartmentHead { get; set; }

    public virtual DbSet<Enrollment> Enrollment { get; set; }

    public virtual DbSet<Exam> Exam { get; set; }

    public virtual DbSet<ExamQuestion> ExamQuestion { get; set; }

    public virtual DbSet<Holiday> Holiday { get; set; }

    public virtual DbSet<HomeroomAssignment> HomeroomAssignment { get; set; }

    public virtual DbSet<LeaveRequest> LeaveRequest { get; set; }

    public virtual DbSet<Message> Message { get; set; }

    public virtual DbSet<Notification> Notification { get; set; }

    public virtual DbSet<ParentStudentLink> ParentStudentLink { get; set; }

    public virtual DbSet<PasswordResetOtp> PasswordResetOtp { get; set; }

    public virtual DbSet<PeriodSlot> PeriodSlot { get; set; }

    public virtual DbSet<Person> Person { get; set; }

    public virtual DbSet<Post> Post { get; set; }

    public virtual DbSet<PostImage> PostImage { get; set; }

    public virtual DbSet<Question> Question { get; set; }

    public virtual DbSet<QuestionOption> QuestionOption { get; set; }

    public virtual DbSet<SchoolProfile> SchoolProfile { get; set; }

    public virtual DbSet<Semester> Semester { get; set; }

    public virtual DbSet<StaffProfile> StaffProfile { get; set; }

    public virtual DbSet<StudentProfile> StudentProfile { get; set; }

    public virtual DbSet<Subject> Subject { get; set; }

    public virtual DbSet<SubjectDepartment> SubjectDepartment { get; set; }

    public virtual DbSet<Submission> Submission { get; set; }

    public virtual DbSet<SubmissionAnswer> SubmissionAnswer { get; set; }

    public virtual DbSet<TeacherAbsence> TeacherAbsence { get; set; }

    public virtual DbSet<TeachingSession> TeachingSession { get; set; }

    public virtual DbSet<TimetableEntry> TimetableEntry { get; set; }

    public virtual DbSet<TimetableVersionClass> TimetableVersionClass { get; set; }

    public virtual DbSet<TimetableVersion> TimetableVersion { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("Vietnamese_CI_AS");

        modelBuilder.Entity<AcademicYear>(entity =>
        {
            entity.HasKey(e => e.AcademicYearId).HasName("PK__Academic__C54C7A0198EA872A");

            entity.HasIndex(e => e.Name, "UQ__Academic__737584F647FE2DE1").IsUnique();

            entity.Property(e => e.Name).HasMaxLength(20);
        });

        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.AccountId).HasName("PK__Account__349DA5A675E7E759");

            entity.HasIndex(e => e.Username, "UQ__Account__536C85E403EE4037").IsUnique();

            entity.HasIndex(e => e.PersonId, "UQ__Account__AA2FFBE481262702").IsUnique();

            entity.Property(e => e.AccountType).HasMaxLength(20);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");
            entity.Property(e => e.LastLoginAt).HasPrecision(0);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.Status)
                .HasMaxLength(25)
                .HasDefaultValue("PendingPasswordChange");
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
            entity.Property(e => e.Username).HasMaxLength(50);

            entity.HasOne(d => d.Person).WithOne(p => p.Account)
                .HasForeignKey<Account>(d => d.PersonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Account__PersonI__10566F31");
        });

        modelBuilder.Entity<AccountSession>(entity =>
        {
            entity.HasKey(e => e.SessionId);

            entity.HasIndex(e => e.AccountId, "IX_AccountSession_Account");

            entity.HasIndex(e => e.RefreshTokenHash, "IX_AccountSession_RefreshToken");

            entity.Property(e => e.CreatedAt).HasPrecision(0);
            entity.Property(e => e.ExpiresAt).HasPrecision(0);
            entity.Property(e => e.LastUsedAt).HasPrecision(0);
            entity.Property(e => e.PreviousRefreshTokenHash).HasMaxLength(64);
            entity.Property(e => e.RefreshTokenHash).HasMaxLength(64);
            entity.Property(e => e.RevokedAt).HasPrecision(0);
            entity.Property(e => e.UserAgent).HasMaxLength(300);

            entity.HasOne(d => d.Account).WithMany(p => p.AccountSession)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<AiFeedback>(entity =>
        {
            entity.HasKey(e => e.FeedbackId).HasName("PK__AiFeedba__6A4BEDD6EF230D1E");

            entity.HasIndex(e => new { e.AssignmentId, e.StudentId }, "UQ_AiFeedback").IsUnique();

            entity.Property(e => e.AiContent).HasMaxLength(2000);
            entity.Property(e => e.ApprovedAt).HasPrecision(0);
            entity.Property(e => e.EditedContent).HasMaxLength(2000);
            entity.Property(e => e.ErrorMessage).HasMaxLength(500);
            entity.Property(e => e.GeneratedAt).HasPrecision(0);
            entity.Property(e => e.PublishedAt).HasPrecision(0);
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .HasDefaultValue("Draft");

            entity.HasOne(d => d.ApprovedByPerson).WithMany(p => p.AiFeedback)
                .HasForeignKey(d => d.ApprovedByPersonId)
                .HasConstraintName("FK__AiFeedbac__Appro__0B5CAFEA");

            entity.HasOne(d => d.Assignment).WithMany(p => p.AiFeedback)
                .HasForeignKey(d => d.AssignmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__AiFeedbac__Assig__0880433F");

            entity.HasOne(d => d.Student).WithMany(p => p.AiFeedback)
                .HasForeignKey(d => d.StudentId)
                .HasConstraintName("FK__AiFeedbac__Stude__09746778");
        });

        modelBuilder.Entity<Assignment>(entity =>
        {
            entity.HasKey(e => e.AssignmentId).HasName("PK__Assignme__32499E77EF005FDB");

            entity.HasIndex(e => new { e.ClassId, e.Status, e.DueAt }, "IX_Assignment_Class");

            entity.HasIndex(e => e.ExamId, "IX_Assignment_Exam");

            entity.Property(e => e.AssignedAt).HasPrecision(0);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");
            entity.Property(e => e.DueAt).HasPrecision(0);
            entity.Property(e => e.Instruction).HasMaxLength(500);
            entity.Property(e => e.OpenAt).HasPrecision(0);
            entity.Property(e => e.ScorePublishedAt).HasPrecision(0);
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .HasDefaultValue("Draft");
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.AssignedByPerson).WithMany(p => p.AssignmentAssignedByPerson)
                .HasForeignKey(d => d.AssignedByPersonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Assignmen__Assig__7755B73D");

            entity.HasOne(d => d.Class).WithMany(p => p.Assignment)
                .HasForeignKey(d => d.ClassId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Assignmen__Class__756D6ECB");

            entity.HasOne(d => d.Exam).WithMany(p => p.Assignment)
                .HasForeignKey(d => d.ExamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Assignmen__ExamI__74794A92");

            entity.HasOne(d => d.ScorePublishedByPerson).WithMany(p => p.AssignmentScorePublishedByPerson)
                .HasForeignKey(d => d.ScorePublishedByPersonId)
                .HasConstraintName("FK__Assignmen__Score__7849DB76");
        });

        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.HasKey(e => e.AttendanceId).HasName("PK__Attendan__8B69261CA0EC7B5F");

            entity.HasIndex(e => new { e.StudentId, e.RecordedAt }, "IX_Attendance_Student");

            entity.HasIndex(e => new { e.SessionId, e.StudentId }, "UQ_Attendance").IsUnique();

            entity.Property(e => e.Note).HasMaxLength(255);
            entity.Property(e => e.RecordedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");
            entity.Property(e => e.Status).HasMaxLength(10);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);

            entity.HasOne(d => d.RecordedByPerson).WithMany(p => p.AttendanceRecordRecordedByPerson)
                .HasForeignKey(d => d.RecordedByPersonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Attendanc__Recor__56E8E7AB");

            entity.HasOne(d => d.Session).WithMany(p => p.AttendanceRecord)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Attendanc__Sessi__540C7B00");

            entity.HasOne(d => d.Student).WithMany(p => p.AttendanceRecord)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Attendanc__Stude__55009F39");

            entity.HasOne(d => d.UpdatedByPerson).WithMany(p => p.AttendanceRecordUpdatedByPerson)
                .HasForeignKey(d => d.UpdatedByPersonId)
                .HasConstraintName("FK__Attendanc__Updat__58D1301D");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditId).HasName("PK__AuditLog__A17F2398232493FD");

            entity.HasIndex(e => new { e.ActorPersonId, e.CreatedAt }, "IX_Audit_Actor").IsDescending(false, true);

            entity.HasIndex(e => new { e.Category, e.CreatedAt }, "IX_Audit_Filter").IsDescending(false, true);

            entity.Property(e => e.Action).HasMaxLength(50);
            entity.Property(e => e.Category).HasMaxLength(30);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");

            entity.HasOne(d => d.ActorPerson).WithMany(p => p.AuditLog)
                .HasForeignKey(d => d.ActorPersonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__AuditLog__ActorP__308E3499");
        });

        modelBuilder.Entity<Campus>(entity =>
        {
            entity.HasKey(e => e.CampusId).HasName("PK__Campus__FD598DD65E4BAAB7");

            entity.HasIndex(e => e.Name, "UQ__Campus__737584F6F80B5FC0").IsUnique();

            entity.HasIndex(e => e.Code, "UQ__Campus__A25C5AA776F207BE").IsUnique();

            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.Code).HasMaxLength(20);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
        });

        modelBuilder.Entity<CampusLeadership>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CampusLe__3214EC0724016194");

            entity.HasIndex(e => new { e.CampusId, e.StartDate, e.EndDate }, "IX_CampusLeadership");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");

            entity.HasOne(d => d.AssignedByPerson).WithMany(p => p.CampusLeadership)
                .HasForeignKey(d => d.AssignedByPersonId)
                .HasConstraintName("FK__CampusLea__Assig__1BC821DD");

            entity.HasOne(d => d.Campus).WithMany(p => p.CampusLeadership)
                .HasForeignKey(d => d.CampusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CampusLea__Campu__19DFD96B");

            entity.HasOne(d => d.Staff).WithMany(p => p.CampusLeadership)
                .HasForeignKey(d => d.StaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CampusLea__Staff__1AD3FDA4");
        });

        modelBuilder.Entity<Class>(entity =>
        {
            entity.HasKey(e => e.ClassId).HasName("PK__Class__CB1927C0907630C5");

            entity.HasIndex(e => new { e.CampusId, e.AcademicYearId, e.Name }, "UQ_Class").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(50);

            entity.HasOne(d => d.AcademicYear).WithMany(p => p.Class)
                .HasForeignKey(d => d.AcademicYearId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Class__AcademicY__6FE99F9F");

            entity.HasOne(d => d.Campus).WithMany(p => p.Class)
                .HasForeignKey(d => d.CampusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Class__CampusId__6EF57B66");

            entity.HasOne(d => d.CopiedFromClass).WithMany(p => p.InverseCopiedFromClass)
                .HasForeignKey(d => d.CopiedFromClassId)
                .HasConstraintName("FK__Class__CopiedFro__71D1E811");
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.ConversationId).HasName("PK__Conversa__C050D877AD622E4F");

            entity.HasIndex(e => new { e.StaffId, e.LastMessageAt }, "IX_Conversation_Staff").IsDescending(false, true);

            entity.HasIndex(e => new { e.ParentId, e.StaffId, e.StudentId }, "UQ_Conversation").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");
            entity.Property(e => e.LastMessageAt).HasPrecision(0);

            entity.HasOne(d => d.Parent).WithMany(p => p.Conversation)
                .HasForeignKey(d => d.ParentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Conversat__Paren__16CE6296");

            entity.HasOne(d => d.Staff).WithMany(p => p.Conversation)
                .HasForeignKey(d => d.StaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Conversat__Staff__17C286CF");

            entity.HasOne(d => d.Student).WithMany(p => p.Conversation)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Conversat__Stude__18B6AB08");
        });

        modelBuilder.Entity<DepartmentHead>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Departme__3214EC071FF6F269");

            entity.HasIndex(e => new { e.DepartmentId, e.StartDate, e.EndDate }, "IX_DepartmentHead");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");

            entity.HasOne(d => d.AssignedByPerson).WithMany(p => p.DepartmentHead)
                .HasForeignKey(d => d.AssignedByPersonId)
                .HasConstraintName("FK__Departmen__Assig__2180FB33");

            entity.HasOne(d => d.Department).WithMany(p => p.DepartmentHead)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Departmen__Depar__1F98B2C1");

            entity.HasOne(d => d.Staff).WithMany(p => p.DepartmentHead)
                .HasForeignKey(d => d.StaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Departmen__Staff__208CD6FA");
        });

        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.HasKey(e => e.EnrollmentId).HasName("PK__Enrollme__7F68771B3F3CD5D5");

            entity.HasIndex(e => new { e.ClassId, e.StartDate }, "IX_Enrollment_Class");

            entity.HasIndex(e => new { e.StudentId, e.StartDate }, "IX_Enrollment_Student");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");
            entity.Property(e => e.Reason).HasMaxLength(255);

            entity.HasOne(d => d.Class).WithMany(p => p.Enrollment)
                .HasForeignKey(d => d.ClassId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Enrollmen__Class__0A9D95DB");

            entity.HasOne(d => d.Student).WithMany(p => p.Enrollment)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Enrollmen__Stude__09A971A2");
        });

        modelBuilder.Entity<Exam>(entity =>
        {
            entity.HasKey(e => e.ExamId).HasName("PK__Exam__297521C73FAE556B");

            entity.HasIndex(e => e.OwnerPersonId, "IX_Exam_Owner");

            entity.HasIndex(e => e.SubjectId, "IX_Exam_Subject");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);

            entity.HasOne(d => d.CopiedFromExam).WithMany(p => p.InverseCopiedFromExam)
                .HasForeignKey(d => d.CopiedFromExamId)
                .HasConstraintName("FK__Exam__CopiedFrom__6AEFE058");

            entity.HasOne(d => d.OwnerPerson).WithMany(p => p.Exam)
                .HasForeignKey(d => d.OwnerPersonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Exam__OwnerPerso__690797E6");

            entity.HasOne(d => d.Subject).WithMany(p => p.Exam)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Exam__SubjectId__681373AD");
        });

        modelBuilder.Entity<ExamQuestion>(entity =>
        {
            entity.HasKey(e => e.ExamQuestionId).HasName("PK__ExamQues__EFAED846C9BE138C");

            entity.HasIndex(e => new { e.ExamId, e.QuestionId }, "UQ_ExamQuestion").IsUnique();

            entity.Property(e => e.Points)
                .HasDefaultValue(1m)
                .HasColumnType("decimal(4, 2)");

            entity.HasOne(d => d.Exam).WithMany(p => p.ExamQuestion)
                .HasForeignKey(d => d.ExamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ExamQuest__ExamI__6FB49575");

            entity.HasOne(d => d.Question).WithMany(p => p.ExamQuestion)
                .HasForeignKey(d => d.QuestionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ExamQuest__Quest__70A8B9AE");
        });

        modelBuilder.Entity<Holiday>(entity =>
        {
            entity.HasKey(e => e.HolidayId).HasName("PK__Holiday__2D35D57ABFAFCD9B");

            entity.HasIndex(e => new { e.AcademicYearId, e.HolidayDate, e.CampusId }, "UQ_Holiday").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");
            entity.Property(e => e.Reason).HasMaxLength(255);

            entity.HasOne(d => d.AcademicYear).WithMany(p => p.Holiday)
                .HasForeignKey(d => d.AcademicYearId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Holiday__Academi__68487DD7");

            entity.HasOne(d => d.Campus).WithMany(p => p.Holiday)
                .HasForeignKey(d => d.CampusId)
                .HasConstraintName("FK__Holiday__CampusI__693CA210");

            entity.HasOne(d => d.CreatedByPerson).WithMany(p => p.Holiday)
                .HasForeignKey(d => d.CreatedByPersonId)
                .HasConstraintName("FK_Holiday_CreatedBy");
        });

        modelBuilder.Entity<HomeroomAssignment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Homeroom__3214EC0708B8D1B0");

            entity.HasIndex(e => new { e.ClassId, e.StartDate, e.EndDate }, "IX_Homeroom_Class");

            entity.HasIndex(e => new { e.StaffId, e.StartDate, e.EndDate }, "IX_Homeroom_Staff");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");

            entity.HasOne(d => d.AssignedByPerson).WithMany(p => p.HomeroomAssignment)
                .HasForeignKey(d => d.AssignedByPersonId)
                .HasConstraintName("FK__HomeroomA__Assig__2739D489");

            entity.HasOne(d => d.Class).WithMany(p => p.HomeroomAssignment)
                .HasForeignKey(d => d.ClassId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__HomeroomA__Class__25518C17");

            entity.HasOne(d => d.Staff).WithMany(p => p.HomeroomAssignment)
                .HasForeignKey(d => d.StaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__HomeroomA__Staff__2645B050");
        });

        modelBuilder.Entity<LeaveRequest>(entity =>
        {
            entity.HasKey(e => e.LeaveRequestId).HasName("PK__LeaveReq__609421EEE1310F83");

            entity.HasIndex(e => new { e.ClassId, e.Status }, "IX_Leave_Class");

            entity.HasIndex(e => new { e.StudentId, e.Status, e.FromDate, e.ToDate }, "IX_Leave_Student");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");
            entity.Property(e => e.DecidedAt).HasPrecision(0);
            entity.Property(e => e.DecisionNote).HasMaxLength(255);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.SessionOfDay).HasMaxLength(10);
            entity.Property(e => e.Status)
                .HasMaxLength(15)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.Class).WithMany(p => p.LeaveRequest)
                .HasForeignKey(d => d.ClassId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__LeaveRequ__Class__10216507");

            entity.HasOne(d => d.DecidedByPerson).WithMany(p => p.LeaveRequestDecidedByPerson)
                .HasForeignKey(d => d.DecidedByPersonId)
                .HasConstraintName("FK__LeaveRequ__Decid__12FDD1B2");

            entity.HasOne(d => d.Parent).WithMany(p => p.LeaveRequestParent)
                .HasForeignKey(d => d.ParentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__LeaveRequ__Paren__0F2D40CE");

            entity.HasOne(d => d.Student).WithMany(p => p.LeaveRequest)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__LeaveRequ__Stude__0E391C95");
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.MessageId).HasName("PK__Message__C87C0C9C73DD1991");

            entity.HasIndex(e => new { e.ConversationId, e.SentAt }, "IX_Message_Conversation").IsDescending(false, true);

            entity.Property(e => e.Content).HasMaxLength(2000);
            entity.Property(e => e.ReadAt).HasPrecision(0);
            entity.Property(e => e.SentAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");

            entity.HasOne(d => d.Conversation).WithMany(p => p.Message)
                .HasForeignKey(d => d.ConversationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Message__Convers__1C873BEC");

            entity.HasOne(d => d.SenderPerson).WithMany(p => p.Message)
                .HasForeignKey(d => d.SenderPersonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Message__SenderP__1D7B6025");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E120F9DC25B");

            entity.HasIndex(e => new { e.RecipientPersonId, e.IsRead, e.CreatedAt }, "IX_Notification_Inbox").IsDescending(false, false, true);

            entity.Property(e => e.Body).HasMaxLength(500);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");
            entity.Property(e => e.NotificationType).HasMaxLength(40);
            entity.Property(e => e.ReadAt).HasPrecision(0);
            entity.Property(e => e.RefEntityType).HasMaxLength(40);
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.RecipientPerson).WithMany(p => p.Notification)
                .HasForeignKey(d => d.RecipientPersonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Notificat__Recip__214BF109");
        });

        modelBuilder.Entity<ParentStudentLink>(entity =>
        {
            entity.HasKey(e => e.LinkId).HasName("PK__ParentSt__2D122135D4896914");

            entity.HasIndex(e => e.StudentId, "IX_Link_Student");

            entity.HasIndex(e => new { e.ParentId, e.StudentId }, "UQ_ParentStudentLink").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");
            entity.Property(e => e.IsPrimary).HasDefaultValue(true);
            entity.Property(e => e.Relationship).HasMaxLength(30);

            entity.HasOne(d => d.Parent).WithMany(p => p.ParentStudentLink)
                .HasForeignKey(d => d.ParentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ParentStu__Paren__03F0984C");

            entity.HasOne(d => d.Student).WithMany(p => p.ParentStudentLink)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ParentStu__Stude__04E4BC85");
        });

        modelBuilder.Entity<PasswordResetOtp>(entity =>
        {
            entity.HasKey(e => e.OtpId).HasName("PK__Password__3143C4A36BFD67E8");

            entity.HasIndex(e => new { e.AccountId, e.CreatedAt }, "IX_Otp_Account").IsDescending(false, true);

            entity.Property(e => e.ConsumedAt).HasPrecision(0);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");
            entity.Property(e => e.ExpiresAt).HasPrecision(0);
            entity.Property(e => e.OtpHash).HasMaxLength(255);

            entity.HasOne(d => d.Account).WithMany(p => p.PasswordResetOtp)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PasswordR__Accou__151B244E");
        });

        modelBuilder.Entity<PeriodSlot>(entity =>
        {
            entity.HasKey(e => e.PeriodSlotId).HasName("PK__PeriodSl__EBCA05D78951A712");

            entity.HasIndex(e => new { e.SessionOfDay, e.PeriodNo }, "UQ_PeriodSlot").IsUnique();

            entity.Property(e => e.EndTime).HasPrecision(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.SessionOfDay).HasMaxLength(10);
            entity.Property(e => e.StartTime).HasPrecision(0);
        });

        modelBuilder.Entity<Person>(entity =>
        {
            entity.HasKey(e => e.PersonId).HasName("PK__Person__AA2FFBE5D0917B03");

            entity.HasIndex(e => new { e.FullName, e.DateOfBirth }, "IX_Person_Identify");

            entity.HasIndex(e => e.PhoneNumber, "IX_Person_Phone");

            entity.HasIndex(e => e.NationalId, "UQ_Person_NationalId").IsUnique().HasFilter("([NationalId] IS NOT NULL)");

            entity.HasIndex(e => e.PersonCode, "UQ__Person__7B5569ED2848A4AA").IsUnique();

            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.AvatarPath).HasMaxLength(500);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.Gender).HasMaxLength(10);
            entity.Property(e => e.NationalId).HasMaxLength(12).IsUnicode(false).IsFixedLength();
            entity.Property(e => e.PersonCode).HasMaxLength(10);
            entity.Property(e => e.PersonType).HasMaxLength(10);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
        });

        modelBuilder.Entity<Post>(entity =>
        {
            entity.HasKey(e => e.PostId).HasName("PK__Post__AA12601895745049");

            entity.HasIndex(e => new { e.Status, e.Visibility, e.CreatedAt }, "IX_Post_Feed").IsDescending(false, false, true);

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");
            entity.Property(e => e.HiddenAt).HasPrecision(0);
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .HasDefaultValue("Posted");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
            entity.Property(e => e.Visibility).HasMaxLength(10);

            entity.HasOne(d => d.AuthorPerson).WithMany(p => p.PostAuthorPerson)
                .HasForeignKey(d => d.AuthorPersonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Post__AuthorPers__2610A626");

            entity.HasOne(d => d.HiddenByPerson).WithMany(p => p.PostHiddenByPerson)
                .HasForeignKey(d => d.HiddenByPersonId)
                .HasConstraintName("FK__Post__HiddenByPe__28ED12D1");
        });

        modelBuilder.Entity<PostImage>(entity =>
        {
            entity.HasKey(e => e.PostImageId).HasName("PK__PostImag__BCD3CCD0393636DA");

            entity.HasIndex(e => new { e.PostId, e.OrderNo }, "IX_PostImage_Post");

            entity.Property(e => e.FilePath).HasMaxLength(500);
            entity.Property(e => e.OrderNo).HasDefaultValue((byte)1);
            entity.Property(e => e.UploadedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");

            entity.HasOne(d => d.Post).WithMany(p => p.PostImage)
                .HasForeignKey(d => d.PostId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PostImage__PostI__2BC97F7C");
        });

        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasKey(e => e.QuestionId).HasName("PK__Question__0DC06FAC89AD8931");

            entity.HasIndex(e => new { e.SubjectId, e.Status, e.IsHidden }, "IX_Question_Bank");

            entity.Property(e => e.Content).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");
            entity.Property(e => e.Explanation).HasMaxLength(2000);
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .HasDefaultValue("Draft");
            entity.Property(e => e.Topic).HasMaxLength(150);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);

            entity.HasOne(d => d.CreatedByPerson).WithMany(p => p.Question)
                .HasForeignKey(d => d.CreatedByPersonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Question__Create__5F7E2DAC");

            entity.HasOne(d => d.Subject).WithMany(p => p.Question)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Question__Subjec__5BAD9CC8");
        });

        modelBuilder.Entity<QuestionOption>(entity =>
        {
            entity.HasKey(e => e.OptionId).HasName("PK__Question__92C7A1FFF146A0D9");

            entity.HasIndex(e => new { e.QuestionId, e.OptionLabel }, "UQ_QuestionOption").IsUnique();

            entity.Property(e => e.Content).HasMaxLength(1000);
            entity.Property(e => e.OptionLabel)
                .HasMaxLength(1)
                .IsFixedLength();

            entity.HasOne(d => d.Question).WithMany(p => p.QuestionOption)
                .HasForeignKey(d => d.QuestionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__QuestionO__Quest__6442E2C9");
        });

        modelBuilder.Entity<SchoolProfile>(entity =>
        {
            entity.HasKey(e => e.SchoolProfileId).HasName("PK__SchoolPr__42620EDA11B7E046");

            entity.Property(e => e.SchoolProfileId).ValueGeneratedNever();
            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.LogoPath).HasMaxLength(500);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
            entity.Property(e => e.Website).HasMaxLength(200);

            entity.HasOne(d => d.UpdatedByPerson).WithMany(p => p.SchoolProfile)
                .HasForeignKey(d => d.UpdatedByPersonId)
                .HasConstraintName("FK_SchoolProfile_UpdatedBy");
        });

        modelBuilder.Entity<Semester>(entity =>
        {
            entity.HasKey(e => e.SemesterId).HasName("PK__Semester__043301DD0088C9E3");

            entity.HasIndex(e => new { e.AcademicYearId, e.SemesterNo }, "UQ_Semester").IsUnique();

            entity.Property(e => e.Name).HasMaxLength(50);

            entity.HasOne(d => d.AcademicYear).WithMany(p => p.Semester)
                .HasForeignKey(d => d.AcademicYearId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Semester__Academ__656C112C");
        });

        modelBuilder.Entity<StaffProfile>(entity =>
        {
            entity.HasKey(e => e.StaffId).HasName("PK__StaffPro__96D4AB17928573D0");

            entity.HasIndex(e => e.TeacherAbbr, "UQ_StaffProfile_Abbr")
                .IsUnique()
                .HasFilter("([TeacherAbbr] IS NOT NULL)");

            entity.Property(e => e.StaffId).ValueGeneratedNever();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Position).HasMaxLength(100);
            entity.Property(e => e.TeacherAbbr).HasMaxLength(10);

            entity.HasOne(d => d.Department).WithMany(p => p.StaffProfile)
                .HasForeignKey(d => d.DepartmentId)
                .HasConstraintName("FK__StaffProf__Depar__7F2BE32F");

            entity.HasOne(d => d.PrimaryCampus).WithMany(p => p.StaffProfile)
                .HasForeignKey(d => d.PrimaryCampusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StaffProf__Prima__7E37BEF6");

            entity.HasOne(d => d.Staff).WithOne(p => p.StaffProfile)
                .HasForeignKey<StaffProfile>(d => d.StaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StaffProf__Staff__7D439ABD");
        });

        modelBuilder.Entity<StudentProfile>(entity =>
        {
            entity.HasKey(e => e.StudentId).HasName("PK__StudentP__32C52B996A753723");

            entity.Property(e => e.StudentId).ValueGeneratedNever();
            entity.Property(e => e.Note).HasMaxLength(255);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Studying");

            entity.HasOne(d => d.Student).WithOne(p => p.StudentProfile)
                .HasForeignKey<StudentProfile>(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StudentPr__Stude__797309D9");
        });

        modelBuilder.Entity<Subject>(entity =>
        {
            entity.HasKey(e => e.SubjectId).HasName("PK__Subject__AC1BA3A8B189A9AB");

            entity.HasIndex(e => e.Name, "UQ__Subject__737584F642EFF95A").IsUnique();

            entity.HasIndex(e => e.Abbreviation, "UQ__Subject__9C41170ED2F47C9B").IsUnique();

            entity.Property(e => e.Abbreviation).HasMaxLength(10);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(150);

            entity.HasOne(d => d.Department).WithMany(p => p.Subject)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Subject__Departm__59063A47");
        });

        modelBuilder.Entity<SubjectDepartment>(entity =>
        {
            entity.HasKey(e => e.DepartmentId).HasName("PK__SubjectD__B2079BEDD9888346");

            entity.HasIndex(e => e.Name, "UQ__SubjectD__737584F6FAF3A0BA").IsUnique();

            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(150);
        });

        modelBuilder.Entity<AssignmentRecipient>(entity =>
        {
            entity.HasKey(e => new { e.AssignmentId, e.StudentId });

            entity.HasIndex(e => e.StudentId, "IX_AssignmentRecipient_Student");

            entity.HasOne(d => d.Assignment).WithMany(p => p.AssignmentRecipient)
                .HasForeignKey(d => d.AssignmentId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Student).WithMany(p => p.AssignmentRecipient)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Submission>(entity =>
        {
            entity.HasKey(e => e.SubmissionId).HasName("PK__Submissi__449EE1252F0D5C2F");

            entity.HasIndex(e => new { e.StudentId, e.SubmittedAt }, "IX_Submission_Student");

            entity.HasIndex(e => new { e.AssignmentId, e.StudentId }, "UQ_Submission").IsUnique();

            entity.Property(e => e.LastSavedAt).HasPrecision(0);
            entity.Property(e => e.Score).HasColumnType("decimal(4, 2)");
            entity.Property(e => e.StartedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");
            entity.Property(e => e.SubmittedAt).HasPrecision(0);

            entity.HasOne(d => d.Assignment).WithMany(p => p.Submission)
                .HasForeignKey(d => d.AssignmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Submissio__Assig__7D0E9093");

            entity.HasOne(d => d.Student).WithMany(p => p.Submission)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Submissio__Stude__7E02B4CC");
        });

        modelBuilder.Entity<SubmissionAnswer>(entity =>
        {
            entity.HasKey(e => e.AnswerId).HasName("PK__Submissi__D4825004190B923E");

            entity.HasIndex(e => new { e.SubmissionId, e.ExamQuestionId }, "UQ_SubmissionAnswer").IsUnique();

            entity.Property(e => e.EarnedPoints).HasColumnType("decimal(4, 2)");

            entity.HasOne(d => d.ExamQuestion).WithMany(p => p.SubmissionAnswer)
                .HasForeignKey(d => d.ExamQuestionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Submissio__ExamQ__03BB8E22");

            entity.HasOne(d => d.SelectedOption).WithMany(p => p.SubmissionAnswer)
                .HasForeignKey(d => d.SelectedOptionId)
                .HasConstraintName("FK__Submissio__Selec__04AFB25B");

            entity.HasOne(d => d.Submission).WithMany(p => p.SubmissionAnswer)
                .HasForeignKey(d => d.SubmissionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Submissio__Submi__02C769E9");
        });

        modelBuilder.Entity<TeacherAbsence>(entity =>
        {
            entity.HasKey(e => e.AbsenceId).HasName("PK__TeacherA__3A074E674F278659");

            entity.HasIndex(e => new { e.TeacherId, e.FromDate, e.ToDate }, "IX_Absence_Teacher");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");
            entity.Property(e => e.Reason).HasMaxLength(255);
            entity.Property(e => e.SlotIds).HasMaxLength(200);

            entity.HasOne(d => d.DeclaredByPerson).WithMany(p => p.TeacherAbsence)
                .HasForeignKey(d => d.DeclaredByPersonId)
                .HasConstraintName("FK__TeacherAb__Decla__4C6B5938");

            entity.HasOne(d => d.Teacher).WithMany(p => p.TeacherAbsence)
                .HasForeignKey(d => d.TeacherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TeacherAb__Teach__4B7734FF");
        });

        modelBuilder.Entity<TeachingSession>(entity =>
        {
            entity.HasKey(e => e.SessionId).HasName("PK__Teaching__C9F49290E9E86E45");

            entity.HasIndex(e => new { e.ClassId, e.SessionDate, e.PeriodSlotId }, "IX_Session_Class");

            entity.HasIndex(e => new { e.Status, e.SessionDate }, "IX_Session_Status");

            entity.HasIndex(e => new { e.TeacherId, e.SessionDate, e.PeriodSlotId }, "IX_Session_Teacher");

            entity.Property(e => e.ChangeNote).HasMaxLength(255);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");
            entity.Property(e => e.Source)
                .HasMaxLength(15)
                .HasDefaultValue("Generated");
            entity.Property(e => e.Status)
                .HasMaxLength(15)
                .HasDefaultValue("Scheduled");
            entity.Property(e => e.UpdatedAt).HasPrecision(0);

            entity.HasOne(d => d.Class).WithMany(p => p.TeachingSession)
                .HasForeignKey(d => d.ClassId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TeachingS__Class__40F9A68C");

            entity.HasOne(d => d.CreatedByPerson).WithMany(p => p.TeachingSessionCreatedByPerson)
                .HasForeignKey(d => d.CreatedByPersonId)
                .HasConstraintName("FK__TeachingS__Creat__46B27FE2");

            entity.HasOne(d => d.PeriodSlot).WithMany(p => p.TeachingSession)
                .HasForeignKey(d => d.PeriodSlotId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TeachingS__Perio__43D61337");

            entity.HasOne(d => d.Subject).WithMany(p => p.TeachingSession)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TeachingS__Subje__41EDCAC5");

            entity.HasOne(d => d.TeacherAbsence).WithMany(p => p.TeachingSession)
                .HasForeignKey(d => d.TeacherAbsenceId)
                .HasConstraintName("FK_Session_Absence");

            entity.HasOne(d => d.Teacher).WithMany(p => p.TeachingSession)
                .HasForeignKey(d => d.TeacherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TeachingS__Teach__42E1EEFE");

            entity.HasOne(d => d.UpdatedByPerson).WithMany(p => p.TeachingSessionUpdatedByPerson)
                .HasForeignKey(d => d.UpdatedByPersonId)
                .HasConstraintName("FK__TeachingS__Updat__489AC854");

            entity.HasOne(d => d.Version).WithMany(p => p.TeachingSession)
                .HasForeignKey(d => d.VersionId)
                .HasConstraintName("FK_Session_Version");
        });

        modelBuilder.Entity<TimetableEntry>(entity =>
        {
            entity.HasKey(e => e.EntryId).HasName("PK__Timetabl__F57BD2F7578D9A1A");

            entity.HasIndex(e => new { e.VersionId, e.ClassId, e.DayOfWeek, e.PeriodSlotId }, "UQ_TimetableEntry").IsUnique();

            entity.HasOne(d => d.Class).WithMany(p => p.TimetableEntry)
                .HasForeignKey(d => d.ClassId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Timetable__Class__3B40CD36");

            entity.HasOne(d => d.PeriodSlot).WithMany(p => p.TimetableEntry)
                .HasForeignKey(d => d.PeriodSlotId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Timetable__Perio__3C34F16F");

            entity.HasOne(d => d.Subject).WithMany(p => p.TimetableEntry)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Timetable__Subje__3D2915A8");

            entity.HasOne(d => d.Teacher).WithMany(p => p.TimetableEntry)
                .HasForeignKey(d => d.TeacherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Timetable__Teach__3E1D39E1");

            entity.HasOne(d => d.Version).WithMany(p => p.TimetableEntry)
                .HasForeignKey(d => d.VersionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Timetable__Versi__3A4CA8FD");
        });

        modelBuilder.Entity<TimetableVersionClass>(entity =>
        {
            entity.HasKey(e => new { e.VersionId, e.ClassId }).HasName("PK_TimetableVersionClass");

            entity.HasOne(d => d.Class).WithMany(p => p.TimetableVersionClass)
                .HasForeignKey(d => d.ClassId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TimetableVersionClass_Class");

            entity.HasOne(d => d.Version).WithMany(p => p.TimetableVersionClass)
                .HasForeignKey(d => d.VersionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TimetableVersionClass_Version");
        });

        modelBuilder.Entity<TimetableVersion>(entity =>
        {
            entity.HasKey(e => e.VersionId).HasName("PK__Timetabl__16C6400F2125148A");

            entity.HasIndex(e => new { e.CampusId, e.SemesterId, e.Status }, "IX_Version_Scope");

            entity.Property(e => e.ImportedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))");
            entity.Property(e => e.PublishedAt).HasPrecision(0);
            entity.Property(e => e.SourceFileName).HasMaxLength(255);
            entity.Property(e => e.Status)
                .HasMaxLength(15)
                .HasDefaultValue("Draft");

            entity.HasOne(d => d.Campus).WithMany(p => p.TimetableVersion)
                .HasForeignKey(d => d.CampusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Timetable__Campu__31B762FC");

            entity.HasOne(d => d.ImportedByPerson).WithMany(p => p.TimetableVersionImportedByPerson)
                .HasForeignKey(d => d.ImportedByPersonId)
                .HasConstraintName("FK__Timetable__Impor__3493CFA7");

            entity.HasOne(d => d.PublishedByPerson).WithMany(p => p.TimetableVersionPublishedByPerson)
                .HasForeignKey(d => d.PublishedByPersonId)
                .HasConstraintName("FK__Timetable__Publi__367C1819");

            entity.HasOne(d => d.Semester).WithMany(p => p.TimetableVersion)
                .HasForeignKey(d => d.SemesterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Timetable__Semes__32AB8735");
        });
        modelBuilder.HasSequence<int>("seq_PersonCode").StartsAt(100001L);

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
