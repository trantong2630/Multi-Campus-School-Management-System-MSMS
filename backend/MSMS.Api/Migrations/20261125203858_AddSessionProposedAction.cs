using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MSMS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionProposedAction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProposedAction",
                table: "TeachingSession",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            // Đề xuất dạy thay đã lưu trước khi có cột loại đề xuất: coi là đề xuất Dạy thay.
            migrationBuilder.Sql("UPDATE TeachingSession SET ProposedAction = 'Substitute' WHERE ProposedTeacherId IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProposedAction",
                table: "TeachingSession");
        }
    }
}
