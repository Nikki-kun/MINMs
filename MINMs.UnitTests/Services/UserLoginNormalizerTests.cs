using MINMs.Server.Services;

namespace MINMs.UnitTests.Services;

public sealed class UserLoginNormalizerTests
{
    [Theory]
    [InlineData("  @User_Name  ", "user_name")]
    [InlineData("LOGIN123", "login123")]
    [InlineData("   @abc_1", "abc_1")]
    public void Normalize_ConvertsToExpectedValue(string raw, string expected)
    {
        var normalized = UserLoginNormalizer.Normalize(raw);

        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("valid_login")]
    [InlineData("abc12")]
    [InlineData("user_1234567890")]
    public void IsValid_ReturnsTrue_ForValidLogins(string login)
    {
        var isValid = UserLoginNormalizer.IsValid(login);

        Assert.True(isValid);
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("with space")]
    [InlineData("bad-dash")]
    [InlineData("русский")]
    public void IsValid_ReturnsFalse_ForInvalidLogins(string login)
    {
        var isValid = UserLoginNormalizer.IsValid(login);

        Assert.False(isValid);
    }
}
