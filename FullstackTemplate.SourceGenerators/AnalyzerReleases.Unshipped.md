# Release tracking

## Unshipped

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
FSTID001 | EntityId | Error | An entity has no public const string IdPrefix.
FSTID002 | EntityId | Error | An entity IdPrefix is not 1 to 17 lower-case ASCII letters.
FSTID003 | EntityId | Error | Two or more entities use the same IdPrefix.
FSTID004 | EntityId | Error | An entity constructor does not pass the IdPrefix of its own type to base(...).
FSTID005 | EntityId | Error | An entity derives from a concrete entity, or from an abstract base class that has an IdPrefix.
FSTMSG001 | UserMessage | Error | A user message shows a code name: a snake_case or PascalCase word, an enum value, the stored name of a fixed-names value object, or a type name.
FSTMSG002 | UserMessage | Error | A user message shows an ID.
FSTTEL001 | Telemetry | Error | An EF query on a DbSet of AppDbContext has no TagWithOperationCallSite.
FSTTEL003 | Telemetry | Error | A caller of a method that returns an untagged query does not tag it.
FSTQRY001 | QueryInput | Error | A query lambda in a request handler compares a text or a list of texts of the request that EntityId.IsValid or PostgresText.CanStore did not check first.
FSTTEL002 | Telemetry | Error | A request handler calls SaveChangesAsync or SaveChanges with no _logger.Here().Information log or no Activity.Current?.SetTag("app.*_id") tag.
FSTTEL004 | Telemetry | Error | A constant span, event, or query name, tag key, metric name, or log property name breaks the Names table of docs/backend/feature-telemetry.md.
FSTSTR001 | Style | Error | A regular string has \" or a \n before its end, or a verbatim string spans lines. Write a raw string literal.
FSTSAF001 | Safety | Error | A reference downcast or an unboxing in app code has no // SAFETY: comment.
FSTSAF002 | Safety | Error | A null-forgiving operator in app code, other than null! and default!, has no // SAFETY: comment.
FSTSAF003 | Safety | Error | A #pragma warning disable has no // SAFETY: comment.
FSTSAF004 | Safety | Error | A SuppressMessage has no Justification.
FSTTYP001 | Types | Error | A using alias names object.
FSTTYP002 | Types | Error | A cast or an as of a cast or an as.
FSTTYP003 | Types | Error | A local widens a value to object, and the method casts or tests it back.
FSTTYP004 | Types | Error | A parameter or a return of app code is object, dynamic, or a Task of object.
FSTTYP005 | Types | Error | A signature of a Domain namespace holds a dictionary of object, an ExpandoObject, or a JSON DOM type.
FSTLNQ001 | Linq | Error | A ToList, ToArray, or ToHashSet is the source of another Enumerable call.
FSTLNQ002 | Linq | Error | The lambda of an Aggregate copies its accumulator.
FSTLNQ003 | Linq | Error | A loop appends to a string with +=.
FSTBRN001 | Branches | Error | A catch of Exception tests the type of the exception in an if and throws the rest again.
FSTBRN002 | Branches | Error | A chain of two or more ternaries tests one value.
FSTNAM001 | Naming | Error | A type, method, or property name ends in Helper, Util, Utility, Info, Data, or Shape.
FSTDI001 | Services | Error | App code news a type that a registration asks the container to build.
FSTMCK001 | Tests | Error | Substitute.For or ForPartsOf of a concrete class or a DbContext.
