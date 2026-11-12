using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MSMS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionDifficultyAndPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Difficulty",
                table: "Question",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Points",
                table: "AssignmentQuestion",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 1m);

            // Bài đã giao trước khi có thang điểm theo phần: chia đều 10 điểm cho các câu (phần dư 0,01 chia từ câu đầu) để mỗi câu
            // hiện đúng điểm của nó và cộng lại bằng 10. Điểm đã chấm của các bài nộp không đổi vì chúng đã được lưu.
            migrationBuilder.Sql(@"
                UPDATE aq SET Points = (1000 / t.N + CASE WHEN t.Rn <= 1000 % t.N THEN 1 ELSE 0 END) / 100.0
                FROM AssignmentQuestion aq
                JOIN (SELECT AssignmentQuestionId,
                             ROW_NUMBER() OVER (PARTITION BY AssignmentId ORDER BY OrderNo, AssignmentQuestionId) AS Rn,
                             COUNT(*) OVER (PARTITION BY AssignmentId) AS N
                      FROM AssignmentQuestion) t ON t.AssignmentQuestionId = aq.AssignmentQuestionId;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "Question");

            migrationBuilder.DropColumn(
                name: "Points",
                table: "AssignmentQuestion");
        }
    }
}
