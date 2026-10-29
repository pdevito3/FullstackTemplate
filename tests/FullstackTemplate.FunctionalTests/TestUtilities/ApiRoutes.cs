namespace FullstackTemplate.FunctionalTests.TestUtilities;

public static class ApiRoutes
{
    public const string Base = "api/v1";
    public const string Health = "health";
    public const string Alive = "alive";

    public static class Users
    {
        public static string GetList => $"{Base}/users";
        public static string GetCurrent => $"{Base}/users/me";
        public static string GetRecord(string id) => $"{Base}/users/{id}";
        public static string GetByIdentifier(string identifier) => $"{Base}/users/by-identifier/{identifier}";
        public static string Create => $"{Base}/users";
        public static string Initiate => $"{Base}/users/initiate";
        public static string Put(string id) => $"{Base}/users/{id}";
        public static string Delete(string id) => $"{Base}/users/{id}";
        public static string UpdateRole(string id) => $"{Base}/users/{id}/role";
        public static string AddPermission(string id) => $"{Base}/users/{id}/permissions";
        public static string RemovePermission(string id, string permission) => $"{Base}/users/{id}/permissions/{permission}";
    }

    public static class Tenants
    {
        public static string GetList => $"{Base}/tenants";
        public static string Create => $"{Base}/tenants";
        public static string GetRecord(string id) => $"{Base}/tenants/{id}";
        public static string Put(string id) => $"{Base}/tenants/{id}";
        public static string Delete(string id) => $"{Base}/tenants/{id}";
    }

    public static class Agents
    {
        public static string GetList => $"{Base}/agents";
        public static string Create => $"{Base}/agents";
        public static string GetRecord(string id) => $"{Base}/agents/{id}";
        public static string AddPermission(string id) => $"{Base}/agents/{id}/permissions";
        public static string RemovePermission(string id, string permission) => $"{Base}/agents/{id}/permissions/{permission}";
    }

    public static class AuditEntries
    {
        public static string GetPage(string entityId) => $"{Base}/audit-entries?entityId={Uri.EscapeDataString(entityId)}";
    }

    // example:start
    public static class Weather
    {
        public static string Get => $"{Base}/weather";
        public static string GetSecure => $"{Base}/weather/secure";
    }

    public static class Venues
    {
        public static string GetList => $"{Base}/venues";
        public static string Create => $"{Base}/venues";
    }

    public static class Workshops
    {
        public static string GetList => $"{Base}/workshops";
        public static string Create => $"{Base}/workshops";
        public static string Get(string id) => $"{Base}/workshops/{id}";
        public static string Update(string id) => $"{Base}/workshops/{id}";
        public static string Schedule(string id) => $"{Base}/workshops/{id}/schedule";
        public static string Cancel(string id) => $"{Base}/workshops/{id}/cancel";
        public static string Complete(string id) => $"{Base}/workshops/{id}/complete";
    }
    // example:end
}
