namespace FullstackTemplate.Server.Resources;

using QueryKit.Configuration;

public class CustomQueryKitConfiguration(Action<QueryKitSettings>? configureSettings = null)
    : QueryKitConfiguration(settings => { configureSettings?.Invoke(settings); })
{
    // configure custom global settings here
    // settings.EqualsOperator = "eq";
}