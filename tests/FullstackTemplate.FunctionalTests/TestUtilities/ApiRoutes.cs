namespace FullstackTemplate.FunctionalTests.TestUtilities;

public static class ApiRoutes
{
    public const string Base = "api/v1";
    public const string Health = "health";
    public const string Alive = "alive";

    public static class Weather
    {
        public static string Get => $"{Base}/weather";
        public static string GetSecure => $"{Base}/weather/secure";
    }
}
