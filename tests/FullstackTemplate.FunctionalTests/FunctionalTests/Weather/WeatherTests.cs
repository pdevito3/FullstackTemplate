namespace FullstackTemplate.FunctionalTests.FunctionalTests.Weather;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain.Weather.Dtos;
using Shouldly;
using TestUtilities;

public class WeatherTests : TestBase
{
    [Test]
    public async Task an_anonymous_caller_gets_the_forecast_of_five_days()
    {
        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Weather.Get);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var forecast = (await result.Content.ReadFromJsonAsync<WeatherForecast[]>()).ShouldNotBeNull();
        forecast.Length.ShouldBe(5);
        forecast.ShouldAllBe(day => !string.IsNullOrEmpty(day.Summary));
    }

    [Test]
    public async Task the_secure_forecast_names_the_caller()
    {
        // Arrange
        FactoryClient.SignInAsAppUser("weather-user");

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Weather.GetSecure);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var forecast = (await result.Content.ReadFromJsonAsync<SecureWeatherForecast[]>()).ShouldNotBeNull();
        forecast.Length.ShouldBe(5);
        forecast.ShouldAllBe(day => day.RequestedBy == "weather-user");
    }
}
