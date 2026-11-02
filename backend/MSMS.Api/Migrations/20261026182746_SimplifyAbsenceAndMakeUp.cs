using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MSMS.Api.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyAbsenceAndMakeUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Session_Original",
                table: "TeachingSession");

            migrationBuilder.DropIndex(
                name: "IX_TeachingSession_OriginalSessionId",
                table: "TeachingSession");

            migrationBuilder.DropColumn(
                name: "OriginalSessionId",
                table: "TeachingSession");

            migrationBuilder.AddColumn<string>(
                name: "SlotIds",
                table: "TeacherAbsence",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            // Khai báo cũ theo buổi: Sáng/Chiều thành danh sách các tiết của buổi đó, Cả ngày để trống.
            migrationBuilder.Sql(@"
                UPDATE a SET SlotIds = (
                    SELECT STRING_AGG(CAST(p.PeriodSlotId AS varchar(10)), ',') FROM PeriodSlot p WHERE p.SessionOfDay = a.SessionOfDay)
                FROM TeacherAbsence a WHERE a.SessionOfDay IN ('Morning', 'Afternoon');");

            migrationBuilder.DropColumn(
                name: "SessionOfDay",
                table: "TeacherAbsence");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SlotIds",
                table: "TeacherAbsence");

            migrationBuilder.AddColumn<long>(
                name: "OriginalSessionId",
                table: "TeachingSession",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionOfDay",
                table: "TeacherAbsence",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingSession_OriginalSessionId",
                table: "TeachingSession",
                column: "OriginalSessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Session_Original",
                table: "TeachingSession",
                column: "OriginalSessionId",
                principalTable: "TeachingSession",
                principalColumn: "SessionId");
        }
    }
}
