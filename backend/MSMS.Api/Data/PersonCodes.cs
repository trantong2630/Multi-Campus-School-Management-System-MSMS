using Microsoft.EntityFrameworkCore;

namespace MSMS.Api.Data;

public static class PersonCodes
{
    public const string Student = "HS";
    public const string Staff = "CB";
    public const string Parent = "PH";

    /// <summary>
    /// Mã hồ sơ = tiền tố + 6 chữ số lấy từ MỘT bộ đếm chung cho cả ba loại (BR-03).
    /// NEXT VALUE FOR không dùng được trong subquery nên không gọi thêm toán tử LINQ nào sau SqlQuery.
    /// </summary>
    public static async Task<string> NextAsync(MsmsDbContext db, string prefix)
    {
        var rows = await db.Database.SqlQuery<int>($"SELECT NEXT VALUE FOR dbo.seq_PersonCode AS Value").ToListAsync();
        return $"{prefix}{rows[0]}";
    }
}
