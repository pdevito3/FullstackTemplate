namespace FullstackTemplate.SourceGenerators;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Refuses a string where an escape hides the text: a regular string with \" or with a \n before its end, and a
/// verbatim string that spans lines. A raw string literal shows the same text as you read it. See "Strings" in
/// docs/backend/code-style.md.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RawStringAnalyzer : DiagnosticAnalyzer
{
	internal static readonly DiagnosticDescriptor EscapeHidesText = new(
		id: "FSTSTR001",
		title: "An escape hides the text of a string",
		messageFormat: """"{0}. Write a raw string literal, for example """say "hi" now""" in place of "say \"hi\" now". Put a text with a new line, or a text that starts or ends with a quote, on the lines between the two delimiters. If an interpolated text has {{ or }}, start it with $$. See docs/backend/code-style.md."""",
		category: "Style",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	private const string EscapedQuote = """The string has \" in it""";
	private const string EscapedNewLine = """The string has \n before its end""";
	private const string VerbatimLines = "The verbatim string spans lines";

	private const string QuoteEscape = @"\""";
	private const string NewLineEscape = @"\n";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [EscapeHidesText];

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.StringLiteralExpression, SyntaxKind.Utf8StringLiteralExpression,
			SyntaxKind.InterpolatedStringExpression);
	}

	private static void Analyze(SyntaxNodeAnalysisContext ctx)
	{
		if (Problem(ctx.Node) is { } problem)
			ctx.ReportDiagnostic(Diagnostic.Create(EscapeHidesText, ctx.Node.GetLocation(), problem));
	}

	private static string? Problem(SyntaxNode node)
	{
		// An unterminated string has no end delimiter to read, and the compiler already refuses it
		if (node.ContainsDiagnostics)
			return null;

		if (node is LiteralExpressionSyntax { Token: var token })
		{
			if (!token.IsKind(SyntaxKind.StringLiteralToken) && !token.IsKind(SyntaxKind.Utf8StringLiteralToken))
				return null;
			var text = token.Text;
			if (text[0] == '@')
				return text.IndexOf('\n') >= 0 ? VerbatimLines : null;
			var end = token.IsKind(SyntaxKind.Utf8StringLiteralToken) ? text.Length - 3 : text.Length - 1;
			return Problem([new Segment(text.Substring(1, end - 1), token.ValueText)]);
		}

		var interpolated = (InterpolatedStringExpressionSyntax)node;
		var texts = interpolated.Contents.OfType<InterpolatedStringTextSyntax>().Select(t => t.TextToken);
		if (interpolated.StringStartToken.IsKind(SyntaxKind.InterpolatedVerbatimStringStartToken))
			return texts.Any(t => t.Text.IndexOf('\n') >= 0) ? VerbatimLines : null;
		if (!interpolated.StringStartToken.IsKind(SyntaxKind.InterpolatedStringStartToken))
			return null;
		return Problem([.. interpolated.Contents.Select(c => c is InterpolatedStringTextSyntax t
			? new Segment(t.TextToken.Text, t.TextToken.ValueText)
			: Segment.Hole)]);
	}

	/// <summary>
	/// The problem of a regular string, given as its text parts and its holes in order. Null when the string breaks no
	/// rule, or when a raw string cannot show its text better.
	/// </summary>
	private static string? Problem(IReadOnlyList<Segment> segments)
	{
		var escapes = segments.SelectMany(s => Escapes(s.Source)).ToList();
		// A raw string has no escapes, so it cannot show a \r, a \t, a \0, or a \u escape as text that you can read
		if (escapes.Count == 0 || escapes.Any(e => e is not (QuoteEscape or NewLineEscape or @"\\" or @"\'")))
			return null;

		var newLines = escapes.Count(e => e == NewLineEscape);
		var last = segments[segments.Count - 1];
		var endsInNewLine = !last.IsHole && Escapes(last.Source).LastOrDefault() == NewLineEscape && last.Source.EndsWith(NewLineEscape, StringComparison.Ordinal);
		var problem = escapes.Contains(QuoteEscape) ? EscapedQuote
			: newLines > 1 || (newLines == 1 && !endsInNewLine) ? EscapedNewLine
			: null;
		return problem is not null && RawFormShowsText(segments) ? problem : null;
	}

	/// <summary>
	/// A raw string that starts or ends with a quote, or that has a new line, needs the delimiters on their own lines.
	/// That form hides white space at the end of a line, which editors also trim, and it takes three lines for a text of
	/// only quotes, such as "\"".
	/// </summary>
	private static bool RawFormShowsText(IReadOnlyList<Segment> segments)
	{
		var value = new StringBuilder();
		foreach (var segment in segments)
			value.Append(segment.IsHole ? "x" : segment.Value);
		var text = value.ToString();
		var ownLines = text.IndexOf('\n') >= 0 || (text.Length > 0 && (text[0] == '"' || text[text.Length - 1] == '"'));
		if (!ownLines)
			return true;
		if (text.All(c => c == '"'))
			return false;
		return !text.Split('\n').Any(line => line.EndsWith(" ", StringComparison.Ordinal) || line.EndsWith("\t", StringComparison.Ordinal));
	}

	/// <summary>The escapes of the source text of a regular string, in order, such as \" and \u0041.</summary>
	private static IEnumerable<string> Escapes(string source)
	{
		for (var i = 0; i < source.Length - 1; i++)
		{
			if (source[i] != '\\')
				continue;
			var length = source[i + 1] switch
			{
				'u' => 6,
				'U' => 10,
				'x' => 2 + HexDigits(source, i + 2),
				_ => 2,
			};
			yield return source.Substring(i, Math.Min(length, source.Length - i));
			i += length - 1;
		}
	}

	private static int HexDigits(string source, int start)
	{
		var n = 0;
		while (n < 4 && start + n < source.Length && Uri.IsHexDigit(source[start + n]))
			n++;
		return n;
	}

	/// <param name="Source">The text of the part as the source writes it, with its escapes.</param>
	/// <param name="Value">The text of the part as the program reads it.</param>
	/// <param name="IsHole">True for a hole of an interpolated string.</param>
	private sealed record Segment(string Source, string Value, bool IsHole = false)
	{
		public static readonly Segment Hole = new("", "", IsHole: true);
	}
}
