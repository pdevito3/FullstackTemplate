namespace FullstackTemplate.Server.Resources;

using QueryKit.Configuration;

public class CustomQueryKitConfiguration(Action<QueryKitSettings>? configureSettings = null)
    : QueryKitConfiguration(settings =>
    {
        // A filter that is too long or too deeply nested gets 400, so that one request cannot use a lot of parse time
        settings.MaxInputLength = FilterMaxLength;
        settings.MaxNestingDepth = FilterMaxNestingDepth;
        // By default QueryKit puts each filter value into the SQL text, and the Npgsql span copies that text to db.query.text.
        // A parameter keeps the user-entered text out of the trace.
        settings.ParameterizeFilterValues = true;
        configureSettings?.Invoke(settings);
    })
{
    public const int FilterMaxLength = 2000;
    public const int FilterMaxNestingDepth = 10;
}
