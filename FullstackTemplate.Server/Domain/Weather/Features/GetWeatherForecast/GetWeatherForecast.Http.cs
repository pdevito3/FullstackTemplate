namespace FullstackTemplate.Server.Domain.Weather.Features;

using System.ComponentModel.DataAnnotations;
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
        .AllowAnonymous()
        .ProducesValidationProblem();

    /// <summary>
    /// Gets the weather forecast for the next 1 to 14 days, 5 by default. The caller does not need to sign in.
    /// </summary>
    public static async Task<Ok<WeatherForecast[]>> Handle(
        [Range(Forecaster.MinDays, Forecaster.MaxDays)] int? days,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetWeatherForecast.Query(days), cancellationToken);
        return TypedResults.Ok(result);
    }
}
