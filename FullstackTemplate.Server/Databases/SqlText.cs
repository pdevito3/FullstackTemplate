namespace FullstackTemplate.Server.Databases;

/// <summary>
/// Reads the words of a Postgres SQL text: the identifiers and the key words, bare or in double quotes. It skips
/// comments, string literals, dollar-quoted text, numbers, and parameters, so a name in a comment or in a value does
/// not count. Postgres folds a bare word to lower case, so a bare word is in lower case here.
/// </summary>
internal static class SqlText
{
    private static readonly HashSet<string> WriteKeywords = ["insert", "update", "delete", "merge", "truncate", "copy"];

    internal readonly record struct Word(string Name, bool Quoted);

    /// <summary>
    /// The first of <paramref name="tables"/> that a word of the SQL names, or null. A word that is a column or an
    /// alias with the name of a table also counts, so the guard errs toward a refusal.
    /// </summary>
    public static string? FirstNamed(string sql, IReadOnlySet<string> tables)
        => Words(sql).Select(word => word.Name).FirstOrDefault(tables.Contains);

    /// <summary>
    /// True when a bare word of the SQL is INSERT, UPDATE, DELETE, MERGE, TRUNCATE, or COPY. A row lock
    /// (<c>FOR UPDATE</c>) also counts, so the guard errs toward a refusal.
    /// </summary>
    public static bool IsWrite(string sql) => Words(sql).Any(word => !word.Quoted && WriteKeywords.Contains(word.Name));

    internal static IEnumerable<Word> Words(string sql)
    {
        var i = 0;
        while (i < sql.Length)
        {
            var c = sql[i];
            if (c == '-' && Next(sql, i) == '-')
                i = AfterLineComment(sql, i);
            else if (c == '/' && Next(sql, i) == '*')
                i = AfterBlockComment(sql, i);
            else if (c == '\'')
                i = AfterQuoted(sql, i, '\'', backslashEscapes: false);
            else if (c == '"')
            {
                var end = AfterQuoted(sql, i, '"', backslashEscapes: false);
                var name = sql[(i + 1)..Math.Max(i + 1, end - 1)].Replace("\"\"", "\"", StringComparison.Ordinal);
                yield return new Word(name, Quoted: true);
                i = end;
            }
            else if (c == '$' && DollarTag(sql, i) is { } tag)
                i = AfterDollarQuoted(sql, i, tag);
            else if (char.IsLetterOrDigit(c) || c == '_')
            {
                var start = i;
                while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] is '_' or '$'))
                    i++;

                var word = sql[start..i];
                if (word is "E" or "e" && i < sql.Length && sql[i] == '\'')
                    i = AfterQuoted(sql, i, '\'', backslashEscapes: true);
                else if (!char.IsAsciiDigit(word[0]))
                    yield return new Word(word.ToLowerInvariant(), Quoted: false);
            }
            else
                i++;
        }
    }

    private static char? Next(string sql, int i) => i + 1 < sql.Length ? sql[i + 1] : null;

    private static int AfterLineComment(string sql, int start)
    {
        var end = sql.IndexOf('\n', start);
        return end < 0 ? sql.Length : end + 1;
    }

    /// <summary>Postgres nests block comments, so a comment ends at the <c>*/</c> of its own <c>/*</c>.</summary>
    private static int AfterBlockComment(string sql, int start)
    {
        var depth = 0;
        var i = start;
        while (i < sql.Length)
        {
            if (sql[i] == '/' && Next(sql, i) == '*')
            {
                depth++;
                i += 2;
            }
            else if (sql[i] == '*' && Next(sql, i) == '/')
            {
                i += 2;
                if (--depth == 0)
                    return i;
            }
            else
                i++;
        }

        return sql.Length;
    }

    /// <summary>A doubled quote is a quote in the text. In an <c>E'...'</c> string, a backslash escapes the next character.</summary>
    private static int AfterQuoted(string sql, int start, char quote, bool backslashEscapes)
    {
        var i = start + 1;
        while (i < sql.Length)
        {
            if (backslashEscapes && sql[i] == '\\')
                i += 2;
            else if (sql[i] != quote)
                i++;
            else if (Next(sql, i) == quote)
                i += 2;
            else
                return i + 1;
        }

        return sql.Length;
    }

    /// <summary>The opening <c>$tag$</c> or <c>$$</c> of dollar-quoted text, or null for a parameter such as <c>$1</c>.</summary>
    private static string? DollarTag(string sql, int start)
    {
        var i = start + 1;
        if (i < sql.Length && (char.IsLetter(sql[i]) || sql[i] == '_'))
            while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] == '_'))
                i++;

        return i < sql.Length && sql[i] == '$' ? sql[start..(i + 1)] : null;
    }

    private static int AfterDollarQuoted(string sql, int start, string tag)
    {
        var end = sql.IndexOf(tag, start + tag.Length, StringComparison.Ordinal);
        return end < 0 ? sql.Length : end + tag.Length;
    }
}
