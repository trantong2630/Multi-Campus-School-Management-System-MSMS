using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MSMS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogAppendOnlyTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // BR-28: Audit chỉ ghi thêm. Ứng dụng đang kết nối bằng sa nên DENY không có tác dụng, phải chặn bằng trigger.
            migrationBuilder.Sql("""
                CREATE TRIGGER dbo.TR_AuditLog_AppendOnly ON dbo.AuditLog
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 50028, N'Nhật ký chỉ ghi thêm, không được sửa hoặc xóa (BR-28).', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER dbo.TR_AuditLog_AppendOnly;");
        }
    }
}
