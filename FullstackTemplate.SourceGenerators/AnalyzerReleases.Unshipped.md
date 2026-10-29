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
