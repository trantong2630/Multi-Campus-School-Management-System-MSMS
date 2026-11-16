using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MSMS.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFeaturePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeaturePermission");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeaturePermission",
                columns: table => new
                {
                    AccountType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FeatureKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeaturePermission", x => new { x.AccountType, x.FeatureKey });
                    table.ForeignKey(
                        name: "FK_FeaturePermission_Account_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Account",
                        principalColumn: "AccountId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeaturePermission_UpdatedBy",
                table: "FeaturePermission",
                column: "UpdatedBy");
        }
    }
}
