namespace FullstackTemplate.Server.Domain.AuditEntries.Features;

using System.Diagnostics;
using Databases;
using Databases.Auditing;
using Dtos;
using Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;
using Services;
using Users;

public static class GetAuditEntryPage
{
    private const string EntityIdParameter = "entityId";

    public sealed record Query(AuditEntryParametersDto Parameters) : IRequest<AuditEntryPageDto>;

    /// <summary>
    /// Pages with a key, not an offset, because the log only grows: the order is the key of the table, newest first,
    /// and each page starts after the last entry of the page before. So a page costs the same at any depth, and no
    /// entry shows twice when a save adds entries between two pages.
    /// </summary>
    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
        : IRequestHandler<Query, AuditEntryPageDto>
    {
        public async Task<AuditEntryPageDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var parameters = request.Parameters;
            Activity.Current?.SetTag("app.audit.entity_id", parameters.EntityId);

            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            caller.Require(Permission.ReadAuditLog());
            var canReadSensitive = caller.Has(Permission.ReadAuditLogSensitive());
            Activity.Current?.SetTag("app.audit.reads_sensitive", canReadSensitive);

            var entries = dbContext.AuditEntries
                .TagWithOperationCallSite("audit_entries.page")
                .AsNoTracking();
            if (parameters.EntityId is { } entityId)
            {
                if (!PostgresText.CanStore(entityId))
                    throw new QueryInputException(EntityIdParameter, $"The {EntityIdParameter} value has a character that is not allowed.");
                entries = entries.Where(e => e.RootId == entityId);
            }
            if (parameters.Cursor is { } cursorText)
            {
                var cursor = AuditCursor.Parse(cursorText);
                entries = entries.Where(e => EF.Functions.LessThan(
                    ValueTuple.Create(e.OccurredAt, e.RecordId, e.Ordinal),
                    ValueTuple.Create(cursor.OccurredAt, cursor.RecordId, cursor.Ordinal)));
            }

            // One more row than the page tells if a next page exists
            var rows = await entries
                .OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.RecordId).ThenByDescending(e => e.Ordinal)
                .Take(parameters.PageSize + 1)
                .ToListAsync(cancellationToken);

            var page = rows.Take(parameters.PageSize).ToList();
            var vocabulary = AuditModel.Of(dbContext.Model).Vocabulary;
            return new AuditEntryPageDto
            {
                Items = page.Select(entry => AuditRedaction.Redact(entry, vocabulary, canReadSensitive)).ToList(),
                NextCursor = rows.Count > page.Count ? AuditCursor.Of(page[^1]).ToString() : null,
            };
        }
    }
}
