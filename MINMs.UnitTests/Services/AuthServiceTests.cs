using Microsoft.Extensions.Options;
using MINMs.Server.Database;
using MINMs.Server.Models.Dtos;
using MINMs.Server.Options;
using MINMs.Server.Services;
using Moq;

namespace MINMs.UnitTests.Services;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_ReturnsNull_WhenLoginIsInvalid_AndDoesNotTouchDatabase()
    {
        var connectionFactory = new Mock<IDbConnectionFactory>(MockBehavior.Strict);
        var jwtOptions = Options.Create(new JwtOptions
        {
            Key = "0123456789abcdef0123456789abcdef",
            ExpiresMinutes = 60,
        });
        var jwtTokenService = new JwtTokenService(jwtOptions);
        var jwtSessionService = new Mock<IJwtSessionService>(MockBehavior.Strict);
        var service = new AuthService(connectionFactory.Object, jwtTokenService, jwtSessionService.Object);

        var result = await service.LoginAsync(new LoginRequest
        {
            Login = "bad login with spaces",
            Password = "password123",
        });

        Assert.Null(result);
        connectionFactory.Verify(x => x.CreateConnection(), Times.Never);
        jwtSessionService.VerifyNoOtherCalls();
    }
}
