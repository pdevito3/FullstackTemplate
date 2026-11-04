# Backend code style

These rules apply to the C# code of the solution and of `scripts/`. The build enforces them.

## Strings

Write a raw string literal (`"""`) when an escape hides the text of a string. The build refuses these strings (FSTSTR001):

- A regular string or a regular interpolated string with `\"`. Write `"""say "hi" now"""`, not `"say \"hi\" now"`.
- A regular string or a regular interpolated string with `\n` before its end. One `\n` at the end is allowed, for example in a line that a writer appends: `"done\n"`.
- A verbatim string (`@"..."` or `$@"..."`) that spans lines. A verbatim string on one line, such as `@"C:\temp"`, is allowed.

Put a text with a new line on the lines between the two delimiters. The compiler removes the indentation of the closing `"""` from each line. A text that starts or ends with a quote also goes on its own lines, because a raw string on one line cannot start or end with a quote. If the text has more quotes in a sequence than the delimiter, add a quote to each delimiter. If an interpolated text has a `{` or a `}`, start it with two or more `$`, for example `$$"""{"id": "{{id}}"}"""`.

The rule also applies to a UTF-8 string (`"..."u8`), and to a string in an attribute, in a `const` field, and in a `switch` pattern. A raw string is a constant, so it is correct in these places. A `\\` or a `\'` in the string does not stop the rule, because a raw string shows them as `\` and `'`.

The build does not refuse these strings:

- A string in generated code. The analyzer does not read generated code. `.editorconfig` marks the EF migrations under `Migrations/` as generated.
- A string with an escape that a raw string cannot show as text, such as `\r`, `\t`, `\0`, or `\u0041`.
- A text of only quotes, such as `"\""`. Its raw form needs three lines.
- A text where the raw form puts white space at the end of a line, such as `"paths:\n  "`. You cannot see that white space, and editors remove it.
- A string in a raw string, such as the C# source of an analyzer test. It is text of the raw string, not a string of the code.

`Directory.Build.props` gives the analyzer to each C# project. The other rules of `FullstackTemplate.SourceGenerators` check only the Server, so `ServerRulesOff.globalconfig` turns them off in each other project. If you add a Server rule, add its ID to that file. `ServerRulesOffTests` fails when an ID is not in it. A global config cannot turn off a generator, so `ValueObjectGenerator` runs in each project. It writes code only for a class that derives from `ValueObject`.

The build does not run the analyzer on the file-based scripts of `scripts/` or on `FullstackTemplate.SourceGenerators`, so `ScriptRawStringTests` runs the rule on them. `scripts/new-feature.cs` writes a text with a quote as a raw string literal, so the code that it writes obeys the rule.
