namespace FullstackTemplate.AppHost;

public static class Config
{
    // Matches the Northflank addon. Set before WithDataVolume, because Postgres 18 uses a different data path.
    public const string PostgresImageTag = "17.6";
}
