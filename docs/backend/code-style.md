# Backend code style

These rules apply to the C# code of the solution. The build enforces them. The rule of strings also applies to `scripts/`.

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

`Directory.Build.props` gives the analyzer to each C# project. A rule says which projects it checks with a custom tag of `RuleScope`. A rule with no tag checks only the Server, so `ServerRulesOff.globalconfig` turns it off in each other project. A rule tagged `AppCode` checks the Server, the BFF, and the AppHost, so `TestRulesOff.globalconfig` turns it off under `tests/`. A rule tagged `AllCode`, such as FSTSTR001, checks each project. If you add a rule, add its ID to the file of its scope. `ServerRulesOffTests` fails when an ID is not in it. A global config cannot turn off a generator, so `ValueObjectGenerator` runs in each project. It writes code only for a class that derives from `ValueObject`.

The build does not run the analyzer on the file-based scripts of `scripts/` or on `FullstackTemplate.SourceGenerators`, so `ScriptRawStringTests` runs the rule on them. `scripts/new-feature.cs` writes a text with a quote as a raw string literal, so the code that it writes obeys the rule.

`scripts/raw-strings.cs` rewrites each string that the rule refuses, and makes sure that each string keeps its value. Use it when you merge code that has many of these strings. See [Take updates from the template](../template-updates.md#the-update-that-adds-fststr001).

## Claims the compiler cannot check

A downcast, a `!`, a `#pragma warning disable`, and a `[SuppressMessage]` each claim a fact that the compiler cannot check. Write the reason, so that the reader can check it and the next change can see when it stops being true.

- A reference downcast or an unboxing, such as `(string)value` or `(int)boxed`, needs a `// SAFETY:` comment (FSTSAF001). A numeric conversion, such as `(int)3.5`, does not.
- A `!`, such as `options.ClientId!`, needs a `// SAFETY:` comment (FSTSAF002). `null!` and `default!` do not, because they give a property or a field the value that the constructor or EF sets.
- A `#pragma warning disable` needs a `// SAFETY:` comment (FSTSAF003).
- A `[SuppressMessage]` needs a `Justification` (FSTSAF004).

Put the comment on the line above the statement or the member, or at the end of the line. One comment covers each claim in its statement. A comment above an `if` with no braces covers the statement under it. The text after `SAFETY:` says why the claim holds, for example `// SAFETY: CheckClaims refuses a logout token with no jti.`

FSTSAF001 and FSTSAF002 check the app code, not the tests. A test asserts with a cast or a `!`, and a wrong claim fails the test.

A type that the code loses and takes back is also a claim:

- A using alias of `object`, such as `using Payload = object;` (FSTTYP001).
- A cast of a cast that widens a value to a reference type and casts it back down, such as `(User)(object)value` or `value as object as User` (FSTTYP002). A numeric cast of a numeric cast, such as `(int)(uint)value`, and `(T)(object)value` for a type parameter are allowed.
- A local that holds a known type as `object`, which the method casts back or tests for a type (FSTTYP003). A null test, such as `o is null` or `o is { } value`, is allowed.

## Signatures

A parameter or a return of app code is not `object`, `dynamic`, `Task<object>`, or `ValueTask<object>` (FSTTYP004). Each caller and the method must test the type of such a value. Use the type that it holds. An override or an implementation of an interface member, such as `Equals(object?)`, takes the signature of its base. At a boundary that gets values of any type, such as an EF property value, an ADO.NET parameter, or the result of an endpoint filter, suppress FSTTYP004 with the boundary as the reason. FSTTYP004 checks the app code, not the tests.

A signature of a `Domain` namespace does not hold untyped values (FSTTYP005): a `Dictionary`, `IDictionary`, or `IReadOnlyDictionary` of string to object, an `ExpandoObject`, or a JSON DOM type (`JsonElement`, `JsonDocument`, `JsonNode`, `JsonObject`, `JObject`). The rule reads parameters, returns, properties, and fields, also inside a generic or an array. Parse the values into a record at the boundary and pass the record to the domain.

## Collections

- A `ToList()`, `ToArray()`, or `ToHashSet()` that another LINQ call reads copies the elements for one read (FSTLNQ001). Put the copy at the end of the chain. Call `AsEnumerable()` to switch from an EF query to memory.
- An `Aggregate` whose lambda copies its accumulator, such as `acc.Append(x).ToList()` or `acc + x` on a string, takes time in the square of the count (FSTLNQ002).
- A `+=` on a string that a loop keeps across its passes (FSTLNQ003). Use a `StringBuilder` or `string.Join`. A string local of the loop body starts again on each pass, so the rule allows it.
- CA1851 refuses a second enumeration of an `IEnumerable`, which can run the query or the iterator again.

## Branches

- A `catch (Exception e)` with an `if` that tests the type of `e` and a `throw;` for the rest (FSTBRN001). Catch the type, or add a `when` filter. A catch that handles each exception and maps one type, such as the catch of `AppDbContext.SaveChangesAsync`, is allowed. So is a catch whose `if` does not leave it, followed by a `throw;` that rethrows each exception.
- A chain of two or more ternaries that compare one value to a constant or match it with a pattern (FSTBRN002). Write a switch expression. IDE0066 covers a switch statement that a switch expression can replace.

## Names

A type, a method, or a property name does not end in `Helper`, `Util`, `Utility`, `Info`, `Data`, or `Shape`, or in their plurals (FSTNAM001). These words say nothing about what the type holds or does. `Manager` is a word of the domain (a tenant manager), so the rule allows it. An override or an implementation of an interface member takes the name of its base.

## Services

App code does not `new` a type that a registration asks the container to build (FSTDI001): a type argument of `AddScoped`, `AddSingleton`, `AddTransient`, `AddHostedService`, or `AddDbContext` (or a `TryAdd` or keyed form) with no factory and no instance, or a `typeof` argument of one. A new copy skips the lifetime, the decorators, and the options of the registration. Inject it. A registration with a factory or an instance builds the value in code, so the rule allows a `new` of that type. A design-time DbContext factory builds its context. The rule does not see a registration by a Scrutor or a MediatR scan.

A decorator that must not take a service of the container, because the service takes the decorator, suppresses FSTDI001 with the reason. See `RefreshIdTokenValidation`.

## Tests

A test does not substitute a concrete class or a DbContext (FSTMCK001). A substitute of a class runs the real code of each member that is not virtual. Substitute an interface or an abstract class, or use the real type. A DbContext needs the real database.

## Review rules

The build cannot enforce these rules, because the bad form and the allowed form use the same syntax. A review checks them. Each rule names the allowed form, so that a fix does not break a real boundary.

### A type test belongs in a parse at a boundary

Test the runtime type of a value only where the value comes in as `object` or as a base type that you do not own, and turn it into a known type there. Do not branch on the type of a value that the code already types.

Allowed. The value comes from a framework as `object`, and the code reads it one time:

- `PaginationHeaderFilter.AddHeader` gets the result of any endpoint. It unwraps `INestedHttpResult` and matches `IPagedList` or `IValueHttpResult { Value: IPagedList }`.
- `AuditCapture.TrackedOwner` gets the value of a foreign key from EF. It tests `ownerId is string id` before it looks up the owner.

Refused. A method of the domain takes a base type and branches on what it is, such as `LabelOf(BaseEntity entity)` with a switch of `Tenant tenant => tenant.Name`, `User user => user.Email.Value`, and `_ => entity.Id`. Give each type the behavior (a member of `Tenant` and of `User`), or take the type that the caller already has. A new entity type then fails the build, not the default arm.

### A fixed set of keys is a record

When the code knows each key at build time, use a record or a class with a property for each key. A `Dictionary<string, T>` is for keys that come from data, or for a dictionary that an API asks for.

Allowed:

- `BindingProblem.ValueKinds` maps the name of a CLR type in a binding error to its `ValueKind`. The key comes from the text of the error, so the dictionary is a lookup table.
- `HostExtensions.ResourceAttributes` builds the resource attributes of OpenTelemetry. `AddAttributes` takes a dictionary, and the environment detector adds keys that the code does not know.
- The claims of a JWT in a test fake. The claims are the format of the token.

Refused. Two methods of the app pass a known set of values through a dictionary, such as a `Dictionary<string, object>` with the keys `email`, `role`, and `expiresAt` that a handler passes to `SendInviteAsync`. A typo in a key, a missing key, and a value of the wrong type each fail at run time. Write `record InviteMessage(Email Email, Role Role, DateTimeOffset ExpiresAt)`, so that the compiler checks each of them.
