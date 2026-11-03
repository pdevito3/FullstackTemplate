namespace FullstackTemplate.SharedTestHelpers.Examples.Fakes;

using Bogus;
using FullstackTemplate.Server.Examples.Workshops;

public static class WorkshopAttachmentRules
{
    public static string FileName(Faker f) => $"{f.Hacker.Noun()} notes.pdf";

    public static string ContentType() => "application/pdf";

    public static long SizeBytes(Faker f) => f.Random.Long(1, WorkshopAttachment.SizeBytesMaximum);
}
