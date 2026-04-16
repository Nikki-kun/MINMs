using Microsoft.AspNetCore.Mvc;
using MINMs.Server.Controllers;
using MINMs.Server.Models.Dtos;
using MINMs.Server.Services;
using Moq;

namespace MINMs.UnitTests.Controllers;

public class UsersControllerTests
{
    private readonly Mock<IUserSearchService> _searchServiceMock;
    private readonly UsersController _controller;

    public UsersControllerTests()
    {
        _searchServiceMock = new Mock<IUserSearchService>();
        _controller = new UsersController(_searchServiceMock.Object);
    }

    [Fact]
    public async Task Search_WithValidQuery_ReturnsOkResultWithUsers()
    {
        // Arrange
        var query = "john";
        var limit = 10;
        var expectedUsers = new List<UserPublicDto>
        {
            new() { Username = "john_doe" },
            new() { Username = "john_smith" }
        };

        _searchServiceMock
            .Setup(x => x.SearchByUsernameAsync(query, limit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUsers);

        // Act
        var result = await _controller.Search(query, limit, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedUsers = Assert.IsAssignableFrom<IReadOnlyList<UserPublicDto>>(okResult.Value);
        Assert.Equal(expectedUsers.Count, returnedUsers.Count);
        Assert.Equal(expectedUsers, returnedUsers);
    }

    [Fact]
    public async Task Search_WithNullQuery_UsesEmptyString()
    {
        // Arrange
        string? query = null;
        var limit = 20;
        var expectedUsers = new List<UserPublicDto>();

        _searchServiceMock
            .Setup(x => x.SearchByUsernameAsync(string.Empty, limit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUsers);

        // Act
        var result = await _controller.Search(query, limit, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expectedUsers, okResult.Value);
        _searchServiceMock.Verify(
            x => x.SearchByUsernameAsync(string.Empty, limit, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Search_WithEmptyQuery_ReturnsEmptyList()
    {
        // Arrange
        var query = "";
        var limit = 20;
        var expectedUsers = new List<UserPublicDto>();

        _searchServiceMock
            .Setup(x => x.SearchByUsernameAsync(query, limit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUsers);

        // Act
        var result = await _controller.Search(query, limit, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedUsers = Assert.IsAssignableFrom<IReadOnlyList<UserPublicDto>>(okResult.Value);
        Assert.Empty(returnedUsers);
    }

    [Fact]
    public async Task Search_WithDefaultLimit_UsesLimit20()
    {
        // Arrange
        var query = "test";
        var expectedUsers = new List<UserPublicDto>
        {
            new() { Username = "test_user" }
        };

        _searchServiceMock
            .Setup(x => x.SearchByUsernameAsync(query, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUsers);

        // Act
        var result = await _controller.Search(query, cancellationToken: CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expectedUsers, okResult.Value);
        _searchServiceMock.Verify(
            x => x.SearchByUsernameAsync(query, 20, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Search_WithCustomLimit_UsesProvidedLimit()
    {
        // Arrange
        var query = "admin";
        var limit = 5;
        var expectedUsers = new List<UserPublicDto>
        {
            new() { Username = "admin1" },
            new() { Username = "admin2" }
        };

        _searchServiceMock
            .Setup(x => x.SearchByUsernameAsync(query, limit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUsers);

        // Act
        var result = await _controller.Search(query, limit, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expectedUsers, okResult.Value);
        _searchServiceMock.Verify(
            x => x.SearchByUsernameAsync(query, limit, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Search_WhenServiceReturnsManyUsers_ReturnsAllUsers()
    {
        // Arrange
        var query = "user";
        var limit = 50;
        var expectedUsers = Enumerable.Range(1, 50)
            .Select(i => new UserPublicDto { Username = $"user{i}" })
            .ToList();

        _searchServiceMock
            .Setup(x => x.SearchByUsernameAsync(query, limit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUsers);

        // Act
        var result = await _controller.Search(query, limit, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedUsers = Assert.IsAssignableFrom<IReadOnlyList<UserPublicDto>>(okResult.Value);
        Assert.Equal(50, returnedUsers.Count);
    }

    [Fact]
    public async Task Search_PassesCancellationTokenToService()
    {
        // Arrange
        var query = "test";
        var cancellationToken = new CancellationToken(true);

        _searchServiceMock
            .Setup(x => x.SearchByUsernameAsync(query, 20, cancellationToken))
            .ReturnsAsync(new List<UserPublicDto>());

        // Act
        var result = await _controller.Search(query, cancellationToken: cancellationToken);

        // Assert
        _searchServiceMock.Verify(
            x => x.SearchByUsernameAsync(query, 20, cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Search_WhenServiceThrowsException_PropagatesException()
    {
        // Arrange
        var query = "error";
        var expectedException = new InvalidOperationException("Database connection failed");

        _searchServiceMock
            .Setup(x => x.SearchByUsernameAsync(query, 20, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _controller.Search(query, cancellationToken: CancellationToken.None));

        Assert.Equal("Database connection failed", exception.Message);
    }

    [Fact]
    public async Task Search_WithSpecialCharactersInQuery_HandlesCorrectly()
    {
        // Arrange
        var query = "user@domain";
        var limit = 10;
        var expectedUsers = new List<UserPublicDto>
        {
            new() { Username = "user@domain" }
        };

        _searchServiceMock
            .Setup(x => x.SearchByUsernameAsync(query, limit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUsers);

        // Act
        var result = await _controller.Search(query, limit, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedUsers = Assert.IsAssignableFrom<IReadOnlyList<UserPublicDto>>(okResult.Value);
        Assert.Single(returnedUsers);
        Assert.Equal("user@domain", returnedUsers[0].Username);
    }

    [Fact]
    public async Task Search_WithLimitZero_HandlesCorrectly()
    {
        // Arrange
        var query = "test";
        var limit = 0;
        var expectedUsers = new List<UserPublicDto>();

        _searchServiceMock
            .Setup(x => x.SearchByUsernameAsync(query, limit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUsers);

        // Act
        var result = await _controller.Search(query, limit, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedUsers = Assert.IsAssignableFrom<IReadOnlyList<UserPublicDto>>(okResult.Value);
        Assert.Empty(returnedUsers);
    }
}