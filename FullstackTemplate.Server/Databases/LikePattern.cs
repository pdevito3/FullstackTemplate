namespace FullstackTemplate.Server.Databases;

/// <summary>
/// Builds a LIKE pattern from the text of a caller. The pattern escapes <c>%</c>, <c>_</c>, and <c>\</c>, so they match
/// as plain characters. Pass the pattern and <see cref="EscapeCharacter"/> to the three-argument
/// <c>EF.Functions.ILike</c> or <c>EF.Functions.Like</c>.
/// </summary>
public static class LikePattern
{
    /// <summary>
    /// The escape character of each pattern. The two-argument <c>ILike</c> and <c>Like</c> send <c>ESCAPE ''</c>, so a
    /// backslash escapes nothing. BannedSymbols.txt bans them.
    /// </summary>
    public const string EscapeCharacter = @"\";

    public static string Containing(string text) => $"%{Exactly(text)}%";

    public static string StartingWith(string text) => $"{Exactly(text)}%";

    public static string Exactly(string text)
        => text.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
