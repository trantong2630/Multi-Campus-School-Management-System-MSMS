using System.Net.Mail;
using System.Text.RegularExpressions;
using MSMS.Api.Common.Text;

namespace MSMS.Api.Common.Errors;

/// <summary>Kiểm dữ liệu đầu vào dùng chung. Thông báo nêu nguyên nhân kèm cách xử lý (Conventions §4.6).</summary>
public static partial class Guard
{
    [GeneratedRegex("^[A-Za-z0-9]+$")]
    private static partial Regex AlnumRegex();

    public static string Required(string? value, string label, int maxLength)
    {
        var v = TextNormalizer.Normalize(value);
        if (v.Length == 0) throw ApiException.BadRequest($"{label} không được để trống. Nhập {label.ToLowerInvariant()} rồi lưu lại.");
        if (v.Length > maxLength) throw ApiException.BadRequest($"{label} tối đa {maxLength} ký tự. Rút gọn rồi lưu lại.");
        return v;
    }

    public static string? Optional(string? value, string label, int maxLength)
    {
        var v = TextNormalizer.Normalize(value);
        if (v.Length == 0) return null;
        if (v.Length > maxLength) throw ApiException.BadRequest($"{label} tối đa {maxLength} ký tự. Rút gọn rồi lưu lại.");
        return v;
    }

    /// <summary>Viết tắt môn và viết tắt giáo viên chỉ gồm chữ và số, vì dấu "-" là dấu tách trong ô MÔN-GV (BR-07, BR-10).</summary>
    public static string Abbreviation(string? value, string label, int maxLength = 10)
    {
        var v = Required(value, label, maxLength);
        if (!AlnumRegex().IsMatch(v))
            throw ApiException.BadRequest($"{label} chỉ gồm chữ và số, không dấu cách, không dấu gạch. Sửa lại rồi lưu.");
        return v;
    }

    /// <summary>Số định danh cá nhân: đúng 12 chữ số, khóa nhận diện học sinh và cán bộ khi import (BR-10, BR-11).</summary>
    public static bool IsNationalId(string value) => value.Length == 12 && value.All(char.IsAsciiDigit);

    public static string NationalId(string? value)
    {
        var v = (value ?? "").Trim();
        if (v.Length == 0) throw ApiException.BadRequest("Số định danh cá nhân không được để trống. Điền đủ 12 chữ số.");
        if (!IsNationalId(v)) throw ApiException.BadRequest("Số định danh cá nhân phải gồm đúng 12 chữ số.");
        return v;
    }

    public static bool IsAlnum(string value) => AlnumRegex().IsMatch(value);

    /// <summary>Địa chỉ email dạng tên@miền.đuôi: một địa chỉ duy nhất (không tên hiển thị, không dấu cách) và tên miền có dấu chấm.</summary>
    public static bool IsEmail(string value) =>
        value.Length <= 254 && MailAddress.TryCreate(value, out var mail) && mail.Address == value && mail.Host.Contains('.');
}
