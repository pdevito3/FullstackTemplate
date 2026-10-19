namespace FullstackTemplate.FunctionalTests.TestUtilities;

public static class HttpClientExtensions
{
    public static async Task<HttpResponseMessage> GetRequestAsync(this HttpClient client, string url)
        => await client.GetAsync(url);
}
