namespace FullstackTemplate.Server.Domain.Weather;

/// <summary>Makes random forecasts for the sample weather features.</summary>
public static class Forecaster
{
    private static readonly string[] Summaries =
        ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

    public const int MinDays = 1;
    public const int MaxDays = 14;
    public const int DefaultDays = 5;

    private const int MinTemperatureC = -20;
    private const int MaxTemperatureC = 55;

    public static IEnumerable<(DateOnly Date, int TemperatureC, string Summary)> NextDays(int days) =>
        Enumerable.Range(1, days).Select(index =>
        {
            var temperatureC = Random.Shared.Next(MinTemperatureC, MaxTemperatureC);
            return (DateOnly.FromDateTime(DateTime.Now.AddDays(index)), temperatureC, SummaryFor(temperatureC));
        });

    // The summary matches the temperature: the range splits into one equal band for each summary.
    private static string SummaryFor(int temperatureC)
        => Summaries[(temperatureC - MinTemperatureC) * Summaries.Length / (MaxTemperatureC - MinTemperatureC)];
}
