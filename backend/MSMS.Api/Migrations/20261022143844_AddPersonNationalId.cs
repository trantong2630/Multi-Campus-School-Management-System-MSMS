using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MSMS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonNationalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NationalId",
                table: "Person",
                type: "char(12)",
                unicode: false,
                fixedLength: true,
                maxLength: 12,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Person_NationalId",
                table: "Person",
                column: "NationalId",
                unique: true,
                filter: "([NationalId] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Person_NationalId",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "NationalId",
                table: "Person");
        }
    }
}
