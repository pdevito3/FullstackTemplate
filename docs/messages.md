# Messages

These rules apply to each text that a user reads: the `detail` and `errors` of a problem, an alert, a toast, a confirm dialog, and a label. A message from the Server and a message from the SPA follow the same rules. Each rule names its check, or says "No check" when a reviewer must read the text. A check catches the common forms only. The rules apply to each message, also to a message that no check sees, so a reviewer reads the text of each new message.

## Rules

1. **Name each thing as the screen names it.** Use the display name of a state, a role, a permission, or a place. Do not show an enum name (`CheckedIn`), a permission code (`manage_users`), a type name (`AuditEntry`), an ID (`ast_01h...`), or a time zone ID (`Asia/Bangkok`). Give the value object or the entity a property with the words, as `Permission.Words` gives "manage users". Write the words out. Words made from the code, such as `Value.Replace("_", " ")`, fail the check when the message shows them directly. In a `Select` lambda, the check does not catch them. Check: `UserMessageAnalyzer` (below) catches the common forms of a code name, an enum or stored name, a type name, and an ID. It is not complete. It does not catch an IANA ID in a plain string. In the SPA, the lint fails on words made with `replace('_', ' ')`, `replaceAll('_', ' ')`, or `split('_').join(' ')`, and `timeZoneLabel(zone)` names a zone. A field whose input is a code, such as a time zone field that takes an IANA ID, can show the value that the user typed, as `TimeZoneId` does.
2. **Say who did it only when the Server knows it.** Do not say "another user" when the same user can have made the change, for example in a second tab. Do not tell the loser of a race on a create that "the record changed", because it did not load a record. No check.
3. **Say what happened, then what the user can do next.** For example "This record changed after you loaded it. Reload it, then make your change again." A message that only says that the save failed is not enough. No check.
4. **Give one rule one message.** Put the message in one constant, and use the constant in each path that can break the rule. For example, the handler check and the unique index of a tenant name both use `Tenant.NameInUseMessage` (see [Error responses](backend.md#error-responses)). Check: `ConstraintErrorsTests` fails when a unique index has no request field and message. No check makes the handler check and the index use the same constant.
5. **Show a time or a number in the format of the field next to it.** If the form shows 12-hour times, the message shows 12-hour times. If the field shows a unit, the message shows the same unit. No check.
6. **Do not repeat the title in the text.** The alert shows the `title` of the problem above the `detail`, so the `detail` adds a fact. No check.
7. **Write correct grammar and correct numbers.** Write "an odometer", not "a Odometer". A limit in a message is the limit that the code enforces: take it from the same constant. No check.

A 400 for a request that does not bind names the JSON field, for example "The firstName field is required." (see [dtos-and-mappings.md](backend/dtos-and-mappings.md#dtos)). That message is for the client code, not for a person, so rule 1 does not apply to the field name. Show a 400 to a user only as a fault of the SPA.

## The check

`UserMessageAnalyzer` in `FullstackTemplate.SourceGenerators` runs in each build of the Server. It reads the code of the Server and catches the common forms of a code name or an ID in a message. It is not complete. It cannot follow each path by which a value gets into a text, so a message that passes the build can still show a code. The rules apply to each message, whether the analyzer sees it or not.

It finds each string that goes to a user message:

- The message of an exception class of the Server: the string that its constructor gives to the base exception, whatever the name of its parameter. This covers the domain exceptions and `ValidationException.Must`. A message for a developer uses an exception of .NET, such as `InvalidOperationException`.
- The message of an `McpException`. The MCP SDK sends it to the client.
- The text of a `CallToolResult` with `IsError = true`, when the initializer of the result sets the text.
- The `detail` and `title` of `TypedResults.Problem` and `TypedResults.ValidationProblem`, the string of `TypedResults.BadRequest`, `Conflict`, `NotFound`, `UnprocessableEntity`, and `InternalServerError`, the `content` of `TypedResults.Text` and `TypedResults.Content` when the `statusCode` is a constant of 400 or more, and the same methods of `Results`. Also the `Detail` and `Title` of a `ProblemDetails`.
- A parameter named `message` or `*Message` of a method of the Server, for example `ForRequestField`.
- A FluentValidation `WithMessage`, with a string or with a lambda, and the `errorMessage` of a `ValidationFailure` or of `AddFailure`.
- The `detail` of a gRPC `Status`.
- A string field or property whose name ends in `Message`, for example `Tenant.NameInUseMessage`.

It follows the string back, in the forms that it knows, through the methods, properties (also an auto-property with an initializer), fields, locals, and parameters of the Server. A value that the code assigns to a parameter with `=` or `??=`, such as `reason = $"...";`, is followed too. So it also checks a message that a helper builds, such as the denials of `Caller`. A value that the message shows (as the whole message, in an interpolation, in a concatenation, or through a string method such as `string.Format`) is checked where it comes from too. A helper that the message calls, such as a quote helper, is checked with the arguments of that call only. A number, such as `(int)mode`, is not a code. A word in braces, such as `{PropertyName}`, is a placeholder that FluentValidation replaces, so it is not a code. Do not use `{PropertyValue}` for an enum or a SmartEnum: it shows the code.

A message after a parse that fails can show the value that the parse did not accept, as `Invalid permission: {value}` after `PermissionEnum.TryFromName(value, ...)` does. A parse is `TryFromName`, `TryFromValue`, `TryParse`, or `IsDefined` of a SmartEnum, of an `IFixedNames` value object, or of `Enum`, such as `Enum.TryParse`. Another `Try*` method, such as `TryMoveTo`, is not a parse. The parse must be the whole condition, and the throw must be on the branch where it fails. These forms are accepted:

- `Try(value) ? x : throw ...`
- `if (!Try(value)) throw ...`
- `if (Try(value, out var parsed) is false) throw ...`
- `if (Try(value)) return ...;` as the statement just before the throw, in the same block. In a `case` of a `switch`, put the two statements in braces.

The value is the text that the user sent, so the analyzer does not follow it to the callers. A caller that gives a name that the parse accepts, such as `Permission.ManageUsers()`, never gets to the message. The analyzer does not check that the parse is of the type whose name the caller gives. So a stored name of another type, or a name given to `TryFromValue`, is not caught. The text of the message and the other values that it shows are checked.

Then it fails the build:

| Rule | Fails when |
|------|------------|
| `FSTMSG001` | The text has a snake_case word or a PascalCase word with two or more parts. Or the message shows an enum value, the stored name of a SmartEnum or an `IFixedNames` value object (its `Value`, its `Name`, or its `ToString`), a list of enum values or stored names (such as `Enum.GetValues<T>()`, `Enum.GetNames<T>()`, `ListNames()` of an `IFixedNames` type, or `KindEnum.List.Select(x => x.Name)`), a type name (`Type.Name` or `Type.FullName`), or `nameof`. An enum of .NET in a namespace that starts with `System`, whose names are each one capital letter and then lower-case letters, such as `DayOfWeek`, is not a code: "the Monday block" reads as the screen names it. This test looks only at the shape of the names, so it also accepts names such as `DateTimeKind.Utc`. |
| `FSTMSG002` | The message shows an ID: a member, a local, or a parameter whose name is `Id` or ends in `Id`, such as `{defectId}` or `{agent.ClientId}`, or a value of a type whose name ends in `Id`, such as a `TimeZoneId` value. |

The error says what to write instead. `UserMessageAnalyzerTests` has a failing case for each kind, among them the permission code of CarePoint 112, the status name of CarePoint 32, and the raw ID of Fleet 76. A message for a developer, such as an `InvalidOperationException` about the model, is not a user message, so it can name types and IDs.

A brand name with two capitals, such as ZeroFactor or GitHub, matches the PascalCase rule. If the analyzer is wrong about one message, suppress it with `#pragma warning disable FSTMSG001` on that line, and write the reason in a comment.

These forms are not checked. The list names the forms that are known. It is not complete.

- A text that a lambda or a delegate builds, other than a `WithMessage` function. For example, `items.Aggregate((a, b) => a + b)`.
- In the `Select` of a list, the check catches only an enum value or a stored name that the lambda returns, alone or in a text, such as `p => $"'{p.Value}'"`. It does not catch a code word in the text of the lambda, an ID, or a name that a method changes, such as `p => p.Value.Replace("_", " ")` or `k => k.ToString().ToLowerInvariant()`. A `Select` over a list of stored names, such as `ListNames().Select(n => n.ToLowerInvariant())`, is caught.
- A text that a `StringBuilder` builds.
- A value that `+=` adds to a local or a parameter.
- The `content` of `Results.Text` or `Results.Content` when the `statusCode` is not a constant.
- A value that comes from a positional record property, such as `DomainError.Detail`. The analyzer does not follow it to the place that makes the record.
- The text of a tool result that is set outside the initializer of the `CallToolResult`, or a result whose `IsError` is not the constant `true`.
- The message of an exception of a package that the Server creates with `new`, other than `McpException`. `DomainError` maps some of them, such as the `ValidationException` of FluentValidation, but their text comes from a `ValidationFailure`, which is checked.
- The text in an errors dictionary, such as the errors of a `RequestValidationException` or of `TypedResults.ValidationProblem`.
- A `WithMessage` that takes a method group, such as `.WithMessage(Describe)`.
- The `params` arguments of a string method, such as `string.Join(", ", nameof(Kind.Created), nameof(Kind.Deleted))` or `string.Join(", ", Mode.Delegated, Mode.Machine)`.
- An array or a collection expression of `nameof`, such as `[nameof(Kind.Created), nameof(Kind.Deleted)]`.
- A list that a spread or a constructor copies, such as `[.. Grade.ListNames()]` or `new List<string>(Grade.ListNames())`.
- A list that a `foreach` loop fills with `Add`, such as `foreach (var p in Permission.ListNames()) names.Add(p);`. A `ListNames()` method of an `IFixedNames` type is caught by its name, also when a loop builds it.

The text of an MCP tool error starts with a code such as `permission_denied`, so that a model can match on it. `McpDomainErrors.CodeFor` suppresses `FSTMSG001` for these codes.

## In the SPA

`failureText(error)` of `lib/request-failure.ts` gives the text of each failed request, query, and mutation. It is the one owner of this text. The toast of `apiClient`, `QueryError`, `useServerErrors`, the stale alert, and the forms and pages that show why a request failed show the text that it gives (see [API client](frontend.md#api-client)). The SPA knows only the kind of a failure, so a text that a component writes can name the wrong cause, such as "Check your connection" for a fault of the Server (#500). `failureText` gives one text for each kind:

| Kind | Text |
|------|------|
| A refusal: a 4xx problem, such as a 403, a 404, a 409, or a 422 | The `detail` of the Server, or its `title`. `UserMessageAnalyzer` checks this text in the Server. |
| A fault of the Server: a 5xx, or an answer that is not a problem | "The server could not complete the request. Try again in a moment." The `title` of a 5xx is the default of ASP.NET, and its `detail` is for a developer (#474). |
| A time limit: a 504 with no body, or the time limit of the SPA | "The server did not answer in time. Reload the page to see the latest data, then try again." |
| No answer | "The app could not reach the server. Check your connection, then try again." |
| A 401 | "The server did not accept your sign-in for this request. Try again in a moment. If it fails again, log out and log in again." |
| A fault of the app: an error that is not of a request, such as a bug | "The app has a fault. Reload the page, then try again." The `message` of the error is for a developer. |

`Record<SpaTextKind, string>` holds the texts of the SPA, so a new kind does not build until it has a text. A component writes only a title that names what did not happen, such as "The invites did not load.", and shows `failureText(error)` with it. A component that also shows the error must not repeat the same text.

Check: in `pnpm lint`, `messages/failure-text` in `frontend/eslint.config.js` fails outside `lib/request-failure.ts` on these forms. Its error names `failureText`.

- The `message` of a value of the type `Error`, or of a subclass, such as `error.message` or `const { message } = error`. The rule reads the type.
- A `message` that a check of the shape gives a value, such as `'message' in error` or `error as { message: string }`.
- The `detail` or the `title` of a problem, such as `error.problemDetails.detail` or `const { detail } = error.problemDetails`.
- A `new Error(...)` that the code does not throw or reject, such as `show(new Error('Check your connection.'))`.
- A root error of a form with a text of its own, such as `setError('root.server', { message: '...' })`.

The error of a query or a mutation has the type `unknown` (`Register` in `lib/request-failure.ts`), so the build fails on `query.error.message`. The lint does not see a text that a component writes in JSX for a failure, such as "The server is down." in an error branch. A reviewer reads it. When the SPA writes its own text, such as a toast, a label, or an empty state, the rules above also apply. The `verify-fullstack-template` skill reads each message on the screen.
