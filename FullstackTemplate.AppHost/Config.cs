namespace FullstackTemplate.AppHost;

internal static class Config
{
    // Set before WithDataVolume, because Postgres 18 uses a different data path. The test projects compile this file,
    // so the tests run the same Postgres version as the app.
    public const string PostgresImageTag = "17.6";
}
