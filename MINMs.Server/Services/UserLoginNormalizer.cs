using System.Text.RegularExpressions;

namespace MINMs.Server.Services;

/// <summary>
/// Единые правила для логина: обрезка, снятие префикса <c>@</c>, нижний регистр и проверка допустимых символов.
/// </summary>
public static partial class UserLoginNormalizer
{
    public const int MinLength = 5;
    public const int MaxLength = 32;

    [GeneratedRegex("^[a-z0-9_]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidPattern();

    public static string Normalize(string raw)
    {
        var s = raw.Trim();
        if (s.StartsWith("@", StringComparison.Ordinal))
            s = s[1..];
        return s.ToLowerInvariant();
    }

    public static bool IsValid(string normalized) =>
        normalized.Length is >= MinLength and <= MaxLength
        && ValidPattern().IsMatch(normalized);
}
