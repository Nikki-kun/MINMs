using System.Data;
using System.Data.Common;

namespace MINMs.Server.Database;

public static class DbConnectionFactoryExtensions
{
    public static async Task<TResult> WithConnectionAsync<TResult>(
        this IDbConnectionFactory factory,
        Func<IDbConnection, Task<TResult>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(action);

        var connection = factory.CreateConnection();
        try
        {
            if (connection is DbConnection dbConnection)
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            else
                connection.Open();

            return await action(connection).ConfigureAwait(false);
        }
        finally
        {
            connection.Dispose();
        }
    }

    public static async Task WithConnectionAsync(
        this IDbConnectionFactory factory,
        Func<IDbConnection, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(action);

        var connection = factory.CreateConnection();
        try
        {
            if (connection is DbConnection dbConnection)
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            else
                connection.Open();

            await action(connection).ConfigureAwait(false);
        }
        finally
        {
            connection.Dispose();
        }
    }
}
