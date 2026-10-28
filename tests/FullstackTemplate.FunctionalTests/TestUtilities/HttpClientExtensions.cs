namespace FullstackTemplate.FunctionalTests.TestUtilities;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

public static class HttpClientExtensions
{
    /// <summary>Signs in as the user with <paramref name="subject"/>, through the first-party client (the BFF).</summary>
    public static void SignInAsAppUser(this HttpClient client, string subject)
        => client.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["sub"] = subject,
            ["azp"] = TestingWebApplicationFactory.FirstPartyClientId,
        });

    public static async Task<HttpResponseMessage> GetRequestAsync(this HttpClient client, string url)
        => await client.GetAsync(url);

    public static async Task<HttpResponseMessage> PostJsonRequestAsync(this HttpClient client, string url, object value)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return await client.PostAsJsonAsync(url, value, options);
    }

    public static async Task<HttpResponseMessage> PutJsonRequestAsync(this HttpClient client, string url, object value)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return await client.PutAsJsonAsync(url, value, options);
    }

    public static async Task<HttpResponseMessage> DeleteRequestAsync(this HttpClient client, string url)
        => await client.DeleteAsync(url);
}
