namespace FullstackTemplate.FunctionalTests.FunctionalTests.Weather;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain.Weather.Dtos;
using Microsoft.AspNetCore.Mvc;
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
    [Arguments(1)]
    [Arguments(14)]
    public async Task an_anonymous_caller_gets_the_number_of_days_that_they_ask_for(int days)
    {
        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Weather.GetDays(days));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var forecast = (await result.Content.ReadFromJsonAsync<WeatherForecast[]>()).ShouldNotBeNull();
        forecast.Length.ShouldBe(days);
    }

    [Test]
    [Arguments(0)]
    [Arguments(15)]
    [Arguments(-1)]
    public async Task a_day_count_out_of_range_is_a_validation_problem(int days)
    {
        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Weather.GetDays(days));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Errors.ShouldContainKey("days");
    }

    [Test]
    public async Task a_day_count_that_is_not_a_number_is_rejected()
    {
        // Act
        var result = await FactoryClient.GetRequestAsync($"{ApiRoutes.Weather.Get}?days=abc");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task the_secure_forecast_names_the_caller()
    {
        // Arrange
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = "weather-user" });

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Weather.GetSecure);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var forecast = (await result.Content.ReadFromJsonAsync<SecureWeatherForecast[]>()).ShouldNotBeNull();
        forecast.Length.ShouldBe(5);
        forecast.ShouldAllBe(day => day.RequestedBy == "weather-user");
    }
}
