using System.Data;
using Npgsql;

namespace KakaoTalkAutomation;

public static class PostgresClient
{
    public static NpgsqlConnection CreateConnection(PostgresSettings settings)
    {
        return new NpgsqlConnection(BuildConnectionString(settings));
    }

    public static async Task TestConnectionAsync(PostgresSettings settings, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection(settings);
        await connection.OpenAsync(cancellationToken);
        await connection.CloseAsync();
    }

    public static async Task<DataTable> ExecuteQueryAsync(
        PostgresSettings settings,
        string sql,
        CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection(settings);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var table = new DataTable();
        table.Load(reader);
        return table;
    }

    private static string BuildConnectionString(PostgresSettings settings)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = settings.Host,
            Port = settings.Port,
            Database = settings.Database,
            Username = settings.Username,
            Password = settings.Password,
            SslMode = settings.SslModeRequire ? SslMode.Require : SslMode.Disable,
            Timeout = 5,
            CommandTimeout = 30
        };

        if (!string.IsNullOrWhiteSpace(settings.SearchPath))
            builder.SearchPath = settings.SearchPath;

        return builder.ConnectionString;
    }
}
