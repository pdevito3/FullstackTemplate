namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net.Http.Json;
using System.Text.Json;
using FullstackTemplate.Bff.Tests.Fakes;

public static class HttpClientExtensions
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// A request as the SPA sends it to a BFF API endpoint: with <c>X-CSRF: 1</c>.
    /// </summary>
    public static Task<HttpResponseMessage> SendApiAsync(this HttpClient client, HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-CSRF", "1");
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> GetApiAsync(this HttpClient client, string path) =>
        client.SendApiAsync(HttpMethod.Get, path);

    /// <summary>
    /// The request that the <see cref="EchoApi"/> received.
    /// </summary>
    public static async Task<EchoedRequest> ReadEchoAsync(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<EchoedRequest>(WebJson))!;

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
}
