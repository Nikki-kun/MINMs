using Microsoft.AspNetCore.Identity.Data;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using MINMs.Server.Database;
using MySqlConnector;
using System.Data;
using MINMs.Server.Models.Dtos;

namespace MINMs.Server.Services;

public interface IMinioStorageService
{
    Task<(bool Success, string? Error, int? FileId)> UploadFileAsync(string login, IFormFile file, string objectName, string originalFilename);
    Task<string> GetFileUrlAsync(string objectName, int expiryInSeconds = 3600);
    Task<bool> DeleteFileAsync(string login, string objectName, int? fileId = null);
    Task<bool> FileExistsAsync(string objectName);
    Task<List<FileResponseDto>> GetUserFilesAsync(string login);
}

public sealed class MinioStorageService(
    IMinioClient minioClient,
    string bucketName,
    IUserSearchService userSearchService,
    IDbConnectionFactory connectionFactory) : IMinioStorageService
{
    private readonly IMinioClient _minioClient = minioClient;
    private readonly string _bucketName = bucketName;

    public async Task<(bool Success, string? Error, int? FileId)> UploadFileAsync(string login, IFormFile file, string objectName, string originalFilename)
    {
        var user = await userSearchService.GetInternalByUserLoginAsync(login);
        if (user is null)
            return (false, "User not found", null);

        int? fileId = null;

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

            fileId = await SaveFileRecordAsync(user.UserId, originalFilename, file.Length, file.ContentType, objectName);

            return (true, null, fileId);
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

            return (false, msg, null);
        }
    }

    private async Task<int> SaveFileRecordAsync(int ownerUserId, string originalFilename, long sizeBytes, string contentType, string objectName)
    {
        return await connectionFactory.WithConnectionAsync(async connection =>
        {
            if (connection is not MySqlConnection mysql)
                throw new InvalidOperationException("Expected MySqlConnection.");

            await using var cmd = mysql.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO files (owner_user_id, original_filename, size_bytes, content_type, object_name)
                VALUES (@owner_user_id, @original_filename, @size_bytes, @content_type, @object_name);
                SELECT LAST_INSERT_ID();
                """;
            cmd.Parameters.AddWithValue("@owner_user_id", ownerUserId);
            cmd.Parameters.AddWithValue("@original_filename", originalFilename);
            cmd.Parameters.AddWithValue("@size_bytes", sizeBytes);
            cmd.Parameters.AddWithValue("@content_type", contentType);
            cmd.Parameters.AddWithValue("@object_name", objectName);

            var fileId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return fileId;
        });
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

    public async Task<bool> DeleteFileAsync(string login, string objectName, int? fileId = null)
    {
        try
        {
            var removeArgs = new RemoveObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName);

            await _minioClient.RemoveObjectAsync(removeArgs);

            if (fileId.HasValue)
            {
                await DeleteFileRecordAsync(fileId.Value);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task DeleteFileRecordAsync(int fileId)
    {
        await connectionFactory.WithConnectionAsync(async connection =>
        {
            if (connection is not MySqlConnection mysql)
                throw new InvalidOperationException("Expected MySqlConnection.");

            await using var cmd = mysql.CreateCommand();
            cmd.CommandText = "DELETE FROM files WHERE file_id = @file_id";
            cmd.Parameters.AddWithValue("@file_id", fileId);

            await cmd.ExecuteNonQueryAsync();
            return 0;
        });
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

    public async Task<List<FileResponseDto>> GetUserFilesAsync(string login)
    {
        var user = await userSearchService.GetInternalByUserLoginAsync(login);
        if (user is null)
            return new List<FileResponseDto>();

        return await connectionFactory.WithConnectionAsync(async connection =>
        {
            if (connection is not MySqlConnection mysql)
                throw new InvalidOperationException("Expected MySqlConnection.");

            await using var cmd = mysql.CreateCommand();
            cmd.CommandText = """
                SELECT file_id, original_filename, size_bytes, created_at, object_name
                FROM files
                WHERE owner_user_id = @user_id
                ORDER BY created_at DESC
                """;
            cmd.Parameters.AddWithValue("@user_id", user.UserId);

            var files = new List<FileResponseDto>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                files.Add(new FileResponseDto
                {
                    Id = reader.GetInt32("file_id"),
                    Name = reader.GetString("original_filename"),
                    ObjectName = reader.GetString("object_name"),
                    Size = reader.GetInt64("size_bytes"),
                    UploadedAt = reader.GetDateTime("created_at").ToString("o")  // ← changed here
                });
            }

            return files;
        });
    }
}
