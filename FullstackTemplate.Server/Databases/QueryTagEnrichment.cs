namespace FullstackTemplate.Server.Databases;

using System.Diagnostics;

/// <summary>
/// Promotes the EF query tags written by <see cref="QueryTagging"/> from SQL comments into span
/// identity and attributes.
/// </summary>
/// <remarks>
/// Npgsql traces every command, and its tracing callback sees the final command text, so this is the
/// last point where the comments are still available. Only tagged queries are renamed. An untagged
/// query keeps the Npgsql default name, so framework queries and SaveChanges batches are never
/// mislabeled as feature work.
/// </remarks>
internal static class QueryTagEnrichment
{
    /// <summary>
    /// Reads the query tags from <paramref name="commandText"/> and writes them onto
    /// <paramref name="activity"/>.
    /// </summary>
    public static void Enrich(Activity activity, string? commandText)
    {
        var metadata = QueryTagMetadata.Parse(commandText);
        if (metadata.OperationName is null)
            return;

        activity.DisplayName = $"SQL: {metadata.OperationName}";
        activity.SetTag("db.operation.name", metadata.OperationName);

        if (metadata.MemberName is not null)
            activity.SetTag("code.function", metadata.MemberName);

        if (metadata.FilePath is not null)
            activity.SetTag("code.file.path", metadata.FilePath);

        if (metadata.LineNumber is not null)
            activity.SetTag("code.line.number", metadata.LineNumber);

        if (metadata is { FilePath: not null, LineNumber: not null })
            activity.SetTag("app.db.call_site", $"{metadata.FilePath}:{metadata.LineNumber}");
    }

    /// <summary>
    /// Operation metadata carried by the EF query-tag comments.
    /// </summary>
    /// <remarks>
    /// The comment format is an integration boundary with EF. This app owns the
    /// <see cref="QueryTagging.QueryPrefix"/> line, and EF owns the call-site line from
    /// <c>TagWithCallSite</c>. The parser stays permissive so a small EF formatting change cannot break
    /// the span names.
    /// </remarks>
    private readonly record struct QueryTagMetadata(
        string? OperationName,
        string? MemberName,
        string? FilePath,
        int? LineNumber)
    {
        private const string CommentPrefix = "--";
        private const string MemberPrefix = "Member:";
        private const string FilePrefix = "File:";

        public static QueryTagMetadata Parse(string? commandText)
        {
            if (string.IsNullOrWhiteSpace(commandText))
                return default;

            string? operationName = null;
            string? suffix = null;
            string? memberName = null;
            string? filePath = null;
            int? lineNumber = null;

            using var reader = new StringReader(commandText);
            while (reader.ReadLine() is { } rawLine)
            {
                var line = rawLine.Trim();
                if (!line.StartsWith(CommentPrefix, StringComparison.Ordinal))
                    continue;

                var comment = line[CommentPrefix.Length..].Trim();
                if (comment.StartsWith(QueryTagging.QueryPrefix, StringComparison.Ordinal))
                {
                    (operationName, memberName) = ParseQueryComment(comment);
                    continue;
                }

                if (comment.StartsWith(QueryTagging.SuffixPrefix, StringComparison.Ordinal))
                {
                    suffix = NormalizeTagValue(comment[QueryTagging.SuffixPrefix.Length..]);
                    continue;
                }

                (filePath, lineNumber) = ParseCallSiteComment(comment, filePath, lineNumber);
            }

            if (operationName is not null && suffix is not null)
                operationName = $"{operationName}.{suffix}";

            return new QueryTagMetadata(operationName, memberName, filePath, lineNumber);
        }

        /// <summary>
        /// Parses the comment that <see cref="QueryTagging.TagWithOperationCallSite{T}"/> writes.
        /// </summary>
        private static (string? OperationName, string? MemberName) ParseQueryComment(string comment)
        {
            var body = comment[QueryTagging.QueryPrefix.Length..].Trim();
            var semicolonIndex = body.IndexOf(';', StringComparison.Ordinal);
            if (semicolonIndex < 0)
                return (NormalizeTagValue(body), null);

            var operationName = NormalizeTagValue(body[..semicolonIndex]);
            var memberSection = body[(semicolonIndex + 1)..].Trim();
            var memberName = memberSection.StartsWith(MemberPrefix, StringComparison.Ordinal)
                ? NormalizeTagValue(memberSection[MemberPrefix.Length..])
                : null;

            return (operationName, memberName);
        }

        /// <summary>
        /// Parses an EF call-site comment. Both the <c>File: path:line</c> and the bare
        /// <c>path:line</c> shapes are accepted.
        /// </summary>
        private static (string? FilePath, int? LineNumber) ParseCallSiteComment(
            string comment,
            string? existingFilePath,
            int? existingLineNumber)
        {
            var callSite = comment.StartsWith(FilePrefix, StringComparison.Ordinal)
                ? comment[FilePrefix.Length..].Trim()
                : comment;

            var separatorIndex = callSite.LastIndexOf(':');
            if (separatorIndex <= 0
                || separatorIndex == callSite.Length - 1
                || !int.TryParse(callSite[(separatorIndex + 1)..], out var parsedLine))
            {
                return (existingFilePath, existingLineNumber);
            }

            var parsedFilePath = NormalizeTagValue(callSite[..separatorIndex]);
            return (parsedFilePath ?? existingFilePath, parsedLine);
        }

        private static string? NormalizeTagValue(string value)
        {
            var trimmed = value.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }
    }
}
