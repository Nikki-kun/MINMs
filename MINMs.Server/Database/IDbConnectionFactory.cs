using System.Data;

namespace MINMs.Server.Database;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
