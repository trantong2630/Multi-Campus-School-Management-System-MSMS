using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MSMS.Api.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyLrnPractice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Đổi mô hình Đề → Bài tập chứa câu hỏi trực tiếp: dữ liệu LRN cũ không chuyển được, xóa trước khi đổi khóa.
            migrationBuilder.Sql("DELETE FROM SubmissionAnswerOption; DELETE FROM SubmissionAnswer; DELETE FROM AiFeedback; DELETE FROM Submission; DELETE FROM AssignmentRecipient; DELETE FROM Assignment;");

            migrationBuilder.DropForeignKey(
                name: "FK__AiFeedbac__Appro__0B5CAFEA",
                table: "AiFeedback");

            migrationBuilder.DropForeignKey(
                name: "FK__AiFeedbac__Assig__0880433F",
                table: "AiFeedback");

            migrationBuilder.DropForeignKey(
                name: "FK__AiFeedbac__Stude__09746778",
                table: "AiFeedback");

            migrationBuilder.DropForeignKey(
                name: "FK__Assignmen__ExamI__74794A92",
                table: "Assignment");

            migrationBuilder.DropForeignKey(
                name: "FK__Assignmen__Score__7849DB76",
                table: "Assignment");

            migrationBuilder.DropForeignKey(
                name: "FK__Submissio__ExamQ__03BB8E22",
                table: "SubmissionAnswer");

            migrationBuilder.DropTable(
                name: "ExamQuestion");

            migrationBuilder.DropTable(
                name: "Exam");

            migrationBuilder.DropIndex(
                name: "IX_Assignment_Class",
                table: "Assignment");

            migrationBuilder.DropIndex(
                name: "IX_Assignment_ScorePublishedByPersonId",
                table: "Assignment");

            migrationBuilder.DropIndex(
                name: "IX_AiFeedback_ApprovedByPersonId",
                table: "AiFeedback");

            migrationBuilder.DropIndex(
                name: "IX_AiFeedback_StudentId",
                table: "AiFeedback");

            migrationBuilder.DropIndex(
                name: "UQ_AiFeedback",
                table: "AiFeedback");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "Assignment");

            migrationBuilder.DropColumn(
                name: "ScorePublishedAt",
                table: "Assignment");

            migrationBuilder.DropColumn(
                name: "ScorePublishedByPersonId",
                table: "Assignment");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Assignment");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "AiFeedback");

            migrationBuilder.DropColumn(
                name: "ApprovedByPersonId",
                table: "AiFeedback");

            migrationBuilder.DropColumn(
                name: "AssignmentId",
                table: "AiFeedback");

            migrationBuilder.DropColumn(
                name: "EditedContent",
                table: "AiFeedback");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "AiFeedback");

            migrationBuilder.DropColumn(
                name: "StudentId",
                table: "AiFeedback");

            migrationBuilder.RenameColumn(
                name: "ExamQuestionId",
                table: "SubmissionAnswer",
                newName: "AssignmentQuestionId");

            migrationBuilder.RenameIndex(
                name: "IX_SubmissionAnswer_ExamQuestionId",
                table: "SubmissionAnswer",
                newName: "IX_SubmissionAnswer_AssignmentQuestionId");

            migrationBuilder.RenameColumn(
                name: "ExamId",
                table: "Assignment",
                newName: "SubjectId");

            migrationBuilder.RenameIndex(
                name: "IX_Assignment_Exam",
                table: "Assignment",
                newName: "IX_Assignment_Subject");

            migrationBuilder.AddColumn<short>(
                name: "DurationMinutes",
                table: "Assignment",
                type: "smallint",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "AiFeedback",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldDefaultValue: "Draft");

            migrationBuilder.AddColumn<long>(
                name: "SubmissionId",
                table: "AiFeedback",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "AssignmentQuestion",
                columns: table => new
                {
                    AssignmentQuestionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssignmentId = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<int>(type: "int", nullable: false),
                    OrderNo = table.Column<short>(type: "smallint", nullable: false),
                    Points = table.Column<decimal>(type: "decimal(4,2)", nullable: false, defaultValue: 1m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentQuestion", x => x.AssignmentQuestionId);
                    table.ForeignKey(
                        name: "FK_AssignmentQuestion_Assignment_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "Assignment",
                        principalColumn: "AssignmentId");
                    table.ForeignKey(
                        name: "FK_AssignmentQuestion_Question_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Question",
                        principalColumn: "QuestionId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assignment_Class",
                table: "Assignment",
                columns: new[] { "ClassId", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "UQ_AiFeedback",
                table: "AiFeedback",
                column: "SubmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentQuestion_QuestionId",
                table: "AssignmentQuestion",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "UQ_AssignmentQuestion",
                table: "AssignmentQuestion",
                columns: new[] { "AssignmentId", "QuestionId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AiFeedback_Submission_SubmissionId",
                table: "AiFeedback",
                column: "SubmissionId",
                principalTable: "Submission",
                principalColumn: "SubmissionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Assignment_Subject_SubjectId",
                table: "Assignment",
                column: "SubjectId",
                principalTable: "Subject",
                principalColumn: "SubjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_SubmissionAnswer_AssignmentQuestion_AssignmentQuestionId",
                table: "SubmissionAnswer",
                column: "AssignmentQuestionId",
                principalTable: "AssignmentQuestion",
                principalColumn: "AssignmentQuestionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiFeedback_Submission_SubmissionId",
                table: "AiFeedback");

            migrationBuilder.DropForeignKey(
                name: "FK_Assignment_Subject_SubjectId",
                table: "Assignment");

            migrationBuilder.DropForeignKey(
                name: "FK_SubmissionAnswer_AssignmentQuestion_AssignmentQuestionId",
                table: "SubmissionAnswer");

            migrationBuilder.DropTable(
                name: "AssignmentQuestion");

            migrationBuilder.DropIndex(
                name: "IX_Assignment_Class",
                table: "Assignment");

            migrationBuilder.DropIndex(
                name: "UQ_AiFeedback",
                table: "AiFeedback");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "Assignment");

            migrationBuilder.DropColumn(
                name: "SubmissionId",
                table: "AiFeedback");

            migrationBuilder.RenameColumn(
                name: "AssignmentQuestionId",
                table: "SubmissionAnswer",
                newName: "ExamQuestionId");

            migrationBuilder.RenameIndex(
                name: "IX_SubmissionAnswer_AssignmentQuestionId",
                table: "SubmissionAnswer",
                newName: "IX_SubmissionAnswer_ExamQuestionId");

            migrationBuilder.RenameColumn(
                name: "SubjectId",
                table: "Assignment",
                newName: "ExamId");

            migrationBuilder.RenameIndex(
                name: "IX_Assignment_Subject",
                table: "Assignment",
                newName: "IX_Assignment_Exam");

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                table: "Assignment",
                type: "datetime2(0)",
                precision: 0,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScorePublishedAt",
                table: "Assignment",
                type: "datetime2(0)",
                precision: 0,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScorePublishedByPersonId",
                table: "Assignment",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Assignment",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "AiFeedback",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Draft",
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldDefaultValue: "Pending");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "AiFeedback",
                type: "datetime2(0)",
                precision: 0,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedByPersonId",
                table: "AiFeedback",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignmentId",
                table: "AiFeedback",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EditedContent",
                table: "AiFeedback",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "AiFeedback",
                type: "datetime2(0)",
                precision: 0,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StudentId",
                table: "AiFeedback",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Exam",
                columns: table => new
                {
                    ExamId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CopiedFromExamId = table.Column<int>(type: "int", nullable: true),
                    OwnerPersonId = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))"),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_Assignment_Class",
                table: "Assignment",
                columns: new[] { "ClassId", "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Assignment_ScorePublishedByPersonId",
                table: "Assignment",
                column: "ScorePublishedByPersonId");

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

            migrationBuilder.AddForeignKey(
                name: "FK__AiFeedbac__Appro__0B5CAFEA",
                table: "AiFeedback",
                column: "ApprovedByPersonId",
                principalTable: "Person",
                principalColumn: "PersonId");

            migrationBuilder.AddForeignKey(
                name: "FK__AiFeedbac__Assig__0880433F",
                table: "AiFeedback",
                column: "AssignmentId",
                principalTable: "Assignment",
                principalColumn: "AssignmentId");

            migrationBuilder.AddForeignKey(
                name: "FK__AiFeedbac__Stude__09746778",
                table: "AiFeedback",
                column: "StudentId",
                principalTable: "StudentProfile",
                principalColumn: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK__Assignmen__ExamI__74794A92",
                table: "Assignment",
                column: "ExamId",
                principalTable: "Exam",
                principalColumn: "ExamId");

            migrationBuilder.AddForeignKey(
                name: "FK__Assignmen__Score__7849DB76",
                table: "Assignment",
                column: "ScorePublishedByPersonId",
                principalTable: "Person",
                principalColumn: "PersonId");

            migrationBuilder.AddForeignKey(
                name: "FK__Submissio__ExamQ__03BB8E22",
                table: "SubmissionAnswer",
                column: "ExamQuestionId",
                principalTable: "ExamQuestion",
                principalColumn: "ExamQuestionId");
        }
    }
}
