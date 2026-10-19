namespace FullstackTemplate.Bff;

using System.Runtime.CompilerServices;
using Serilog;
using Serilog.Core;
using Serilog.Core.Enrichers;

public static class LoggerExtensions
{
    /// <summary>
    /// Adds the call site to the next log event, so a log line leads straight to the code that wrote it.
    /// <para>
    /// <c>CallSite</c> is the short form (<c>LoginTraceLink.cs:Capture@30</c>) that the
    /// console shows. <c>code.function</c>, <c>code.file.path</c>, and <c>code.line.number</c> use the
    /// same attribute names as the call-site tags on server spans, so one search finds both.
    /// </para>
    /// Usage: <c>_logger.Here().Information("Login started for {Provider}", provider);</c>
    /// </summary>
    /// <param name="logger">The logger to write the event to.</param>
    /// <param name="memberName">Filled by the compiler. Do not pass a value.</param>
    /// <param name="filePath">Filled by the compiler. Do not pass a value.</param>
    /// <param name="lineNumber">Filled by the compiler. Do not pass a value.</param>
    public static ILogger Here(
        this ILogger logger,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        return logger.ForContext(new ILogEventEnricher[]
        {
            new PropertyEnricher("CallSite", $"{Path.GetFileName(filePath)}:{memberName}@{lineNumber}"),
            new PropertyEnricher("code.function", memberName),
            new PropertyEnricher("code.file.path", filePath),
            new PropertyEnricher("code.line.number", lineNumber),
        });
    }
}
