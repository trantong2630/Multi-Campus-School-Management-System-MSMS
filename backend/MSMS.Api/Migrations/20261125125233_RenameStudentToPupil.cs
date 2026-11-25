using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MSMS.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameStudentToPupil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssignmentRecipient_StudentProfile_StudentId",
                table: "AssignmentRecipient");

            migrationBuilder.RenameTable(
                name: "StudentProfile",
                newName: "PupilProfile");

            migrationBuilder.RenameTable(
                name: "ParentStudentLink",
                newName: "ParentPupilLink");

            migrationBuilder.RenameColumn(
                name: "StudentId",
                table: "Submission",
                newName: "PupilId");

            migrationBuilder.RenameIndex(
                name: "IX_Submission_Student",
                table: "Submission",
                newName: "IX_Submission_Pupil");

            migrationBuilder.RenameColumn(
                name: "StudentId",
                table: "LeaveRequest",
                newName: "PupilId");

            migrationBuilder.RenameIndex(
                name: "IX_Leave_Student",
                table: "LeaveRequest",
                newName: "IX_Leave_Pupil");

            migrationBuilder.RenameColumn(
                name: "StudentId",
                table: "Enrollment",
                newName: "PupilId");

            migrationBuilder.RenameIndex(
                name: "IX_Enrollment_Student",
                table: "Enrollment",
                newName: "IX_Enrollment_Pupil");

            migrationBuilder.RenameColumn(
                name: "StudentId",
                table: "Conversation",
                newName: "PupilId");

            migrationBuilder.RenameIndex(
                name: "IX_Conversation_StudentId",
                table: "Conversation",
                newName: "IX_Conversation_PupilId");

            migrationBuilder.RenameColumn(
                name: "StudentId",
                table: "AttendanceRecord",
                newName: "PupilId");

            migrationBuilder.RenameIndex(
                name: "IX_Attendance_Student",
                table: "AttendanceRecord",
                newName: "IX_Attendance_Pupil");

            migrationBuilder.RenameColumn(
                name: "StudentId",
                table: "AssignmentRecipient",
                newName: "PupilId");

            migrationBuilder.RenameIndex(
                name: "IX_AssignmentRecipient_Student",
                table: "AssignmentRecipient",
                newName: "IX_AssignmentRecipient_Pupil");

            migrationBuilder.RenameColumn(
                name: "StudentId",
                table: "PupilProfile",
                newName: "PupilId");

            migrationBuilder.RenameColumn(
                name: "StudentId",
                table: "ParentPupilLink",
                newName: "PupilId");

            migrationBuilder.RenameIndex(
                name: "UQ_ParentStudentLink",
                table: "ParentPupilLink",
                newName: "UQ_ParentPupilLink");

            migrationBuilder.RenameIndex(
                name: "IX_Link_Student",
                table: "ParentPupilLink",
                newName: "IX_Link_Pupil");

            migrationBuilder.AddForeignKey(
                name: "FK_AssignmentRecipient_PupilProfile_PupilId",
                table: "AssignmentRecipient",
                column: "PupilId",
                principalTable: "PupilProfile",
                principalColumn: "PupilId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssignmentRecipient_PupilProfile_PupilId",
                table: "AssignmentRecipient");

            migrationBuilder.RenameTable(
                name: "PupilProfile",
                newName: "StudentProfile");

            migrationBuilder.RenameTable(
                name: "ParentPupilLink",
                newName: "ParentStudentLink");

            migrationBuilder.RenameColumn(
                name: "PupilId",
                table: "Submission",
                newName: "StudentId");

            migrationBuilder.RenameIndex(
                name: "IX_Submission_Pupil",
                table: "Submission",
                newName: "IX_Submission_Student");

            migrationBuilder.RenameColumn(
                name: "PupilId",
                table: "LeaveRequest",
                newName: "StudentId");

            migrationBuilder.RenameIndex(
                name: "IX_Leave_Pupil",
                table: "LeaveRequest",
                newName: "IX_Leave_Student");

            migrationBuilder.RenameColumn(
                name: "PupilId",
                table: "Enrollment",
                newName: "StudentId");

            migrationBuilder.RenameIndex(
                name: "IX_Enrollment_Pupil",
                table: "Enrollment",
                newName: "IX_Enrollment_Student");

            migrationBuilder.RenameColumn(
                name: "PupilId",
                table: "Conversation",
                newName: "StudentId");

            migrationBuilder.RenameIndex(
                name: "IX_Conversation_PupilId",
                table: "Conversation",
                newName: "IX_Conversation_StudentId");

            migrationBuilder.RenameColumn(
                name: "PupilId",
                table: "AttendanceRecord",
                newName: "StudentId");

            migrationBuilder.RenameIndex(
                name: "IX_Attendance_Pupil",
                table: "AttendanceRecord",
                newName: "IX_Attendance_Student");

            migrationBuilder.RenameColumn(
                name: "PupilId",
                table: "AssignmentRecipient",
                newName: "StudentId");

            migrationBuilder.RenameIndex(
                name: "IX_AssignmentRecipient_Pupil",
                table: "AssignmentRecipient",
                newName: "IX_AssignmentRecipient_Student");

            migrationBuilder.RenameColumn(
                name: "PupilId",
                table: "StudentProfile",
                newName: "StudentId");

            migrationBuilder.RenameColumn(
                name: "PupilId",
                table: "ParentStudentLink",
                newName: "StudentId");

            migrationBuilder.RenameIndex(
                name: "UQ_ParentPupilLink",
                table: "ParentStudentLink",
                newName: "UQ_ParentStudentLink");

            migrationBuilder.RenameIndex(
                name: "IX_Link_Pupil",
                table: "ParentStudentLink",
                newName: "IX_Link_Student");

            migrationBuilder.AddForeignKey(
                name: "FK_AssignmentRecipient_StudentProfile_StudentId",
                table: "AssignmentRecipient",
                column: "StudentId",
                principalTable: "StudentProfile",
                principalColumn: "StudentId");
        }
    }
}
