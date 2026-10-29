namespace FullstackTemplate.Server.Resources.Entrypoints.Grpc;

using Serilog.Core;
using Serilog.Events;

/// <summary>
/// Grpc.AspNetCore.Server logs each error status at Information: "Error status code '{StatusCode}' with detail
/// '{Detail}' raised." The detail is the message for the caller, and it can hold text that the caller typed, such as a
/// field name in a sort order. So the log keeps the status code and gets a fixed text in place of the detail.
/// </summary>
public sealed class GrpcStatusDetailRedaction : ILogEventEnricher
{
    private const string CallHandler = "Grpc.AspNetCore.Server.ServerCallHandler";
    private const string Detail = "Detail";
    private const string NotLogged = "(not logged)";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (logEvent.Properties.TryGetValue(Constants.SourceContextPropertyName, out var source)
            && source is ScalarValue { Value: CallHandler }
            && logEvent.Properties.ContainsKey(Detail))
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(Detail, NotLogged));
    }
}
