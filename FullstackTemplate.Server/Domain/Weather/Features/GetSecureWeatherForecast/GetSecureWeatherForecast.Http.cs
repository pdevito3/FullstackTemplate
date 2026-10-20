namespace FullstackTemplate.Server.Domain.Weather.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class GetSecureWeatherForecastEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapGet("/weather/secure", Handle)
        .WithName("GetSecureWeatherForecast")
        .WithTags("Weather")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization();

    /// <summary>
    /// Gets the weather forecast for the next five days, with the name of the caller.
    /// </summary>
    public static async Task<Ok<SecureWeatherForecast[]>> Handle(IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSecureWeatherForecast.Query(), cancellationToken);
        return TypedResults.Ok(result);
    }
}
