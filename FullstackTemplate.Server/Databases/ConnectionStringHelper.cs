namespace FullstackTemplate.Server.Databases;

using Npgsql;

/// <summary>
/// Corrects Northflank's malformed PostgreSQL connection string for use with .NET's Npgsql driver.
/// </summary>
/// <remarks>
/// <para>
/// This helper exists to work around two issues with the POSTGRES_URI provided by Northflank:
/// </para>
/// <list type="number">
///   <item>
///     <term>Format mismatch</term>
///     <description>
///       Northflank provides URIs in PostgreSQL format (postgresql://user:pass@host:port/db),
///       but .NET's Npgsql driver expects ADO.NET format (Host=...;Username=...;Password=...).
///     </description>
///   </item>
///   <item>
///     <term>Malformed query string</term>
///     <description>
///       The URI ends with ?sslmode with no value (e.g., .../_13a9c48d44a3?sslmode).
///       It should be ?sslmode=require or omitted entirely. This causes parsing failures
///       even with Npgsql's built-in URI parser.
///     </description>
///   </item>
/// </list>
/// <para>
/// If Northflank fixes their connection string format or you don't need this correction, this helper can be removed.
/// </para>
/// </remarks>
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