using System.Globalization;
using System.Text;

namespace MSMS.Api.Common.Text;

/// <summary>Chuẩn hóa chuỗi trước khi lưu và so sánh (Conventions §3): trim, gộp khoảng trắng, NFC.</summary>
public static class TextNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var parts = value.Normalize(NormalizationForm.FormC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts);
    }

    /// <summary>Bỏ dấu, xử lý riêng đ/Đ vì Unicode decomposition bỏ sót chữ này.</summary>
    public static string RemoveDiacritics(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var sb = new StringBuilder(value.Length);
        foreach (var c in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(c switch { 'đ' => 'd', 'Đ' => 'D', _ => c });
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

}
