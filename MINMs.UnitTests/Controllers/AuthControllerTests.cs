using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MINMs.Server.Controllers;
using MINMs.Server.Models.Dtos;
using MINMs.Server.Services;
using Moq;

namespace MINMs.UnitTests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _authServiceMock = new();
    private readonly Mock<IUserSearchService> _userSearchServiceMock = new();
    private readonly Mock<IJwtSessionService> _jwtSessionServiceMock = new();
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _controller = new AuthController(
            _authServiceMock.Object,
            _userSearchServiceMock.Object,
            _jwtSessionServiceMock.Object);
    }

    [Fact]
    public async Task Register_WhenCreated_Returns201WithAuthResponse()
    {
        var request = new RegisterRequest
        {
            Username = "Display Name",
            Password = "password12",
        };
        var createdAt = DateTime.UtcNow;
        var expected = new AuthResponse
        {
            AccessToken = "jwt-token",
            ExpiresInSeconds = 3600,
            UserId = 42,
            Login = "valid_login",
            Username = "Display Name",
            UserCreatedAt = createdAt,
        };
        _authServiceMock
            .Setup(x => x.RegisterAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegisterOutcome.Created(expected));

        var result = await _controller.Register(request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
        Assert.Same(expected, objectResult.Value);
    }

    [Fact]
    public async Task Register_WhenDuplicateLogin_Returns409()
    {
        var request = new RegisterRequest
        {
            Username = "User",
            Password = "password12",
        };
        _authServiceMock
            .Setup(x => x.RegisterAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegisterOutcome.DuplicateLogin);

        var result = await _controller.Register(request, CancellationToken.None);

        var objectResult = Assert.IsType<ConflictObjectResult>(result);
        Assert.NotNull(objectResult.Value);
    }

    [Fact]
    public async Task Register_WhenInvalidLogin_Returns400()
    {
        var request = new RegisterRequest
        {
            Username = "User",
            Password = "password12",
        };
        _authServiceMock
            .Setup(x => x.RegisterAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegisterOutcome.InvalidLogin);

        var result = await _controller.Register(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Register_WhenCreatedWithNullResponse_Returns500()
    {
        var request = new RegisterRequest
        {
            Username = "User",
            Password = "password12",
        };
        _authServiceMock
            .Setup(x => x.RegisterAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegisterOutcome(RegisterOutcomeKind.Created, Response: null));

        var result = await _controller.Register(request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    }

    [Fact]
    public async Task Register_PassesCancellationTokenToService()
    {
        var request = new RegisterRequest
        {
            Username = "User",
            Password = "password12",
        };
        var cancellationToken = new CancellationToken(true);
        _authServiceMock
            .Setup(x => x.RegisterAsync(request, cancellationToken))
            .ReturnsAsync(RegisterOutcome.InvalidLogin);

        await _controller.Register(request, cancellationToken);

        _authServiceMock.Verify(x => x.RegisterAsync(request, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Register_WhenServiceThrowsException_PropagatesException()
    {
        var request = new RegisterRequest
        {
            Username = "User",
            Password = "password12",
        };
        var expected = new InvalidOperationException("db error");
        _authServiceMock
            .Setup(x => x.RegisterAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _controller.Register(request, CancellationToken.None));

        Assert.Same(expected, exception);
    }
}
