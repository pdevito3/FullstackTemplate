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
