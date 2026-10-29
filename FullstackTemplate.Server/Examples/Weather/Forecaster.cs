namespace FullstackTemplate.Server.Examples.Weather;

/// <summary>Makes random forecasts for the sample weather features.</summary>
public static class Forecaster
{
    private static readonly string[] Summaries =
        ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

    private const int MinTemperatureC = -20;
    private const int MaxTemperatureC = 55;

    /// <summary>One forecast for each of the next five days.</summary>
    public static IEnumerable<(DateOnly Date, int TemperatureC, string Summary)> NextFiveDays() =>
        Enumerable.Range(1, 5).Select(index =>
        {
            var temperatureC = Random.Shared.Next(MinTemperatureC, MaxTemperatureC);
            return (DateOnly.FromDateTime(DateTime.Now.AddDays(index)), temperatureC, SummaryFor(temperatureC));
        });

    // The summary matches the temperature: the range splits into one equal band for each summary.
    private static string SummaryFor(int temperatureC)
        => Summaries[(temperatureC - MinTemperatureC) * Summaries.Length / (MaxTemperatureC - MinTemperatureC)];
}
