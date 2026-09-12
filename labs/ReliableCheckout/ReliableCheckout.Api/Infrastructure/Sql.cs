using Microsoft.Data.Sqlite;

namespace ReliableCheckout.Infrastructure;

internal static class Sql
{
    public static SqliteCommand Command(SqliteConnection connection, SqliteTransaction? transaction, string text,
        params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = text;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }

    public static async Task<int> ExecuteAsync(SqliteConnection connection, SqliteTransaction? transaction,
        string text, CancellationToken token, params (string Name, object? Value)[] parameters)
    {
        await using var command = Command(connection, transaction, text, parameters);
        return await command.ExecuteNonQueryAsync(token);
    }
}
