using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MINMs.Server.Controllers;
using MINMs.Server.Services;
using Moq;

namespace MINMs.UnitTests.Controllers;

public class FilesControllerTests
{
    private readonly Mock<IMinioStorageService> _storageServiceMock;
    private readonly FilesController _controller;

    public FilesControllerTests()
    {
        _storageServiceMock = new Mock<IMinioStorageService>();
        _controller = new FilesController(_storageServiceMock.Object);
    }

    #region Upload Tests

    [Fact]
    public async Task Upload_WithValidFile_ReturnsOkWithMessageAndObjectName()
    {
        // Arrange
        var fileName = "testfile.jpg";
        var fileMock = CreateMockFile(fileName, "fake file content");

        _storageServiceMock
            .Setup(x => x.UploadFileAsync(fileMock.Object, It.IsAny<string>()))
            .ReturnsAsync((true, null));

        // Act
        var result = await _controller.Upload(fileMock.Object);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;

        var message = GetPropertyValue<string>(response, "Message");
        var objectName = GetPropertyValue<string>(response, "ObjectName");

        Assert.Equal("The file is uploaded", message);
        Assert.Contains(fileName, objectName);
        _storageServiceMock.Verify(x => x.UploadFileAsync(fileMock.Object, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Upload_WithNullFile_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.Upload(null!);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("The file is not selected", badRequestResult.Value);
    }

    [Fact]
    public async Task Upload_WithEmptyFile_ReturnsBadRequest()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(0);

        // Act
        var result = await _controller.Upload(fileMock.Object);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("The file is not selected", badRequestResult.Value);
    }

    [Fact]
    public async Task Upload_WhenStorageServiceReturnsError_ReturnsInternalServerError()
    {
        // Arrange
        var fileMock = CreateMockFile("test.txt", "content");
        var errorMessage = "MinIO connection failed";

        _storageServiceMock
            .Setup(x => x.UploadFileAsync(fileMock.Object, It.IsAny<string>()))
            .ReturnsAsync((false, errorMessage));

        // Act
        var result = await _controller.Upload(fileMock.Object);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
        Assert.Equal(errorMessage, statusCodeResult.Value);
    }

    [Fact]
    public async Task Upload_WhenStorageServiceReturnsErrorWithoutMessage_ReturnsDefaultErrorMessage()
    {
        // Arrange
        var fileMock = CreateMockFile("test.txt", "content");

        _storageServiceMock
            .Setup(x => x.UploadFileAsync(fileMock.Object, It.IsAny<string>()))
            .ReturnsAsync((false, null));

        // Act
        var result = await _controller.Upload(fileMock.Object);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
        Assert.Equal("Error loading to the storage", statusCodeResult.Value);
    }

    [Fact]
    public async Task Upload_GeneratesUniqueObjectNameWithGuid()
    {
        // Arrange
        var fileName = "document.pdf";
        var fileMock = CreateMockFile(fileName, "content");
        string? capturedObjectName = null;

        _storageServiceMock
            .Setup(x => x.UploadFileAsync(fileMock.Object, It.IsAny<string>()))
            .Callback<IFormFile, string>((_, objectName) => capturedObjectName = objectName)
            .ReturnsAsync((true, null));

        // Act
        await _controller.Upload(fileMock.Object);

        // Assert
        Assert.NotNull(capturedObjectName);
        Assert.EndsWith($"_{fileName}", capturedObjectName);

        var guidPart = capturedObjectName[..36];
        Assert.True(Guid.TryParse(guidPart, out _));
    }

    #endregion

    #region Download Tests

    [Fact]
    public async Task Download_WithValidObjectName_ReturnsRedirectToUrl()
    {
        // Arrange
        var objectName = "file123.pdf";
        var expectedUrl = "https://minio.example.com/bucket/file123.pdf";

        _storageServiceMock
            .Setup(x => x.GetFileUrlAsync(objectName))
            .ReturnsAsync(expectedUrl);

        // Act
        var result = await _controller.Download(objectName);

        // Assert
        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal(expectedUrl, redirectResult.Url);
    }

    [Fact]
    public async Task Download_WhenObjectNotFound_ReturnsNotFound()
    {
        // Arrange
        var objectName = "nonexistent.txt";

        _storageServiceMock
            .Setup(x => x.GetFileUrlAsync(objectName))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _controller.Download(objectName);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("The file was not found", notFoundResult.Value);
    }

    [Fact]
    public async Task Download_WhenUrlIsEmpty_ReturnsNotFound()
    {
        // Arrange
        var objectName = "empty.txt";

        _storageServiceMock
            .Setup(x => x.GetFileUrlAsync(objectName))
            .ReturnsAsync(string.Empty);

        // Act
        var result = await _controller.Download(objectName);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("The file was not found", notFoundResult.Value);
    }

    #endregion

    #region Helper Methods

    private static Mock<IFormFile> CreateMockFile(string fileName, string content)
    {
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(content);
        writer.Flush();
        stream.Position = 0;

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.Length).Returns(stream.Length);
        fileMock.Setup(f => f.OpenReadStream()).Returns(stream);
        fileMock.Setup(f => f.ContentDisposition).Returns($"form-data; name=\"file\"; filename=\"{fileName}\"");
        fileMock.Setup(f => f.ContentType).Returns("application/octet-stream");

        return fileMock;
    }

    private static T? GetPropertyValue<T>(object obj, string propertyName)
    {
        var property = obj.GetType().GetProperty(propertyName);
        if (property == null)
            throw new ArgumentException($"Property '{propertyName}' not found on type '{obj.GetType()}'");

        var value = property.GetValue(obj);
        return value == null ? default : (T)value;
    }

    #endregion
}