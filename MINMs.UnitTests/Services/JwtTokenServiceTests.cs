using Microsoft.Extensions.Options;
using MINMs.Server.Options;
using MINMs.Server.Services;

namespace MINMs.UnitTests.Services;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void CreateAccessToken_ReturnsTokenAndPreservesJti()
    {
        var options = Options.Create(new JwtOptions
        {
            Key = "0123456789abcdef0123456789abcdef",
            Issuer = "MINMs.Tests",
            Audience = "MINMs.Tests.Client",
            ExpiresMinutes = 30,
        });
        var service = new JwtTokenService(options);

        var result = service.CreateAccessToken(userId: 42, login: "test_user", jti: "jti-123");

        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.Equal("jti-123", result.Jti);
        Assert.True(result.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public void CreateAccessToken_Throws_WhenKeyTooShort()
    {
        var options = Options.Create(new JwtOptions
        {
            Key = "short-key",
            ExpiresMinutes = 30,
        });
        var service = new JwtTokenService(options);

        Assert.Throws<InvalidOperationException>(() =>
            service.CreateAccessToken(userId: 1, login: "test_user", jti: "jti"));
    }
}
