using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MSMS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionProposedTeacher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProposedTeacherId",
                table: "TeachingSession",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeachingSession_ProposedTeacherId",
                table: "TeachingSession",
                column: "ProposedTeacherId");

            migrationBuilder.AddForeignKey(
                name: "FK_Session_ProposedTeacher",
                table: "TeachingSession",
                column: "ProposedTeacherId",
                principalTable: "StaffProfile",
                principalColumn: "StaffId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Session_ProposedTeacher",
                table: "TeachingSession");

            migrationBuilder.DropIndex(
                name: "IX_TeachingSession_ProposedTeacherId",
                table: "TeachingSession");

            migrationBuilder.DropColumn(
                name: "ProposedTeacherId",
                table: "TeachingSession");
        }
    }
}
