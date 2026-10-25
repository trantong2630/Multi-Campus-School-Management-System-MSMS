using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MSMS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTimetableVersionClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImportSnapshot",
                table: "TimetableVersion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TimetableVersionClass",
                columns: table => new
                {
                    VersionId = table.Column<int>(type: "int", nullable: false),
                    ClassId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimetableVersionClass", x => new { x.VersionId, x.ClassId });
                    table.ForeignKey(
                        name: "FK_TimetableVersionClass_Class",
                        column: x => x.ClassId,
                        principalTable: "Class",
                        principalColumn: "ClassId");
                    table.ForeignKey(
                        name: "FK_TimetableVersionClass_Version",
                        column: x => x.VersionId,
                        principalTable: "TimetableVersion",
                        principalColumn: "VersionId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimetableVersionClass_ClassId",
                table: "TimetableVersionClass",
                column: "ClassId");

            // Phiên bản đã có: các lớp của phiên bản là các lớp có ô trong phiên bản đó.
            migrationBuilder.Sql(
                "INSERT INTO TimetableVersionClass (VersionId, ClassId) SELECT DISTINCT VersionId, ClassId FROM TimetableEntry");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TimetableVersionClass");

            migrationBuilder.DropColumn(
                name: "ImportSnapshot",
                table: "TimetableVersion");
        }
    }
}
