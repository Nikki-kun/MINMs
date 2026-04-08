using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MINMs.Server.Controllers;
using MINMs.Server.Models.Dtos;
using MINMs.Server.Services;
using Moq;
using Xunit;

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
    public async Task Upload_WithValidFile_ReturnsOkWithFileUploadResponseDto()
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
        var response = Assert.IsType<FileUploadResponseDto>(okResult.Value);

        Assert.Equal("The file is uploaded", response.Message);
        Assert.Contains(fileName, response.ObjectName);
        Assert.NotNull(response.ObjectName);
        Assert.Contains("_", response.ObjectName);

        var guidPart = response.ObjectName.Split('_')[0];
        Assert.True(Guid.TryParse(guidPart, out _));

        _storageServiceMock.Verify(x => x.UploadFileAsync(fileMock.Object, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Upload_WithValidFile_ReturnsOkWithCorrectStatusCode()
    {
        // Arrange
        var fileMock = CreateMockFile("test.txt", "content");

        _storageServiceMock
            .Setup(x => x.UploadFileAsync(fileMock.Object, It.IsAny<string>()))
            .ReturnsAsync((true, null));

        // Act
        var result = await _controller.Upload(fileMock.Object);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task Upload_WithNullFile_ReturnsBadRequestWithErrorMessage()
    {
        // Act
        var result = await _controller.Upload(null!);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
        Assert.Equal("The file is not selected", badRequestResult.Value);
    }

    [Fact]
    public async Task Upload_WithEmptyFile_ReturnsBadRequestWithErrorMessage()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(0);

        // Act
        var result = await _controller.Upload(fileMock.Object);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
        Assert.Equal("The file is not selected", badRequestResult.Value);
        _storageServiceMock.Verify(x => x.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Upload_WhenStorageServiceReturnsError_ReturnsInternalServerErrorWithErrorMessage()
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
        FileUploadResponseDto? capturedResponse = null;

        _storageServiceMock
            .Setup(x => x.UploadFileAsync(fileMock.Object, It.IsAny<string>()))
            .ReturnsAsync((true, null));

        // Act
        var result = await _controller.Upload(fileMock.Object);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<FileUploadResponseDto>(okResult.Value);

        Assert.EndsWith($"_{fileName}", response.ObjectName);

        var guidPart = response.ObjectName[..36];
        Assert.True(Guid.TryParse(guidPart, out _));
    }

    [Fact]
    public async Task Upload_WithFileHavingSpecialCharactersInName_HandlesCorrectly()
    {
        // Arrange
        var fileName = "my file (1) @#$%.jpg";
        var fileMock = CreateMockFile(fileName, "content");

        _storageServiceMock
            .Setup(x => x.UploadFileAsync(fileMock.Object, It.IsAny<string>()))
            .ReturnsAsync((true, null));

        // Act
        var result = await _controller.Upload(fileMock.Object);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<FileUploadResponseDto>(okResult.Value);

        Assert.EndsWith($"_{fileName}", response.ObjectName);
        Assert.Contains(fileName, response.ObjectName);
    }

    [Fact]
    public async Task Upload_WithLargeFile_HandlesCorrectly()
    {
        // Arrange
        var fileName = "largefile.bin";
        var fileMock = CreateMockFile(fileName, new string('A', 1000000)); // 1MB content

        _storageServiceMock
            .Setup(x => x.UploadFileAsync(fileMock.Object, It.IsAny<string>()))
            .ReturnsAsync((true, null));

        // Act
        var result = await _controller.Upload(fileMock.Object);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<FileUploadResponseDto>(okResult.Value);

        Assert.Equal("The file is uploaded", response.Message);
        Assert.Contains(fileName, response.ObjectName);
        _storageServiceMock.Verify(x => x.UploadFileAsync(fileMock.Object, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Upload_WithMultipleFiles_GeneratesDifferentObjectNames()
    {
        // Arrange
        var fileNames = new[] { "file1.txt", "file2.txt", "file3.txt" };
        var uploadedObjectNames = new List<string>();

        _storageServiceMock
            .Setup(x => x.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
            .ReturnsAsync((true, null));

        // Act
        foreach (var fileName in fileNames)
        {
            var fileMock = CreateMockFile(fileName, "content");
            var result = await _controller.Upload(fileMock.Object);
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<FileUploadResponseDto>(okResult.Value);
            uploadedObjectNames.Add(response.ObjectName);
        }

        // Assert
        Assert.Equal(3, uploadedObjectNames.Count);
        Assert.Distinct(uploadedObjectNames);

        foreach (var fileName in fileNames)
        {
            Assert.Contains(uploadedObjectNames, name => name.EndsWith($"_{fileName}"));
        }
    }

    #endregion

    #region Download Tests

    [Fact]
    public async Task Download_WithValidObjectName_ReturnsRedirectToUrl()
    {
        // Arrange
        var objectName = "file123.pdf";
        var expectedUrl = "https://minio.example.com/bucket/file123.pdf?X-Amz-Expires=3600";

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
    public async Task Download_WhenObjectNotFound_ReturnsNotFoundWithErrorMessage()
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
        Assert.Equal(404, notFoundResult.StatusCode);
        Assert.Equal("The file was not found", notFoundResult.Value);
    }

    [Fact]
    public async Task Download_WhenUrlIsEmpty_ReturnsNotFoundWithErrorMessage()
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
        Assert.Equal(404, notFoundResult.StatusCode);
        Assert.Equal("The file was not found", notFoundResult.Value);
    }

    [Fact]
    public async Task Download_WithObjectNameContainingSpecialCharacters_PassesCorrectlyToService()
    {
        // Arrange
        var objectName = "folder/subfolder/file name with spaces.pdf";
        var expectedUrl = "https://minio.example.com/presigned-url";

        _storageServiceMock
            .Setup(x => x.GetFileUrlAsync(objectName))
            .ReturnsAsync(expectedUrl);

        // Act
        await _controller.Download(objectName);

        // Assert
        _storageServiceMock.Verify(x => x.GetFileUrlAsync(objectName), Times.Once);
    }

    [Fact]
    public async Task Download_WithRussianCharactersInObjectName_HandlesCorrectly()
    {
        // Arrange
        var objectName = "файл_с_русским_названием.pdf";
        var expectedUrl = "https://minio.example.com/presigned-url";

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
    public async Task Download_WithEmptyObjectName_ReturnsNotFound()
    {
        // Arrange
        var objectName = "";

        _storageServiceMock
            .Setup(x => x.GetFileUrlAsync(objectName))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _controller.Download(objectName);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
        Assert.Equal("The file was not found", notFoundResult.Value);
    }

    [Fact]
    public async Task Download_WithNullObjectName_ReturnsNotFound()
    {
        // Arrange
        string? objectName = null;

        _storageServiceMock
            .Setup(x => x.GetFileUrlAsync(objectName!))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _controller.Download(objectName!);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
        Assert.Equal("The file was not found", notFoundResult.Value);
    }

    #endregion

    #region Integration Scenario Tests

    [Fact]
    public async Task UploadAndDownload_CompleteFlow_WorksCorrectly()
    {
        // Arrange
        var fileName = "completeflow.txt";
        var fileMock = CreateMockFile(fileName, "test content");
        string? uploadedObjectName = null;

        _storageServiceMock
            .Setup(x => x.UploadFileAsync(fileMock.Object, It.IsAny<string>()))
            .Callback<IFormFile, string>((_, objectName) => uploadedObjectName = objectName)
            .ReturnsAsync((true, null));

        var expectedUrl = "https://minio.example.com/presigned-url";
        _storageServiceMock
            .Setup(x => x.GetFileUrlAsync(It.IsAny<string>()))
            .ReturnsAsync(expectedUrl);

        // Act - Upload
        var uploadResult = await _controller.Upload(fileMock.Object);
        var okResult = Assert.IsType<OkObjectResult>(uploadResult);
        var uploadResponse = Assert.IsType<FileUploadResponseDto>(okResult.Value);

        // Act - Download
        var downloadResult = await _controller.Download(uploadResponse.ObjectName);
        var redirectResult = Assert.IsType<RedirectResult>(downloadResult);

        // Assert
        Assert.Equal("The file is uploaded", uploadResponse.Message);
        Assert.Contains(fileName, uploadResponse.ObjectName);
        Assert.Equal(expectedUrl, redirectResult.Url);

        _storageServiceMock.Verify(x => x.UploadFileAsync(fileMock.Object, It.IsAny<string>()), Times.Once);
        _storageServiceMock.Verify(x => x.GetFileUrlAsync(uploadResponse.ObjectName), Times.Once);
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

    #endregion
}