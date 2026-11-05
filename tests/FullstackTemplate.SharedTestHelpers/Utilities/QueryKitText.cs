namespace FullstackTemplate.SharedTestHelpers.Utilities;

public static class QueryKitText
{
    /// <summary>
    /// Puts text in a QueryKit literal. Text with no quote goes in quotes. QueryKit has no escape in quotes, so text
    /// with a quote goes in a raw string: its delimiter is one quote longer than the longest run of quotes in the text,
    /// and one space pads each side. QueryKit removes the padding, so a quote at an end of the text stays in the text.
    /// </summary>
    public static string Quote(string text)
    {
        if (!text.Contains('"'))
        {
            return $"""
                "{text}"
                """;
        }

        var (run, longestRun) = (0, 0);
        foreach (var c in text)
        {
            run = c == '"' ? run + 1 : 0;
            longestRun = Math.Max(longestRun, run);
        }

        var delimiter = new string('"', Math.Max(3, longestRun + 1));
        return $"{delimiter} {text} {delimiter}";
    }
}
