using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace MINMs.Server.Services;

public interface IMinioStorageService
{
    Task<(bool Success, string? Error)> UploadFileAsync(IFormFile file, string objectName);
    Task<string> GetFileUrlAsync(string objectName, int expiryInSeconds = 3600);
    Task<bool> DeleteFileAsync(string objectName);
    Task<bool> FileExistsAsync(string objectName);
}

public sealed class MinioStorageService(IMinioClient minioClient, string bucketName) : IMinioStorageService
{
    private readonly IMinioClient _minioClient = minioClient;
    private readonly string _bucketName = bucketName;

    public async Task<(bool Success, string? Error)> UploadFileAsync(IFormFile file, string objectName)
    {
        try
        {
            using var stream = file.OpenReadStream();
            var putObjectArgs = new PutObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName)
                .WithStreamData(stream)
                .WithObjectSize(file.Length)
                .WithContentType(file.ContentType);

            await _minioClient.PutObjectAsync(putObjectArgs);
            return (true, null);
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            Console.WriteLine($"Ошибка загрузки: {msg}");
            if (msg.Contains("request time", StringComparison.OrdinalIgnoreCase) &&
                msg.Contains("too large", StringComparison.OrdinalIgnoreCase))
            {
                msg =
                    "Время на компьютере сильно расходится со временем MinIO (подпись запроса отклонена). "
                    + "Синхронизируйте дату и время Windows, затем перезапустите Docker Desktop или выполните wsl --shutdown и снова запустите контейнеры.";
            }

            return (false, msg);
        }
    }

    public async Task<string> GetFileUrlAsync(string objectName, int expiryInSeconds = 3600)
    {
        try
        {
            var presignedArgs = new PresignedGetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName)
                .WithExpiry(expiryInSeconds);

            return await _minioClient.PresignedGetObjectAsync(presignedArgs);
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task<bool> DeleteFileAsync(string objectName)
    {
        try
        {
            var removeArgs = new RemoveObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName);

            await _minioClient.RemoveObjectAsync(removeArgs);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> FileExistsAsync(string objectName)
    {
        try
        {
            var statArgs = new StatObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName);

            await _minioClient.StatObjectAsync(statArgs);
            return true;
        }
        catch (MinioException)
        {
            return false;
        }
    }
}

