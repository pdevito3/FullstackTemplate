# Messages

These rules apply to each text that a user reads: the `detail` and `errors` of a problem, an alert, a toast, a confirm dialog, and a label. A message from the Server and a message from the SPA follow the same rules. Each rule names its check, or says "No check" when a reviewer must read the text.

## Rules

1. **Name each thing as the screen names it.** Use the display name of a state, a role, a permission, or a place. Do not show an enum name (`CheckedIn`), a permission code (`manage_users`), a type name (`AuditEntry`), an ID (`ast_01h...`), or a time zone ID (`Asia/Bangkok`). Give the value object or the entity a property with the words, as `Permission.Words` gives "manage users". Write the words out. Words made from the code, such as `Value.Replace("_", " ")`, fail the check. Check: `UserMessageAnalyzer` (below) catches a code name, an enum or stored name, a type name, and an ID. It does not catch an IANA ID in a plain string. A field whose input is a code, such as a time zone field that takes an IANA ID, can show the value that the user typed, as `TimeZoneId` does.
2. **Say who did it only when the Server knows it.** Do not say "another user" when the same user can have made the change, for example in a second tab. Do not tell the loser of a race on a create that "the record changed", because it did not load a record. No check.
3. **Say what happened, then what the user can do next.** For example "This record changed after you loaded it. Reload it, then make your change again." A message that only says that the save failed is not enough. No check.
4. **Give one rule one message.** Put the message in one constant, and use the constant in each path that can break the rule. For example, the handler check and the unique index of a tenant name both use `Tenant.NameInUseMessage` (see [Error responses](backend.md#error-responses)). Check: `ConstraintErrorsTests` fails when a unique index has no request field and message. No check makes the handler check and the index use the same constant.
5. **Show a time or a number in the format of the field next to it.** If the form shows 12-hour times, the message shows 12-hour times. If the field shows a unit, the message shows the same unit. No check.
6. **Do not repeat the title in the text.** The alert shows the `title` of the problem above the `detail`, so the `detail` adds a fact. No check.
7. **Write correct grammar and correct numbers.** Write "an odometer", not "a Odometer". A limit in a message is the limit that the code enforces: take it from the same constant. No check.

A 400 for a request that does not bind names the JSON field, for example "The firstName field is required." (see [dtos-and-mappings.md](backend/dtos-and-mappings.md#dtos)). That message is for the client code, not for a person, so rule 1 does not apply to the field name. Show a 400 to a user only as a fault of the SPA.

## The check

`UserMessageAnalyzer` in `FullstackTemplate.SourceGenerators` runs in each build of the Server. It finds each string that goes to a user message:

- The message of an exception class of the Server: the string that its constructor gives to the base exception, whatever the name of its parameter. This covers the domain exceptions and `ValidationException.Must`. A message for a developer uses an exception of .NET, such as `InvalidOperationException`.
- The message of an `McpException`. The MCP SDK sends it to the client.
- The text of a `CallToolResult` with `IsError = true`, when the initializer of the result sets the text.
- The `detail` and `title` of `TypedResults.Problem` and `TypedResults.ValidationProblem`, the string of `TypedResults.BadRequest`, `Conflict`, `NotFound`, `UnprocessableEntity`, and `InternalServerError`, the `content` of `TypedResults.Text` and `TypedResults.Content` when the `statusCode` is a constant of 400 or more, and the same methods of `Results`. Also the `Detail` and `Title` of a `ProblemDetails`.
- A parameter named `message` or `*Message` of a method of the Server, for example `ForRequestField`.
- A FluentValidation `WithMessage`, with a string or with a lambda, and the `errorMessage` of a `ValidationFailure` or of `AddFailure`.
- The `detail` of a gRPC `Status`.
- A string field or property whose name ends in `Message`, for example `Tenant.NameInUseMessage`.

It follows the string back through the methods, properties (also an auto-property with an initializer), fields, locals, and parameters of the Server. A value that the code assigns to a parameter with `=` or `??=`, such as `reason = $"...";`, is followed too. So it also checks a message that a helper builds, such as the denials of `Caller`. A value that the message shows (as the whole message, in an interpolation, in a concatenation, or through a string method such as `string.Format`) is checked where it comes from too. A helper that the message calls, such as a quote helper, is checked with the arguments of that call only. A number, such as `(int)mode`, is not a code. A word in braces, such as `{PropertyName}`, is a placeholder that FluentValidation replaces, so it is not a code. Do not use `{PropertyValue}` for an enum or a SmartEnum: it shows the code.

A message after a parse that fails can show the value that the parse did not accept, as `Invalid permission: {value}` after `PermissionEnum.TryFromName(value, ...)` does. A parse is `TryFromName`, `TryFromValue`, `TryParse`, or `IsDefined` of a SmartEnum, of an `IFixedNames` value object, or of `Enum`, such as `Enum.TryParse`. Another `Try*` method, such as `TryMoveTo`, is not a parse. The parse must be the whole condition, and the throw must be on the branch where it fails. These forms are accepted:

- `Try(value) ? x : throw ...`
- `if (!Try(value)) throw ...`
- `if (Try(value, out var parsed) is false) throw ...`
- `if (Try(value)) return ...;` as the statement just before the throw, in the same block. In a `case` of a `switch`, put the two statements in braces.

The value is the text that the user sent, so the analyzer does not follow it to the callers. A caller that gives a name that the parse accepts, such as `Permission.ManageUsers()`, never gets to the message. The analyzer does not check that the parse is of the type whose name the caller gives. So a stored name of another type, or a name given to `TryFromValue`, is not caught. The text of the message and the other values that it shows are checked.

Then it fails the build:

| Rule | Fails when |
|------|------------|
| `FSTMSG001` | The text has a snake_case word or a PascalCase word with two or more parts. Or the message shows an enum value, the stored name of a SmartEnum or an `IFixedNames` value object (its `Value`, its `Name`, or its `ToString`), a list of enum values or stored names (such as `Enum.GetValues<T>()`, `Enum.GetNames<T>()`, `ListNames()` of an `IFixedNames` type, or `KindEnum.List.Select(x => x.Name)`), a type name (`Type.Name` or `Type.FullName`), or `nameof`. |
| `FSTMSG002` | The message shows an ID: a member, a local, or a parameter whose name is `Id` or ends in `Id`, such as `{defectId}` or `{agent.ClientId}`, or a value of a type whose name ends in `Id`, such as a `TimeZoneId` value. |

The error says what to write instead. `UserMessageAnalyzerTests` has a failing case for each kind, among them the permission code of CarePoint 112, the status name of CarePoint 32, and the raw ID of Fleet 76. A message for a developer, such as an `InvalidOperationException` about the model, is not a user message, so it can name types and IDs.

A brand name with two capitals, such as ZeroFactor or GitHub, matches the PascalCase rule. If the analyzer is wrong about one message, suppress it with `#pragma warning disable FSTMSG001` on that line, and write the reason in a comment.

These paths are not checked:

- A text that a lambda or a delegate builds, other than a `WithMessage` function and the `Select` of a list. For example, `items.Aggregate((a, b) => a + b)`.
- A text that a `StringBuilder` builds.
- A value that `+=` adds to a local or a parameter.
- The `content` of `Results.Text` or `Results.Content` when the `statusCode` is not a constant.
- A value that comes from a positional record property, such as `DomainError.Detail`. The analyzer does not follow it to the place that makes the record.
- The text of a tool result that is set outside the initializer of the `CallToolResult`, or a result whose `IsError` is not the constant `true`.
- The message of an exception of a package that the Server creates with `new`, other than `McpException`. `DomainError` maps some of them, such as the `ValidationException` of FluentValidation, but their text comes from a `ValidationFailure`, which is checked.
- The text in an errors dictionary, such as the errors of a `RequestValidationException` or of `TypedResults.ValidationProblem`.
- A `WithMessage` that takes a method group, such as `.WithMessage(Describe)`.

The text of an MCP tool error starts with a code such as `permission_denied`, so that a model can match on it. `McpDomainErrors.CodeFor` suppresses `FSTMSG001` for these codes.

## In the SPA

`apiClient` shows the `detail` of a problem in a toast (see [API client](frontend.md#api-client)). A component that also shows the error must not repeat the same text. When the SPA writes its own text, such as a toast, a label, or an empty state, these rules also apply. The SPA has no check for them. The `verify-fullstack-template` skill reads each message on the screen.
