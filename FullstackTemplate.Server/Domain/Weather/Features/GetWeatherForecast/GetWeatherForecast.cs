namespace FullstackTemplate.Server.Domain.Weather.Features;

using System.ComponentModel.DataAnnotations;
using Dtos;
using MediatR;

public static class GetWeatherForecast
{
    public sealed record Query([property: Range(Forecaster.MinDays, Forecaster.MaxDays)] int? Days)
        : IRequest<WeatherForecast[]>;

    public sealed class Handler : IRequestHandler<Query, WeatherForecast[]>
    {
        public Task<WeatherForecast[]> Handle(Query request, CancellationToken cancellationToken)
        {
            var forecast = Forecaster.NextDays(request.Days ?? Forecaster.DefaultDays)
                .Select(day => new WeatherForecast(day.Date, day.TemperatureC, day.Summary))
                .ToArray();
            return Task.FromResult(forecast);
        }
    }
}
