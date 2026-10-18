namespace FullstackTemplate.FunctionalTests.TestUtilities;

public static class ApiRoutes
{
    public const string Base = "api/v1";
    public const string Health = "health";
    public const string Alive = "alive";

    public static class Users
    {
        public static string GetList => $"{Base}/users";
        public static string GetRecord(Guid id) => $"{Base}/users/{id}";
        public static string GetByIdentifier(string identifier) => $"{Base}/users/by-identifier/{identifier}";
        public static string Create => $"{Base}/users";
        public static string Initiate => $"{Base}/users/initiate";
        public static string Put(Guid id) => $"{Base}/users/{id}";
        public static string Delete(Guid id) => $"{Base}/users/{id}";
        public static string Role(Guid id) => $"{Base}/users/{id}/role";
    }

    public static class Tenants
    {
        public static string GetList => $"{Base}/tenants";
        public static string Create => $"{Base}/tenants";
        public static string GetRecord(Guid id) => $"{Base}/tenants/{id}";
        public static string Put(Guid id) => $"{Base}/tenants/{id}";
        public static string Delete(Guid id) => $"{Base}/tenants/{id}";
    }

    public static class Weather
    {
        public static string Get => $"{Base}/weather";
        public static string GetSecure => $"{Base}/weather/secure";
    }
}
