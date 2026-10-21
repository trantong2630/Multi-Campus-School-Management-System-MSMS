using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MSMS.Api.Migrations
{
    /// <inheritdoc />
    public partial class CreateInitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "seq_PersonCode",
                startValue: 100001L);

            migrationBuilder.CreateTable(
                name: "AcademicYear",
                columns: table => new
                {
                    AcademicYearId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Academic__C54C7A0198EA872A", x => x.AcademicYearId);
                });

            migrationBuilder.CreateTable(
                name: "Campus",
                columns: table => new
                {
                    CampusId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Campus__FD598DD65E4BAAB7", x => x.CampusId);
                });

            migrationBuilder.CreateTable(
                name: "PeriodSlot",
                columns: table => new
                {
                    PeriodSlotId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionOfDay = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PeriodNo = table.Column<byte>(type: "tinyint", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time(0)", precision: 0, nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time(0)", precision: 0, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__PeriodSl__EBCA05D78951A712", x => x.PeriodSlotId);
                });

            migrationBuilder.CreateTable(
                name: "Person",
                columns: table => new
                {
                    PersonId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AvatarPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Person__AA2FFBE5D0917B03", x => x.PersonId);
                });

            migrationBuilder.CreateTable(
                name: "SubjectDepartment",
                columns: table => new
                {
                    DepartmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__SubjectD__B2079BEDD9888346", x => x.DepartmentId);
                });

            migrationBuilder.CreateTable(
                name: "Semester",
                columns: table => new
                {
                    SemesterId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    SemesterNo = table.Column<byte>(type: "tinyint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Semester__043301DD0088C9E3", x => x.SemesterId);
                    table.ForeignKey(
                        name: "FK__Semester__Academ__656C112C",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYear",
                        principalColumn: "AcademicYearId");
                });

            migrationBuilder.CreateTable(
                name: "Class",
                columns: table => new
                {
                    ClassId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampusId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GradeLevel = table.Column<byte>(type: "tinyint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CopiedFromClassId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Class__CB1927C0907630C5", x => x.ClassId);
                    table.ForeignKey(
                        name: "FK__Class__AcademicY__6FE99F9F",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYear",
                        principalColumn: "AcademicYearId");
                    table.ForeignKey(
                        name: "FK__Class__CampusId__6EF57B66",
                        column: x => x.CampusId,
                        principalTable: "Campus",
                        principalColumn: "CampusId");
                    table.ForeignKey(
                        name: "FK__Class__CopiedFro__71D1E811",
                        column: x => x.CopiedFromClassId,
                        principalTable: "Class",
                        principalColumn: "ClassId");
                });

            migrationBuilder.CreateTable(
                name: "Account",
                columns: table => new
                {
                    AccountId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false, defaultValue: "PendingPasswordChange"),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Account__349DA5A675E7E759", x => x.AccountId);
                    table.ForeignKey(
                        name: "FK__Account__PersonI__10566F31",
                        column: x => x.PersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                });

            migrationBuilder.CreateTable(
                name: "AuditLog",
                columns: table => new
                {
                    AuditId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ActorPersonId = table.Column<int>(type: "int", nullable: false),
                    TargetIds = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__AuditLog__A17F2398232493FD", x => x.AuditId);
                    table.ForeignKey(
                        name: "FK__AuditLog__ActorP__308E3499",
                        column: x => x.ActorPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                });

            migrationBuilder.CreateTable(
                name: "Holiday",
                columns: table => new
                {
                    HolidayId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    CampusId = table.Column<int>(type: "int", nullable: true),
                    HolidayDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsAdHoc = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByPersonId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Holiday__2D35D57ABFAFCD9B", x => x.HolidayId);
                    table.ForeignKey(
                        name: "FK_Holiday_CreatedBy",
                        column: x => x.CreatedByPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                    table.ForeignKey(
                        name: "FK__Holiday__Academi__68487DD7",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYear",
                        principalColumn: "AcademicYearId");
                    table.ForeignKey(
                        name: "FK__Holiday__CampusI__693CA210",
                        column: x => x.CampusId,
                        principalTable: "Campus",
                        principalColumn: "CampusId");
                });

            migrationBuilder.CreateTable(
                name: "Notification",
                columns: table => new
                {
                    NotificationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipientPersonId = table.Column<int>(type: "int", nullable: false),
                    NotificationType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RefEntityType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    RefEntityId = table.Column<long>(type: "bigint", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Notifica__20CF2E120F9DC25B", x => x.NotificationId);
                    table.ForeignKey(
                        name: "FK__Notificat__Recip__214BF109",
                        column: x => x.RecipientPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                });

            migrationBuilder.CreateTable(
                name: "Post",
                columns: table => new
                {
                    PostId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthorPersonId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Visibility = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "Posted"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    HiddenByPersonId = table.Column<int>(type: "int", nullable: true),
                    HiddenAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Post__AA12601895745049", x => x.PostId);
                    table.ForeignKey(
                        name: "FK__Post__AuthorPers__2610A626",
                        column: x => x.AuthorPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                    table.ForeignKey(
                        name: "FK__Post__HiddenByPe__28ED12D1",
                        column: x => x.HiddenByPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                });

            migrationBuilder.CreateTable(
                name: "SchoolProfile",
                columns: table => new
                {
                    SchoolProfileId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Website = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LogoPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedByPersonId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__SchoolPr__42620EDA11B7E046", x => x.SchoolProfileId);
                    table.ForeignKey(
                        name: "FK_SchoolProfile_UpdatedBy",
                        column: x => x.UpdatedByPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                });

            migrationBuilder.CreateTable(
                name: "StudentProfile",
                columns: table => new
                {
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Studying"),
                    AdmissionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LeftDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__StudentP__32C52B996A753723", x => x.StudentId);
                    table.ForeignKey(
                        name: "FK__StudentPr__Stude__797309D9",
                        column: x => x.StudentId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                });

            migrationBuilder.CreateTable(
                name: "StaffProfile",
                columns: table => new
                {
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    PrimaryCampusId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TeacherAbbr = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Position = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HireDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__StaffPro__96D4AB17928573D0", x => x.StaffId);
                    table.ForeignKey(
                        name: "FK__StaffProf__Depar__7F2BE32F",
                        column: x => x.DepartmentId,
                        principalTable: "SubjectDepartment",
                        principalColumn: "DepartmentId");
                    table.ForeignKey(
                        name: "FK__StaffProf__Prima__7E37BEF6",
                        column: x => x.PrimaryCampusId,
                        principalTable: "Campus",
                        principalColumn: "CampusId");
                    table.ForeignKey(
                        name: "FK__StaffProf__Staff__7D439ABD",
                        column: x => x.StaffId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                });

            migrationBuilder.CreateTable(
                name: "Subject",
                columns: table => new
                {
                    SubjectId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Abbreviation = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Subject__AC1BA3A8B189A9AB", x => x.SubjectId);
                    table.ForeignKey(
                        name: "FK__Subject__Departm__59063A47",
                        column: x => x.DepartmentId,
                        principalTable: "SubjectDepartment",
                        principalColumn: "DepartmentId");
                });

            migrationBuilder.CreateTable(
                name: "TimetableVersion",
                columns: table => new
                {
                    VersionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampusId = table.Column<int>(type: "int", nullable: false),
                    SemesterId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false, defaultValue: "Draft"),
                    SourceFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ImportedByPersonId = table.Column<int>(type: "int", nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))"),
                    PublishedByPersonId = table.Column<int>(type: "int", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    DeletedSessionCount = table.Column<int>(type: "int", nullable: true),
                    CreatedSessionCount = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Timetabl__16C6400F2125148A", x => x.VersionId);
                    table.ForeignKey(
                        name: "FK__Timetable__Campu__31B762FC",
                        column: x => x.CampusId,
                        principalTable: "Campus",
                        principalColumn: "CampusId");
                    table.ForeignKey(
                        name: "FK__Timetable__Impor__3493CFA7",
                        column: x => x.ImportedByPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                    table.ForeignKey(
                        name: "FK__Timetable__Publi__367C1819",
                        column: x => x.PublishedByPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                    table.ForeignKey(
                        name: "FK__Timetable__Semes__32AB8735",
                        column: x => x.SemesterId,
                        principalTable: "Semester",
                        principalColumn: "SemesterId");
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetOtp",
                columns: table => new
                {
                    OtpId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    OtpHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false),
                    AttemptCount = table.Column<byte>(type: "tinyint", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Password__3143C4A36BFD67E8", x => x.OtpId);
                    table.ForeignKey(
                        name: "FK__PasswordR__Accou__151B244E",
                        column: x => x.AccountId,
                        principalTable: "Account",
                        principalColumn: "AccountId");
                });

            migrationBuilder.CreateTable(
                name: "PostImage",
                columns: table => new
                {
                    PostImageId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostId = table.Column<int>(type: "int", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OrderNo = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)1),
                    FileSizeBytes = table.Column<int>(type: "int", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__PostImag__BCD3CCD0393636DA", x => x.PostImageId);
                    table.ForeignKey(
                        name: "FK__PostImage__PostI__2BC97F7C",
                        column: x => x.PostId,
                        principalTable: "Post",
                        principalColumn: "PostId");
                });

            migrationBuilder.CreateTable(
                name: "Enrollment",
                columns: table => new
                {
                    EnrollmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    ClassId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Enrollme__7F68771B3F3CD5D5", x => x.EnrollmentId);
                    table.ForeignKey(
                        name: "FK__Enrollmen__Class__0A9D95DB",
                        column: x => x.ClassId,
                        principalTable: "Class",
                        principalColumn: "ClassId");
                    table.ForeignKey(
                        name: "FK__Enrollmen__Stude__09A971A2",
                        column: x => x.StudentId,
                        principalTable: "StudentProfile",
                        principalColumn: "StudentId");
                });

            migrationBuilder.CreateTable(
                name: "LeaveRequest",
                columns: table => new
                {
                    LeaveRequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: false),
                    ClassId = table.Column<int>(type: "int", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SessionOfDay = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false, defaultValue: "Pending"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))"),
                    DecidedByPersonId = table.Column<int>(type: "int", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__LeaveReq__609421EEE1310F83", x => x.LeaveRequestId);
                    table.ForeignKey(
                        name: "FK__LeaveRequ__Class__10216507",
                        column: x => x.ClassId,
                        principalTable: "Class",
                        principalColumn: "ClassId");
                    table.ForeignKey(
                        name: "FK__LeaveRequ__Decid__12FDD1B2",
                        column: x => x.DecidedByPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                    table.ForeignKey(
                        name: "FK__LeaveRequ__Paren__0F2D40CE",
                        column: x => x.ParentId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                    table.ForeignKey(
                        name: "FK__LeaveRequ__Stude__0E391C95",
                        column: x => x.StudentId,
                        principalTable: "StudentProfile",
                        principalColumn: "StudentId");
                });

            migrationBuilder.CreateTable(
                name: "ParentStudentLink",
                columns: table => new
                {
                    LinkId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    Relationship = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ParentSt__2D122135D4896914", x => x.LinkId);
                    table.ForeignKey(
                        name: "FK__ParentStu__Paren__03F0984C",
                        column: x => x.ParentId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                    table.ForeignKey(
                        name: "FK__ParentStu__Stude__04E4BC85",
                        column: x => x.StudentId,
                        principalTable: "StudentProfile",
                        principalColumn: "StudentId");
                });

            migrationBuilder.CreateTable(
                name: "CampusLeadership",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampusId = table.Column<int>(type: "int", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AssignedByPersonId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__CampusLe__3214EC0724016194", x => x.Id);
                    table.ForeignKey(
                        name: "FK__CampusLea__Assig__1BC821DD",
                        column: x => x.AssignedByPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                    table.ForeignKey(
                        name: "FK__CampusLea__Campu__19DFD96B",
                        column: x => x.CampusId,
                        principalTable: "Campus",
                        principalColumn: "CampusId");
                    table.ForeignKey(
                        name: "FK__CampusLea__Staff__1AD3FDA4",
                        column: x => x.StaffId,
                        principalTable: "StaffProfile",
                        principalColumn: "StaffId");
                });

            migrationBuilder.CreateTable(
                name: "Conversation",
                columns: table => new
                {
                    ConversationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentId = table.Column<int>(type: "int", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))"),
                    LastMessageAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Conversa__C050D877AD622E4F", x => x.ConversationId);
                    table.ForeignKey(
                        name: "FK__Conversat__Paren__16CE6296",
                        column: x => x.ParentId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                    table.ForeignKey(
                        name: "FK__Conversat__Staff__17C286CF",
                        column: x => x.StaffId,
                        principalTable: "StaffProfile",
                        principalColumn: "StaffId");
                    table.ForeignKey(
                        name: "FK__Conversat__Stude__18B6AB08",
                        column: x => x.StudentId,
                        principalTable: "StudentProfile",
                        principalColumn: "StudentId");
                });

            migrationBuilder.CreateTable(
                name: "DepartmentHead",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AssignedByPersonId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Departme__3214EC071FF6F269", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Departmen__Assig__2180FB33",
                        column: x => x.AssignedByPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                    table.ForeignKey(
                        name: "FK__Departmen__Depar__1F98B2C1",
                        column: x => x.DepartmentId,
                        principalTable: "SubjectDepartment",
                        principalColumn: "DepartmentId");
                    table.ForeignKey(
                        name: "FK__Departmen__Staff__208CD6FA",
                        column: x => x.StaffId,
                        principalTable: "StaffProfile",
                        principalColumn: "StaffId");
                });

            migrationBuilder.CreateTable(
                name: "HomeroomAssignment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClassId = table.Column<int>(type: "int", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AssignedByPersonId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Homeroom__3214EC0708B8D1B0", x => x.Id);
                    table.ForeignKey(
                        name: "FK__HomeroomA__Assig__2739D489",
                        column: x => x.AssignedByPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                    table.ForeignKey(
                        name: "FK__HomeroomA__Class__25518C17",
                        column: x => x.ClassId,
                        principalTable: "Class",
                        principalColumn: "ClassId");
                    table.ForeignKey(
                        name: "FK__HomeroomA__Staff__2645B050",
                        column: x => x.StaffId,
                        principalTable: "StaffProfile",
                        principalColumn: "StaffId");
                });

            migrationBuilder.CreateTable(
                name: "TeacherAbsence",
                columns: table => new
                {
                    AbsenceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<int>(type: "int", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SessionOfDay = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DeclaredByPersonId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__TeacherA__3A074E674F278659", x => x.AbsenceId);
                    table.ForeignKey(
                        name: "FK__TeacherAb__Decla__4C6B5938",
                        column: x => x.DeclaredByPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                    table.ForeignKey(
                        name: "FK__TeacherAb__Teach__4B7734FF",
                        column: x => x.TeacherId,
                        principalTable: "StaffProfile",
                        principalColumn: "StaffId");
                });

            migrationBuilder.CreateTable(
                name: "Exam",
                columns: table => new
                {
                    ExamId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OwnerPersonId = table.Column<int>(type: "int", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    CopiedFromExamId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Exam__297521C73FAE556B", x => x.ExamId);
                    table.ForeignKey(
                        name: "FK__Exam__CopiedFrom__6AEFE058",
                        column: x => x.CopiedFromExamId,
                        principalTable: "Exam",
                        principalColumn: "ExamId");
                    table.ForeignKey(
                        name: "FK__Exam__OwnerPerso__690797E6",
                        column: x => x.OwnerPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                    table.ForeignKey(
                        name: "FK__Exam__SubjectId__681373AD",
                        column: x => x.SubjectId,
                        principalTable: "Subject",
                        principalColumn: "SubjectId");
                });

            migrationBuilder.CreateTable(
                name: "Question",
                columns: table => new
                {
                    QuestionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    GradeLevel = table.Column<byte>(type: "tinyint", nullable: true),
                    Topic = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "Draft"),
                    IsAiGenerated = table.Column<bool>(type: "bit", nullable: false),
                    IsHidden = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByPersonId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Question__0DC06FAC89AD8931", x => x.QuestionId);
                    table.ForeignKey(
                        name: "FK__Question__Create__5F7E2DAC",
                        column: x => x.CreatedByPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                    table.ForeignKey(
                        name: "FK__Question__Subjec__5BAD9CC8",
                        column: x => x.SubjectId,
                        principalTable: "Subject",
                        principalColumn: "SubjectId");
                });

            migrationBuilder.CreateTable(
                name: "TeachingAssignment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClassId = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    TeacherId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AssignedByPersonId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Teaching__3214EC07EC22014A", x => x.Id);
                    table.ForeignKey(
                        name: "FK__TeachingA__Assig__2DE6D218",
                        column: x => x.AssignedByPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                    table.ForeignKey(
                        name: "FK__TeachingA__Class__2B0A656D",
                        column: x => x.ClassId,
                        principalTable: "Class",
                        principalColumn: "ClassId");
                    table.ForeignKey(
                        name: "FK__TeachingA__Subje__2BFE89A6",
                        column: x => x.SubjectId,
                        principalTable: "Subject",
                        principalColumn: "SubjectId");
                    table.ForeignKey(
                        name: "FK__TeachingA__Teach__2CF2ADDF",
                        column: x => x.TeacherId,
                        principalTable: "StaffProfile",
                        principalColumn: "StaffId");
                });

            migrationBuilder.CreateTable(
                name: "TimetableEntry",
                columns: table => new
                {
                    EntryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VersionId = table.Column<int>(type: "int", nullable: false),
                    ClassId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<byte>(type: "tinyint", nullable: false),
                    PeriodSlotId = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    TeacherId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Timetabl__F57BD2F7578D9A1A", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK__Timetable__Class__3B40CD36",
                        column: x => x.ClassId,
                        principalTable: "Class",
                        principalColumn: "ClassId");
                    table.ForeignKey(
                        name: "FK__Timetable__Perio__3C34F16F",
                        column: x => x.PeriodSlotId,
                        principalTable: "PeriodSlot",
                        principalColumn: "PeriodSlotId");
                    table.ForeignKey(
                        name: "FK__Timetable__Subje__3D2915A8",
                        column: x => x.SubjectId,
                        principalTable: "Subject",
                        principalColumn: "SubjectId");
                    table.ForeignKey(
                        name: "FK__Timetable__Teach__3E1D39E1",
                        column: x => x.TeacherId,
                        principalTable: "StaffProfile",
                        principalColumn: "StaffId");
                    table.ForeignKey(
                        name: "FK__Timetable__Versi__3A4CA8FD",
                        column: x => x.VersionId,
                        principalTable: "TimetableVersion",
                        principalColumn: "VersionId");
                });

            migrationBuilder.CreateTable(
                name: "Message",
                columns: table => new
                {
                    MessageId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConversationId = table.Column<int>(type: "int", nullable: false),
                    SenderPersonId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))"),
                    ReadAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Message__C87C0C9C73DD1991", x => x.MessageId);
                    table.ForeignKey(
                        name: "FK__Message__Convers__1C873BEC",
                        column: x => x.ConversationId,
                        principalTable: "Conversation",
                        principalColumn: "ConversationId");
                    table.ForeignKey(
                        name: "FK__Message__SenderP__1D7B6025",
                        column: x => x.SenderPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                });

            migrationBuilder.CreateTable(
                name: "TeachingSession",
                columns: table => new
                {
                    SessionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClassId = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    TeacherId = table.Column<int>(type: "int", nullable: false),
                    SessionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodSlotId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false, defaultValue: "Scheduled"),
                    Source = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false, defaultValue: "Generated"),
                    VersionId = table.Column<int>(type: "int", nullable: true),
                    OriginalSessionId = table.Column<long>(type: "bigint", nullable: true),
                    TeacherAbsenceId = table.Column<int>(type: "int", nullable: true),
                    ChangeNote = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedByPersonId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))"),
                    UpdatedByPersonId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Teaching__C9F49290E9E86E45", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_Session_Absence",
                        column: x => x.TeacherAbsenceId,
                        principalTable: "TeacherAbsence",
                        principalColumn: "AbsenceId");
                    table.ForeignKey(
                        name: "FK_Session_Original",
                        column: x => x.OriginalSessionId,
                        principalTable: "TeachingSession",
                        principalColumn: "SessionId");
                    table.ForeignKey(
                        name: "FK_Session_Version",
                        column: x => x.VersionId,
                        principalTable: "TimetableVersion",
                        principalColumn: "VersionId");
                    table.ForeignKey(
                        name: "FK__TeachingS__Class__40F9A68C",
                        column: x => x.ClassId,
                        principalTable: "Class",
                        principalColumn: "ClassId");
                    table.ForeignKey(
                        name: "FK__TeachingS__Creat__46B27FE2",
                        column: x => x.CreatedByPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                    table.ForeignKey(
                        name: "FK__TeachingS__Perio__43D61337",
                        column: x => x.PeriodSlotId,
                        principalTable: "PeriodSlot",
                        principalColumn: "PeriodSlotId");
                    table.ForeignKey(
                        name: "FK__TeachingS__Subje__41EDCAC5",
                        column: x => x.SubjectId,
                        principalTable: "Subject",
                        principalColumn: "SubjectId");
                    table.ForeignKey(
                        name: "FK__TeachingS__Teach__42E1EEFE",
                        column: x => x.TeacherId,
                        principalTable: "StaffProfile",
                        principalColumn: "StaffId");
                    table.ForeignKey(
                        name: "FK__TeachingS__Updat__489AC854",
                        column: x => x.UpdatedByPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                });

            migrationBuilder.CreateTable(
                name: "Assignment",
                columns: table => new
                {
                    AssignmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExamId = table.Column<int>(type: "int", nullable: false),
                    ClassId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Instruction = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OpenAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false),
                    DueAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "Draft"),
                    AssignedByPersonId = table.Column<int>(type: "int", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    ScorePublishedByPersonId = table.Column<int>(type: "int", nullable: true),
                    ScorePublishedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Assignme__32499E77EF005FDB", x => x.AssignmentId);
                    table.ForeignKey(
                        name: "FK__Assignmen__Assig__7755B73D",
                        column: x => x.AssignedByPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                    table.ForeignKey(
                        name: "FK__Assignmen__Class__756D6ECB",
                        column: x => x.ClassId,
                        principalTable: "Class",
                        principalColumn: "ClassId");
                    table.ForeignKey(
                        name: "FK__Assignmen__ExamI__74794A92",
                        column: x => x.ExamId,
                        principalTable: "Exam",
                        principalColumn: "ExamId");
                    table.ForeignKey(
                        name: "FK__Assignmen__Score__7849DB76",
                        column: x => x.ScorePublishedByPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                });

            migrationBuilder.CreateTable(
                name: "ExamQuestion",
                columns: table => new
                {
                    ExamQuestionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExamId = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<int>(type: "int", nullable: false),
                    OrderNo = table.Column<short>(type: "smallint", nullable: false),
                    Points = table.Column<decimal>(type: "decimal(4,2)", nullable: false, defaultValue: 1m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ExamQues__EFAED846C9BE138C", x => x.ExamQuestionId);
                    table.ForeignKey(
                        name: "FK__ExamQuest__ExamI__6FB49575",
                        column: x => x.ExamId,
                        principalTable: "Exam",
                        principalColumn: "ExamId");
                    table.ForeignKey(
                        name: "FK__ExamQuest__Quest__70A8B9AE",
                        column: x => x.QuestionId,
                        principalTable: "Question",
                        principalColumn: "QuestionId");
                });

            migrationBuilder.CreateTable(
                name: "QuestionOption",
                columns: table => new
                {
                    OptionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuestionId = table.Column<int>(type: "int", nullable: false),
                    OptionLabel = table.Column<string>(type: "nchar(1)", fixedLength: true, maxLength: 1, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Question__92C7A1FFF146A0D9", x => x.OptionId);
                    table.ForeignKey(
                        name: "FK__QuestionO__Quest__6442E2C9",
                        column: x => x.QuestionId,
                        principalTable: "Question",
                        principalColumn: "QuestionId");
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRecord",
                columns: table => new
                {
                    AttendanceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsBackfilled = table.Column<bool>(type: "bit", nullable: false),
                    RecordedByPersonId = table.Column<int>(type: "int", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))"),
                    UpdatedByPersonId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Attendan__8B69261CA0EC7B5F", x => x.AttendanceId);
                    table.ForeignKey(
                        name: "FK__Attendanc__Recor__56E8E7AB",
                        column: x => x.RecordedByPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                    table.ForeignKey(
                        name: "FK__Attendanc__Sessi__540C7B00",
                        column: x => x.SessionId,
                        principalTable: "TeachingSession",
                        principalColumn: "SessionId");
                    table.ForeignKey(
                        name: "FK__Attendanc__Stude__55009F39",
                        column: x => x.StudentId,
                        principalTable: "StudentProfile",
                        principalColumn: "StudentId");
                    table.ForeignKey(
                        name: "FK__Attendanc__Updat__58D1301D",
                        column: x => x.UpdatedByPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                });

            migrationBuilder.CreateTable(
                name: "AiFeedback",
                columns: table => new
                {
                    FeedbackId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssignmentId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "Draft"),
                    AiContent = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EditedContent = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    ApprovedByPersonId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__AiFeedba__6A4BEDD6EF230D1E", x => x.FeedbackId);
                    table.ForeignKey(
                        name: "FK__AiFeedbac__Appro__0B5CAFEA",
                        column: x => x.ApprovedByPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                    table.ForeignKey(
                        name: "FK__AiFeedbac__Assig__0880433F",
                        column: x => x.AssignmentId,
                        principalTable: "Assignment",
                        principalColumn: "AssignmentId");
                    table.ForeignKey(
                        name: "FK__AiFeedbac__Stude__09746778",
                        column: x => x.StudentId,
                        principalTable: "StudentProfile",
                        principalColumn: "StudentId");
                });

            migrationBuilder.CreateTable(
                name: "Submission",
                columns: table => new
                {
                    SubmissionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssignmentId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))"),
                    LastSavedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    Score = table.Column<decimal>(type: "decimal(4,2)", nullable: true),
                    CorrectCount = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Submissi__449EE1252F0D5C2F", x => x.SubmissionId);
                    table.ForeignKey(
                        name: "FK__Submissio__Assig__7D0E9093",
                        column: x => x.AssignmentId,
                        principalTable: "Assignment",
                        principalColumn: "AssignmentId");
                    table.ForeignKey(
                        name: "FK__Submissio__Stude__7E02B4CC",
                        column: x => x.StudentId,
                        principalTable: "StudentProfile",
                        principalColumn: "StudentId");
                });

            migrationBuilder.CreateTable(
                name: "SubmissionAnswer",
                columns: table => new
                {
                    AnswerId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubmissionId = table.Column<long>(type: "bigint", nullable: false),
                    ExamQuestionId = table.Column<int>(type: "int", nullable: false),
                    SelectedOptionId = table.Column<int>(type: "int", nullable: true),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: true),
                    EarnedPoints = table.Column<decimal>(type: "decimal(4,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Submissi__D4825004190B923E", x => x.AnswerId);
                    table.ForeignKey(
                        name: "FK__Submissio__ExamQ__03BB8E22",
                        column: x => x.ExamQuestionId,
                        principalTable: "ExamQuestion",
                        principalColumn: "ExamQuestionId");
                    table.ForeignKey(
                        name: "FK__Submissio__Selec__04AFB25B",
                        column: x => x.SelectedOptionId,
                        principalTable: "QuestionOption",
                        principalColumn: "OptionId");
                    table.ForeignKey(
                        name: "FK__Submissio__Submi__02C769E9",
                        column: x => x.SubmissionId,
                        principalTable: "Submission",
                        principalColumn: "SubmissionId");
                });

            migrationBuilder.CreateIndex(
                name: "UQ__Academic__737584F647FE2DE1",
                table: "AcademicYear",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__Account__536C85E403EE4037",
                table: "Account",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__Account__AA2FFBE481262702",
                table: "Account",
                column: "PersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiFeedback_ApprovedByPersonId",
                table: "AiFeedback",
                column: "ApprovedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_AiFeedback_StudentId",
                table: "AiFeedback",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "UQ_AiFeedback",
                table: "AiFeedback",
                columns: new[] { "AssignmentId", "StudentId" },
                unique: true,
                filter: "[StudentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Assignment_AssignedByPersonId",
                table: "Assignment",
                column: "AssignedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignment_Class",
                table: "Assignment",
                columns: new[] { "ClassId", "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Assignment_Exam",
                table: "Assignment",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignment_ScorePublishedByPersonId",
                table: "Assignment",
                column: "ScorePublishedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_Student",
                table: "AttendanceRecord",
                columns: new[] { "StudentId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecord_RecordedByPersonId",
                table: "AttendanceRecord",
                column: "RecordedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecord_UpdatedByPersonId",
                table: "AttendanceRecord",
                column: "UpdatedByPersonId");

            migrationBuilder.CreateIndex(
                name: "UQ_Attendance",
                table: "AttendanceRecord",
                columns: new[] { "SessionId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Audit_Actor",
                table: "AuditLog",
                columns: new[] { "ActorPersonId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Audit_Filter",
                table: "AuditLog",
                columns: new[] { "Category", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "UQ__Campus__737584F6F80B5FC0",
                table: "Campus",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__Campus__A25C5AA776F207BE",
                table: "Campus",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampusLeadership",
                table: "CampusLeadership",
                columns: new[] { "CampusId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CampusLeadership_AssignedByPersonId",
                table: "CampusLeadership",
                column: "AssignedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_CampusLeadership_StaffId",
                table: "CampusLeadership",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Class_AcademicYearId",
                table: "Class",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_Class_CopiedFromClassId",
                table: "Class",
                column: "CopiedFromClassId");

            migrationBuilder.CreateIndex(
                name: "UQ_Class",
                table: "Class",
                columns: new[] { "CampusId", "AcademicYearId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversation_Staff",
                table: "Conversation",
                columns: new[] { "StaffId", "LastMessageAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Conversation_StudentId",
                table: "Conversation",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "UQ_Conversation",
                table: "Conversation",
                columns: new[] { "ParentId", "StaffId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentHead",
                table: "DepartmentHead",
                columns: new[] { "DepartmentId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentHead_AssignedByPersonId",
                table: "DepartmentHead",
                column: "AssignedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentHead_StaffId",
                table: "DepartmentHead",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollment_Class",
                table: "Enrollment",
                columns: new[] { "ClassId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Enrollment_Student",
                table: "Enrollment",
                columns: new[] { "StudentId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Exam_CopiedFromExamId",
                table: "Exam",
                column: "CopiedFromExamId");

            migrationBuilder.CreateIndex(
                name: "IX_Exam_Owner",
                table: "Exam",
                column: "OwnerPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Exam_Subject",
                table: "Exam",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamQuestion_QuestionId",
                table: "ExamQuestion",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "UQ_ExamQuestion",
                table: "ExamQuestion",
                columns: new[] { "ExamId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Holiday_CampusId",
                table: "Holiday",
                column: "CampusId");

            migrationBuilder.CreateIndex(
                name: "IX_Holiday_CreatedByPersonId",
                table: "Holiday",
                column: "CreatedByPersonId");

            migrationBuilder.CreateIndex(
                name: "UQ_Holiday",
                table: "Holiday",
                columns: new[] { "AcademicYearId", "HolidayDate", "CampusId" },
                unique: true,
                filter: "[CampusId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Homeroom_Class",
                table: "HomeroomAssignment",
                columns: new[] { "ClassId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Homeroom_Staff",
                table: "HomeroomAssignment",
                columns: new[] { "StaffId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_HomeroomAssignment_AssignedByPersonId",
                table: "HomeroomAssignment",
                column: "AssignedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Leave_Class",
                table: "LeaveRequest",
                columns: new[] { "ClassId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Leave_Student",
                table: "LeaveRequest",
                columns: new[] { "StudentId", "Status", "FromDate", "ToDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequest_DecidedByPersonId",
                table: "LeaveRequest",
                column: "DecidedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequest_ParentId",
                table: "LeaveRequest",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Message_Conversation",
                table: "Message",
                columns: new[] { "ConversationId", "SentAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Message_SenderPersonId",
                table: "Message",
                column: "SenderPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_Inbox",
                table: "Notification",
                columns: new[] { "RecipientPersonId", "IsRead", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Link_Student",
                table: "ParentStudentLink",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "UQ_ParentStudentLink",
                table: "ParentStudentLink",
                columns: new[] { "ParentId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Otp_Account",
                table: "PasswordResetOtp",
                columns: new[] { "AccountId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "UQ_PeriodSlot",
                table: "PeriodSlot",
                columns: new[] { "SessionOfDay", "PeriodNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Person_Identify",
                table: "Person",
                columns: new[] { "FullName", "DateOfBirth" });

            migrationBuilder.CreateIndex(
                name: "IX_Person_Phone",
                table: "Person",
                column: "PhoneNumber");

            migrationBuilder.CreateIndex(
                name: "UQ__Person__7B5569ED2848A4AA",
                table: "Person",
                column: "PersonCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Post_AuthorPersonId",
                table: "Post",
                column: "AuthorPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Post_Feed",
                table: "Post",
                columns: new[] { "Status", "Visibility", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Post_HiddenByPersonId",
                table: "Post",
                column: "HiddenByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_PostImage_Post",
                table: "PostImage",
                columns: new[] { "PostId", "OrderNo" });

            migrationBuilder.CreateIndex(
                name: "IX_Question_Bank",
                table: "Question",
                columns: new[] { "SubjectId", "Status", "IsHidden" });

            migrationBuilder.CreateIndex(
                name: "IX_Question_CreatedByPersonId",
                table: "Question",
                column: "CreatedByPersonId");

            migrationBuilder.CreateIndex(
                name: "UQ_QuestionOption",
                table: "QuestionOption",
                columns: new[] { "QuestionId", "OptionLabel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolProfile_UpdatedByPersonId",
                table: "SchoolProfile",
                column: "UpdatedByPersonId");

            migrationBuilder.CreateIndex(
                name: "UQ_Semester",
                table: "Semester",
                columns: new[] { "AcademicYearId", "SemesterNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffProfile_DepartmentId",
                table: "StaffProfile",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffProfile_PrimaryCampusId",
                table: "StaffProfile",
                column: "PrimaryCampusId");

            migrationBuilder.CreateIndex(
                name: "UQ_StaffProfile_Abbr",
                table: "StaffProfile",
                column: "TeacherAbbr",
                unique: true,
                filter: "([TeacherAbbr] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Subject_DepartmentId",
                table: "Subject",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "UQ__Subject__737584F642EFF95A",
                table: "Subject",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__Subject__9C41170ED2F47C9B",
                table: "Subject",
                column: "Abbreviation",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__SubjectD__737584F6FAF3A0BA",
                table: "SubjectDepartment",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Submission_Student",
                table: "Submission",
                columns: new[] { "StudentId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "UQ_Submission",
                table: "Submission",
                columns: new[] { "AssignmentId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionAnswer_ExamQuestionId",
                table: "SubmissionAnswer",
                column: "ExamQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionAnswer_SelectedOptionId",
                table: "SubmissionAnswer",
                column: "SelectedOptionId");

            migrationBuilder.CreateIndex(
                name: "UQ_SubmissionAnswer",
                table: "SubmissionAnswer",
                columns: new[] { "SubmissionId", "ExamQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Absence_Teacher",
                table: "TeacherAbsence",
                columns: new[] { "TeacherId", "FromDate", "ToDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAbsence_DeclaredByPersonId",
                table: "TeacherAbsence",
                column: "DeclaredByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_TA_ClassSubject",
                table: "TeachingAssignment",
                columns: new[] { "ClassId", "SubjectId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TA_Teacher",
                table: "TeachingAssignment",
                columns: new[] { "TeacherId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignment_AssignedByPersonId",
                table: "TeachingAssignment",
                column: "AssignedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignment_SubjectId",
                table: "TeachingAssignment",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Session_Class",
                table: "TeachingSession",
                columns: new[] { "ClassId", "SessionDate", "PeriodSlotId" });

            migrationBuilder.CreateIndex(
                name: "IX_Session_Status",
                table: "TeachingSession",
                columns: new[] { "Status", "SessionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Session_Teacher",
                table: "TeachingSession",
                columns: new[] { "TeacherId", "SessionDate", "PeriodSlotId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeachingSession_CreatedByPersonId",
                table: "TeachingSession",
                column: "CreatedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingSession_OriginalSessionId",
                table: "TeachingSession",
                column: "OriginalSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingSession_PeriodSlotId",
                table: "TeachingSession",
                column: "PeriodSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingSession_SubjectId",
                table: "TeachingSession",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingSession_TeacherAbsenceId",
                table: "TeachingSession",
                column: "TeacherAbsenceId");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingSession_UpdatedByPersonId",
                table: "TeachingSession",
                column: "UpdatedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingSession_VersionId",
                table: "TeachingSession",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableEntry_ClassId",
                table: "TimetableEntry",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableEntry_PeriodSlotId",
                table: "TimetableEntry",
                column: "PeriodSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableEntry_SubjectId",
                table: "TimetableEntry",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableEntry_TeacherId",
                table: "TimetableEntry",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "UQ_TimetableEntry",
                table: "TimetableEntry",
                columns: new[] { "VersionId", "ClassId", "DayOfWeek", "PeriodSlotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimetableVersion_ImportedByPersonId",
                table: "TimetableVersion",
                column: "ImportedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableVersion_PublishedByPersonId",
                table: "TimetableVersion",
                column: "PublishedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableVersion_SemesterId",
                table: "TimetableVersion",
                column: "SemesterId");

            migrationBuilder.CreateIndex(
                name: "IX_Version_Scope",
                table: "TimetableVersion",
                columns: new[] { "CampusId", "SemesterId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiFeedback");

            migrationBuilder.DropTable(
                name: "AttendanceRecord");

            migrationBuilder.DropTable(
                name: "AuditLog");

            migrationBuilder.DropTable(
                name: "CampusLeadership");

            migrationBuilder.DropTable(
                name: "DepartmentHead");

            migrationBuilder.DropTable(
                name: "Enrollment");

            migrationBuilder.DropTable(
                name: "Holiday");

            migrationBuilder.DropTable(
                name: "HomeroomAssignment");

            migrationBuilder.DropTable(
                name: "LeaveRequest");

            migrationBuilder.DropTable(
                name: "Message");

            migrationBuilder.DropTable(
                name: "Notification");

            migrationBuilder.DropTable(
                name: "ParentStudentLink");

            migrationBuilder.DropTable(
                name: "PasswordResetOtp");

            migrationBuilder.DropTable(
                name: "PostImage");

            migrationBuilder.DropTable(
                name: "SchoolProfile");

            migrationBuilder.DropTable(
                name: "SubmissionAnswer");

            migrationBuilder.DropTable(
                name: "TeachingAssignment");

            migrationBuilder.DropTable(
                name: "TimetableEntry");

            migrationBuilder.DropTable(
                name: "TeachingSession");

            migrationBuilder.DropTable(
                name: "Conversation");

            migrationBuilder.DropTable(
                name: "Account");

            migrationBuilder.DropTable(
                name: "Post");

            migrationBuilder.DropTable(
                name: "ExamQuestion");

            migrationBuilder.DropTable(
                name: "QuestionOption");

            migrationBuilder.DropTable(
                name: "Submission");

            migrationBuilder.DropTable(
                name: "TeacherAbsence");

            migrationBuilder.DropTable(
                name: "TimetableVersion");

            migrationBuilder.DropTable(
                name: "PeriodSlot");

            migrationBuilder.DropTable(
                name: "Question");

            migrationBuilder.DropTable(
                name: "Assignment");

            migrationBuilder.DropTable(
                name: "StudentProfile");

            migrationBuilder.DropTable(
                name: "StaffProfile");

            migrationBuilder.DropTable(
                name: "Semester");

            migrationBuilder.DropTable(
                name: "Class");

            migrationBuilder.DropTable(
                name: "Exam");

            migrationBuilder.DropTable(
                name: "AcademicYear");

            migrationBuilder.DropTable(
                name: "Campus");

            migrationBuilder.DropTable(
                name: "Person");

            migrationBuilder.DropTable(
                name: "Subject");

            migrationBuilder.DropTable(
                name: "SubjectDepartment");

            migrationBuilder.DropSequence(
                name: "seq_PersonCode");
        }
    }
}
