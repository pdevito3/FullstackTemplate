namespace FullstackTemplate.Server.Databases;

using Npgsql;

public static class ConnectionStringHelper
{
    /// <summary>
    /// Converts PostgreSQL URI to Npgsql connection string.
    /// Handles Northflank's malformed URI with ?sslmode (no value).
    /// </summary>
    public static string? ConvertPostgresUri(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString) || !connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) &&
            !connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
            return connectionString;

        // Remove malformed query string (e.g., ?sslmode with no value)
        var queryIndex = connectionString.IndexOf('?');
        if (queryIndex > 0)
            connectionString = connectionString[..queryIndex];

        var uri = new Uri(connectionString);
        var userInfo = uri.UserInfo.Split(':');
        var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "";
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var database = uri.AbsolutePath.TrimStart('/');

        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = database,
            Username = username,
            Password = password,
            SslMode = SslMode.Require
        }.ConnectionString;
    }
}