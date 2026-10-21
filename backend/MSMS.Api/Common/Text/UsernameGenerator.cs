namespace MSMS.Api.Common.Text;

public static class UsernameGenerator
{
    /// <summary>BR-03: tên + chữ cái đầu họ và đệm + mã hồ sơ, bỏ dấu, chữ thường. Trần Đức Kiên + HS186194 → kientdhs186194.</summary>
    public static string Build(string fullName, string personCode)
    {
        var words = TextNormalizer.Normalize(fullName).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var given = words[^1];
        var initials = string.Concat(words[..^1].Select(w => w[0]));
        return TextNormalizer.RemoveDiacritics(given + initials + personCode).ToLowerInvariant();
    }
}
