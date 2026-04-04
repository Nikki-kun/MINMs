using System.Data;

namespace MINMs.Server.Database;

/// <summary>
/// Создаёт подключения к основной БД приложения.
/// </summary>
public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
