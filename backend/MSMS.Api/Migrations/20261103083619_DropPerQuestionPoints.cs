using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MSMS.Api.Migrations
{
    /// <inheritdoc />
    public partial class DropPerQuestionPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EarnedPoints",
                table: "SubmissionAnswer");

            migrationBuilder.DropColumn(
                name: "Points",
                table: "AssignmentQuestion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EarnedPoints",
                table: "SubmissionAnswer",
                type: "decimal(4,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Points",
                table: "AssignmentQuestion",
                type: "decimal(4,2)",
                nullable: false,
                defaultValue: 1m);
        }
    }
}
