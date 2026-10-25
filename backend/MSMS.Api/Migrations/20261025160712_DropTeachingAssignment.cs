using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MSMS.Api.Migrations
{
    /// <inheritdoc />
    public partial class DropTeachingAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeachingAssignment");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeachingAssignment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssignedByPersonId = table.Column<int>(type: "int", nullable: true),
                    ClassId = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    TeacherId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(CONVERT([datetime2](0),(sysdatetimeoffset() AT TIME ZONE 'SE Asia Standard Time')))"),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Teaching__3214EC07EC22014A", x => x.Id);
                    table.ForeignKey(
                        name: "FK__TeachingA__Assig__2DE6D218",
                        column: x => x.AssignedByPersonId,
                        principalTable: "Person",
                        principalColumn: "PersonId");
                    table.ForeignKey(
                        name: "FK__TeachingA__Class__2B0A656D",
                        column: x => x.ClassId,
                        principalTable: "Class",
                        principalColumn: "ClassId");
                    table.ForeignKey(
                        name: "FK__TeachingA__Subje__2BFE89A6",
                        column: x => x.SubjectId,
                        principalTable: "Subject",
                        principalColumn: "SubjectId");
                    table.ForeignKey(
                        name: "FK__TeachingA__Teach__2CF2ADDF",
                        column: x => x.TeacherId,
                        principalTable: "StaffProfile",
                        principalColumn: "StaffId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TA_ClassSubject",
                table: "TeachingAssignment",
                columns: new[] { "ClassId", "SubjectId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TA_Teacher",
                table: "TeachingAssignment",
                columns: new[] { "TeacherId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignment_AssignedByPersonId",
                table: "TeachingAssignment",
                column: "AssignedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignment_SubjectId",
                table: "TeachingAssignment",
                column: "SubjectId");
        }
    }
}
