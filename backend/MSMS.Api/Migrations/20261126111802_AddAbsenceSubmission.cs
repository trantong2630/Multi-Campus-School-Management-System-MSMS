using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MSMS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAbsenceSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReturnReason",
                table: "TeacherAbsence",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "TeacherAbsence",
                type: "datetime2(0)",
                precision: 0,
                nullable: true);

            // Khai báo đang chờ duyệt theo luật cũ (mọi tiết còn chờ đã có đề xuất) coi như tổ trưởng đã gửi, để hiệu phó vẫn duyệt được.
            migrationBuilder.Sql(
                "UPDATE a SET a.SubmittedAt = a.CreatedAt FROM dbo.TeacherAbsence a " +
                "WHERE EXISTS (SELECT 1 FROM dbo.TeachingSession s WHERE s.TeacherAbsenceId = a.AbsenceId AND s.Status = 'Pending') " +
                "AND NOT EXISTS (SELECT 1 FROM dbo.TeachingSession s WHERE s.TeacherAbsenceId = a.AbsenceId AND s.Status = 'Pending' AND s.ProposedAction IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReturnReason",
                table: "TeacherAbsence");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "TeacherAbsence");
        }
    }
}
