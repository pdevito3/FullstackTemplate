namespace FullstackTemplate.Server.Examples.Weather.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class GetWeatherForecastEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapGet("/weather", Handle)
        .WithName("GetWeatherForecast")
        .WithTags("Weather")
        .MapToApiVersion(ApiVersions.V1)
        .AllowAnonymous();

    /// <summary>
    /// Gets the weather forecast for the next five days. The caller does not need to sign in.
    /// </summary>
    public static async Task<Ok<WeatherForecast[]>> Handle(IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetWeatherForecast.Query(), cancellationToken);
        return TypedResults.Ok(result);
    }
}
