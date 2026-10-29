namespace FullstackTemplate.Server.Examples.Weather.Features;

using Dtos;
using MediatR;

public static class GetWeatherForecast
{
    public sealed record Query : IRequest<WeatherForecast[]>;

    public sealed class Handler : IRequestHandler<Query, WeatherForecast[]>
    {
        public Task<WeatherForecast[]> Handle(Query request, CancellationToken cancellationToken)
        {
            var forecast = Forecaster.NextFiveDays()
                .Select(day => new WeatherForecast(day.Date, day.TemperatureC, day.Summary))
                .ToArray();
            return Task.FromResult(forecast);
        }
    }
}
