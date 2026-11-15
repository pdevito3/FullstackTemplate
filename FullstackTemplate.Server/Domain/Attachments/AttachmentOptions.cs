namespace FullstackTemplate.Server.Domain.Attachments;

/// <summary>The settings of the attachments, in <c>Attachments</c>.</summary>
public sealed record AttachmentOptions
{
    public const string SectionName = "Attachments";

    /// <summary>
    /// The longest time of one add, from the start of its put to the end of the save of its row. At this time the add
    /// stops its put or its save, and throws a <see cref="TimeoutException"/>.
    /// </summary>
    public TimeSpan AddTimeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <exception cref="InvalidOperationException">A setting is out of its range.</exception>
    public static AttachmentOptions From(IConfiguration configuration)
    {
        var options = configuration.GetSection(SectionName).Get<AttachmentOptions>() ?? new AttachmentOptions();
        if (options.AddTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException($"{SectionName}:AddTimeout must be over zero.");
        return options;
    }
}
