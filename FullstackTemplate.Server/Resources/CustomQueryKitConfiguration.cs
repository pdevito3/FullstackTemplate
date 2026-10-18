namespace FullstackTemplate.Server.Resources;

using QueryKit.Configuration;

public class CustomQueryKitConfiguration(Action<QueryKitSettings>? configureSettings = null)
    : QueryKitConfiguration(settings =>
    {
        // A filter that is too long or too deeply nested gets 400, so that one request cannot use a lot of parse time
        settings.MaxInputLength = FilterMaxLength;
        settings.MaxNestingDepth = FilterMaxNestingDepth;
        configureSettings?.Invoke(settings);
    })
{
    public const int FilterMaxLength = 2000;
    public const int FilterMaxNestingDepth = 10;
}
