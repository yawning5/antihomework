using System.Data;
using Npgsql;

namespace KakaoTalkAutomation;

public sealed class ChatOutRepository
{
    public async Task<ChatOutMessage?> GetNextAsync(PostgresSettings settings, CancellationToken cancellationToken = default)
    {
        const string sql = """
            select msg_id, room_nm, msg
            from chat_out
            order by msg_id asc
            limit 1;
            """;

        await using var connection = PostgresClient.CreateConnection(settings);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new ChatOutMessage
        {
            MsgId = reader.GetInt64(reader.GetOrdinal("msg_id")),
            RoomName = reader.GetString(reader.GetOrdinal("room_nm")),
            Message = reader.IsDBNull(reader.GetOrdinal("msg"))
                ? string.Empty
                : reader.GetString(reader.GetOrdinal("msg"))
        };
    }

    public async Task<DataTable> GetPreviewAsync(PostgresSettings settings, int limit = 20, CancellationToken cancellationToken = default)
    {
        const string sql = """
            select msg_id, room_nm, msg, reg_dt
            from chat_out
            order by msg_id asc
            limit @limit;
            """;

        await using var connection = PostgresClient.CreateConnection(settings);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var table = new DataTable();
        table.Load(reader);
        return table;
    }

    public async Task DeleteAsync(PostgresSettings settings, long msgId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            delete from chat_out
            where msg_id = @msgId;
            """;

        await using var connection = PostgresClient.CreateConnection(settings);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("msgId", msgId);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected != 1)
            throw new InvalidOperationException($"Delete affected {affected} rows for msg_id={msgId}.");
    }
}
