namespace MINMs.Server.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "MINMs";
    public string Audience { get; set; } = "MINMs";
    public int ExpiresMinutes { get; set; } = 60;
}
