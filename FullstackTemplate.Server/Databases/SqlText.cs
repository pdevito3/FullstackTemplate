namespace FullstackTemplate.Server.Databases;

/// <summary>
/// Reads the tokens of a Postgres SQL text that the write guard needs: the identifiers and the key words, bare or in
/// double quotes, the dollar-quoted text, and the semicolons. It skips comments, string literals, numbers, and
/// parameters, so a name in a comment or in a value does not count. Postgres folds a bare word to lower case, so a
/// bare word is in lower case here.
/// </summary>
internal static class SqlText
{
    private static readonly HashSet<string> WriteKeywords = ["insert", "update", "delete", "merge", "truncate", "copy"];

    internal enum TokenKind
    {
        Word,
        QuotedName,
        DollarQuoted,
        Semicolon,
    }

    internal readonly record struct Token(TokenKind Kind, string Text);

    /// <summary>
    /// The first of <paramref name="tables"/> that a word of the SQL names, or null. A word that is a column or an
    /// alias with the name of a table also counts, so the guard errs toward a refusal.
    /// </summary>
    public static string? FirstNamed(string sql, IReadOnlySet<string> tables)
        => Tokens(sql)
            .Where(token => token.Kind is TokenKind.Word or TokenKind.QuotedName)
            .Select(token => token.Text)
            .FirstOrDefault(tables.Contains);

    /// <summary>
    /// True when a bare word of the SQL is INSERT, UPDATE, DELETE, MERGE, TRUNCATE, or COPY. A row lock
    /// (<c>FOR UPDATE</c>) also counts, so the guard errs toward a refusal.
    /// </summary>
    public static bool IsWrite(string sql)
        => Tokens(sql).Any(token => token.Kind == TokenKind.Word && WriteKeywords.Contains(token.Text));

    /// <summary>
    /// True when the SQL runs code whose writes the words do not show: a statement that starts with DO or CALL, or
    /// dollar-quoted text, which is the usual form of a function body. A function that a SELECT calls is not found.
    /// </summary>
    public static bool RunsHiddenCode(string sql)
    {
        var startsStatement = true;
        foreach (var token in Tokens(sql))
        {
            if (token.Kind == TokenKind.DollarQuoted
                || (startsStatement && token.Kind == TokenKind.Word && token.Text is "do" or "call"))
                return true;

            startsStatement = token.Kind == TokenKind.Semicolon;
        }

        return false;
    }

    internal static IEnumerable<Token> Tokens(string sql)
    {
        var i = 0;
        while (i < sql.Length)
        {
            var c = sql[i];
            if (c == ';')
            {
                yield return new Token(TokenKind.Semicolon, ";");
                i++;
            }
            else if (c == '-' && Next(sql, i) == '-')
                i = AfterLineComment(sql, i);
            else if (c == '/' && Next(sql, i) == '*')
                i = AfterBlockComment(sql, i);
            else if (c == '\'')
                i = AfterQuoted(sql, i, '\'', backslashEscapes: false);
            else if (c == '"')
            {
                var end = AfterQuoted(sql, i, '"', backslashEscapes: false);
                var name = sql[(i + 1)..Math.Max(i + 1, end - 1)].Replace("\"\"", "\"", StringComparison.Ordinal);
                yield return new Token(TokenKind.QuotedName, name);
                i = end;
            }
            else if (c == '$' && DollarTag(sql, i) is { } tag)
            {
                var end = AfterDollarQuoted(sql, i, tag);
                yield return new Token(TokenKind.DollarQuoted, sql[i..end]);
                i = end;
            }
            else if (char.IsLetterOrDigit(c) || c == '_')
            {
                var start = i;
                while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] is '_' or '$'))
                    i++;

                var word = sql[start..i];
                if (word is "E" or "e" && i < sql.Length && sql[i] == '\'')
                    i = AfterQuoted(sql, i, '\'', backslashEscapes: true);
                else if (!char.IsAsciiDigit(word[0]))
                    yield return new Token(TokenKind.Word, word.ToLowerInvariant());
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
