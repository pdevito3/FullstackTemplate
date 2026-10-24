namespace FullstackTemplate.UnitTests.Databases.Auditing;

using FullstackTemplate.Server.Domain.AuditEntries;

public enum ExportFormat
{
    Csv,
    Json,
}

[AuditActionName("users_exported")]
public sealed record UsersExported(AuditSubject Subject, ExportFormat Format, int Count) : AuditAction(Subject);

public sealed record ActionWithNoName(AuditSubject Subject) : AuditAction(Subject);
