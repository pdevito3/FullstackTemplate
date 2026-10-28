namespace FullstackTemplate.Server.Resources;

/// <summary>
/// Marks a command that <see cref="RaceRetryBehavior{TRequest,TResponse}"/> runs again when another save changed its
/// aggregate first. The command sends no version, so the caller decided nothing on rows that changed, and the server
/// can decide it again on the new rows. See <c>docs/backend/concurrency.md</c>.
/// <list type="bullet">
/// <item>The request has no <c>Version</c>, also in a DTO in it. On a command with a version, a stale version is the
/// answer that the caller must get.</item>
/// <item>Send the command only from an entrypoint, not from a handler. A run again discards each tracked change of the
/// DI scope, also the changes of the handler that sent it.</item>
/// </list>
/// Tests enforce both rules.
/// </summary>
public interface IRunsAgainWhenRaced;
