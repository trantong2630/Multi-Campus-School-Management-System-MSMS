using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MSMS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionAnswerOption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubmissionAnswerOption",
                columns: table => new
                {
                    AnswerId = table.Column<long>(type: "bigint", nullable: false),
                    OptionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionAnswerOption", x => new { x.AnswerId, x.OptionId });
                    table.ForeignKey(
                        name: "FK_SubmissionAnswerOption_QuestionOption_OptionId",
                        column: x => x.OptionId,
                        principalTable: "QuestionOption",
                        principalColumn: "OptionId");
                    table.ForeignKey(
                        name: "FK_SubmissionAnswerOption_SubmissionAnswer_AnswerId",
                        column: x => x.AnswerId,
                        principalTable: "SubmissionAnswer",
                        principalColumn: "AnswerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionAnswerOption_Option",
                table: "SubmissionAnswerOption",
                column: "OptionId");

            migrationBuilder.Sql(
                "INSERT INTO SubmissionAnswerOption (AnswerId, OptionId) " +
                "SELECT AnswerId, SelectedOptionId FROM SubmissionAnswer WHERE SelectedOptionId IS NOT NULL");

            migrationBuilder.DropForeignKey(
                name: "FK__Submissio__Selec__04AFB25B",
                table: "SubmissionAnswer");

            migrationBuilder.DropIndex(
                name: "IX_SubmissionAnswer_SelectedOptionId",
                table: "SubmissionAnswer");

            migrationBuilder.DropColumn(
                name: "SelectedOptionId",
                table: "SubmissionAnswer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SelectedOptionId",
                table: "SubmissionAnswer",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE sa SET SelectedOptionId = (SELECT MIN(sao.OptionId) FROM SubmissionAnswerOption sao WHERE sao.AnswerId = sa.AnswerId) " +
                "FROM SubmissionAnswer sa");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionAnswer_SelectedOptionId",
                table: "SubmissionAnswer",
                column: "SelectedOptionId");

            migrationBuilder.AddForeignKey(
                name: "FK__Submissio__Selec__04AFB25B",
                table: "SubmissionAnswer",
                column: "SelectedOptionId",
                principalTable: "QuestionOption",
                principalColumn: "OptionId");

            migrationBuilder.DropTable(
                name: "SubmissionAnswerOption");
        }
    }
}
