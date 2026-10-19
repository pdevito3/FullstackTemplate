namespace FullstackTemplate.FunctionalTests.TestUtilities;

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

/// <summary>
/// Makes IDs that must not match a record: a malformed value, a wrong prefix, upper case, and 300 characters.
/// </summary>
public static class MalformedIds
{
    public static readonly string[] Kinds = ["malformed", "wrong-prefix", "upper-case", "too-long"];

    /// <summary>Changes the <paramref name="id"/> of an existing record into an ID of the given kind.</summary>
    public static string From(string kind, string id, string wrongPrefix) => kind switch
    {
        "malformed" => "not-an-id",
        "wrong-prefix" => $"{wrongPrefix}{id[id.IndexOf('_')..]}",
        "upper-case" => $"{id[..id.IndexOf('_')]}{id[id.IndexOf('_')..].ToUpperInvariant()}",
        "too-long" => id + new string('a', 300 - id.Length),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>Makes one case for each target and each kind of ID.</summary>
    public static IEnumerable<(string, string)> Cases(params string[] targets)
        => targets.SelectMany(target => Kinds.Select(kind => (target, kind)));

    public static async Task ShouldBeNotFoundAsync(HttpResponseMessage result, string detail)
    {
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = (await result.Content.ReadFromJsonAsync<ProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status404NotFound);
        problem.Detail.ShouldBe(detail);
    }
}
