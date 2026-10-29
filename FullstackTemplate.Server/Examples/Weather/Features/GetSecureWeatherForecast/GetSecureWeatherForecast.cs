namespace FullstackTemplate.Server.Examples.Weather.Features;

using Dtos;
using MediatR;
using Services;

public static class GetSecureWeatherForecast
{
    public sealed record Query : IRequest<SecureWeatherForecast[]>;

    public sealed class Handler(ICurrentUserService currentUserService) : IRequestHandler<Query, SecureWeatherForecast[]>
    {
        public Task<SecureWeatherForecast[]> Handle(Query request, CancellationToken cancellationToken)
        {
            var requestedBy = currentUserService.Name ?? currentUserService.UserIdentifier ?? "Unknown";

            var forecast = Forecaster.NextFiveDays()
                .Select(day => new SecureWeatherForecast(day.Date, day.TemperatureC, day.Summary, requestedBy))
                .ToArray();
            return Task.FromResult(forecast);
        }
    }
}
