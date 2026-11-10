using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MSMS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTextbookLessons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LessonId",
                table: "Question",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TextbookBook",
                columns: table => new
                {
                    BookId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    GradeLevel = table.Column<byte>(type: "tinyint", nullable: false),
                    Volume = table.Column<byte>(type: "tinyint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PageOffset = table.Column<short>(type: "smallint", nullable: false),
                    PdfPageCount = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TextbookBook", x => x.BookId);
                    table.ForeignKey(
                        name: "FK_TextbookBook_Subject",
                        column: x => x.SubjectId,
                        principalTable: "Subject",
                        principalColumn: "SubjectId");
                });

            migrationBuilder.CreateTable(
                name: "TextbookLesson",
                columns: table => new
                {
                    LessonId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookId = table.Column<int>(type: "int", nullable: false),
                    Chapter = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<short>(type: "smallint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartPage = table.Column<short>(type: "smallint", nullable: false),
                    EndPage = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TextbookLesson", x => x.LessonId);
                    table.ForeignKey(
                        name: "FK_TextbookLesson_Book",
                        column: x => x.BookId,
                        principalTable: "TextbookBook",
                        principalColumn: "BookId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Question_LessonId",
                table: "Question",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_TextbookBook_SubjectId",
                table: "TextbookBook",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "UQ_TextbookBook_Code",
                table: "TextbookBook",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_TextbookLesson_Order",
                table: "TextbookLesson",
                columns: new[] { "BookId", "SortOrder" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Question_TextbookLesson",
                table: "Question",
                column: "LessonId",
                principalTable: "TextbookLesson",
                principalColumn: "LessonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Question_TextbookLesson",
                table: "Question");

            migrationBuilder.DropTable(
                name: "TextbookLesson");

            migrationBuilder.DropTable(
                name: "TextbookBook");

            migrationBuilder.DropIndex(
                name: "IX_Question_LessonId",
                table: "Question");

            migrationBuilder.DropColumn(
                name: "LessonId",
                table: "Question");
        }
    }
}
