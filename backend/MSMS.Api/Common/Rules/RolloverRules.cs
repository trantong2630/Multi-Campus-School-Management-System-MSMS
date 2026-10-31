using System.Text.RegularExpressions;

namespace MSMS.Api.Common.Rules;

/// <summary>Quy tắc gợi ý khi kết chuyển năm học (SCH-06, 4.2.35). Chỉ là gợi ý, người dùng sửa được trước khi tạo lớp.</summary>
public static class RolloverRules
{
    public const byte MaxGrade = 9;

    /// <summary>Khối của lớp năm sau: tăng 1. Khối 9 không có khối 10 (học sinh tốt nghiệp) nên không gợi ý. Hệ thống không tự xét lưu ban.</summary>
    public static byte? NextGrade(byte grade) => grade < MaxGrade ? (byte)(grade + 1) : null;

    /// <summary>
    /// Gợi ý tên lớp năm sau: thay chữ số khối đứng riêng ở lần xuất hiện đầu tiên bằng khối mới (6A1 → 7A1, "Lớp 6B" → "Lớp 7B").
    /// Tên không có chữ số khối thì giữ nguyên để người dùng tự đặt.
    /// </summary>
    public static string SuggestName(string name, byte oldGrade, byte newGrade) =>
        new Regex($@"(?<!\d){oldGrade}(?!\d)").Replace(name, newGrade.ToString(), 1);
}
