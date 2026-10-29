namespace FullstackTemplate.Server.Examples.Workshops;

using Ardalis.SmartEnum;
using Domain;
using Exceptions;

public partial class WorkshopStatus : ValueObject, IFixedNames
{
    private const string RequestField = "status";

    private WorkshopStatusEnum _status = null!;

    public string Value
    {
        get => _status.Name;
        private set
        {
            if (!WorkshopStatusEnum.TryFromName(value, true, out var parsed))
                throw new ValidationException(RequestField, $"Invalid workshop status: {value}");

            _status = parsed;
        }
    }

    public WorkshopStatus(string value)
    {
        ValidationException.ThrowWhenNullOrWhitespace(value, RequestField, "Please provide a workshop status.");
        Value = value;
    }

    public string Words => _status.Words;

    public bool CanBeEdited => _status.CanBeEdited;

    public bool CanMoveTo(WorkshopStatus target) => _status.Next.Contains(target._status);

    public WorkshopStatus MoveTo(WorkshopStatus target)
    {
        ValidationException.Must(CanMoveTo(target), RequestField, MoveRefusal(target));
        return target;
    }

    public string MoveRefusal(WorkshopStatus target)
        => $"A {Words} workshop cannot be {target.Words}.";

    public override string ToString() => Value;

    public static WorkshopStatus Of(string value) => new(value);
    public static List<string> ListNames() => WorkshopStatusEnum.List.OrderBy(x => x.Value).Select(x => x.Name).ToList();

    public static WorkshopStatus Draft() => new(WorkshopStatusEnum.Draft.Name);
    public static WorkshopStatus Scheduled() => new(WorkshopStatusEnum.Scheduled.Name);
    public static WorkshopStatus Cancelled() => new(WorkshopStatusEnum.Cancelled.Name);
    public static WorkshopStatus Completed() => new(WorkshopStatusEnum.Completed.Name);

    protected WorkshopStatus() { } // EF Core

    private abstract class WorkshopStatusEnum(string name, int value, string words)
        : SmartEnum<WorkshopStatusEnum>(name, value)
    {
        public static readonly WorkshopStatusEnum Draft = new DraftType();
        public static readonly WorkshopStatusEnum Scheduled = new ScheduledType();
        public static readonly WorkshopStatusEnum Cancelled = new CancelledType();
        public static readonly WorkshopStatusEnum Completed = new CompletedType();

        public string Words { get; } = words;

        public virtual bool CanBeEdited => false;

        public abstract IReadOnlyList<WorkshopStatusEnum> Next { get; }

        private sealed class DraftType() : WorkshopStatusEnum("Draft", 0, "draft")
        {
            public override bool CanBeEdited => true;
            public override IReadOnlyList<WorkshopStatusEnum> Next => [Scheduled, Cancelled];
        }

        private sealed class ScheduledType() : WorkshopStatusEnum("Scheduled", 1, "scheduled")
        {
            public override IReadOnlyList<WorkshopStatusEnum> Next => [Cancelled, Completed];
        }

        private sealed class CancelledType() : WorkshopStatusEnum("Cancelled", 2, "cancelled")
        {
            public override IReadOnlyList<WorkshopStatusEnum> Next => [];
        }

        private sealed class CompletedType() : WorkshopStatusEnum("Completed", 3, "completed")
        {
            public override IReadOnlyList<WorkshopStatusEnum> Next => [];
        }
    }
}
