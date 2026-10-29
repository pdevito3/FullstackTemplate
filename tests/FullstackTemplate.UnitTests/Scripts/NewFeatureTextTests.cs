namespace FullstackTemplate.UnitTests.Scripts;

using System.Diagnostics;
using Shouldly;

/// <summary>
/// scripts/new-feature.cs makes the messages and the tool descriptions of a feature from the names of the entity and
/// its properties. With <c>--messages</c> it prints them and writes no file, so these tests read them for names that
/// are hard to put in a sentence.
/// </summary>
[NotInParallel(nameof(NewFeatureTextTests))]
public class NewFeatureTextTests
{
    [Test]
    public async Task each_text_is_a_correct_sentence_for_each_kind_of_property()
    {
        // Act
        var lines = await MessagesAsync("UnitLoad", "Status:text, Notes:text?, IsFragile:bool, DueOn:date, CountedAt:instant, Items:int");

        // Assert
        lines.ShouldBe([
            "add to other tenant: You can add a unit load only to your own tenant.",
            "Status missing: Please give the unit load a value for \"Status\".",
            "Status too long: The \"Status\" value of a unit load can have no more than {StatusMaxLength} characters.",
            "Status bad character: The \"Status\" value of a unit load has a character that is not allowed.",
            "Status tool parameter: The \"Status\" value of the unit load.",
            "Notes too long: The \"Notes\" value of a unit load can have no more than {NotesMaxLength} characters.",
            "Notes bad character: The \"Notes\" value of a unit load has a character that is not allowed.",
            "Notes tool parameter: The \"Notes\" value of the unit load. Leave it out for none.",
            "IsFragile tool parameter: The \"Is fragile\" value of the unit load: true or false.",
            "DueOn missing: Please give the unit load a value for \"Due on\".",
            "DueOn tool parameter: The \"Due on\" value of the unit load, a date such as 2026-01-02.",
            "CountedAt missing: Please give the unit load a value for \"Counted at\".",
            "CountedAt tool parameter: The \"Counted at\" value of the unit load, an ISO 8601 instant such as 2026-01-02T10:00:00Z.",
            "Items out of range: The \"Items\" value of a unit load is from {ItemsMinimum} to {ItemsMaximum}.",
            "Items tool parameter: The \"Items\" value of the unit load, a whole number. A value out of range gives the invalid_argument error.",
            "add tool: Adds a unit load to the caller's tenant. The caller needs the manage_unit_loads permission in the tenant.",
            "get tool: Gets one unit load of the caller's tenant by its ID.",
            "list tool: Gets one page of the unit loads of the caller's tenant, with the page data.",
            "list search: A text that the \"Status\" value contains, with no regard to case.",
            "list filters: A QueryKit filter on the fields of a unit load, for example: status @=* \"text\"",
            "update tool: Updates a unit load of the caller's tenant. The caller needs the manage_unit_loads permission in the tenant.",
        ]);
    }

    [Test]
    [Arguments("Shipment", "a shipment")]
    [Arguments("Order", "an order")]
    [Arguments("UserNote", "a user note")]
    [Arguments("UsageReport", "a usage report")]
    [Arguments("UnpaidBill", "an unpaid bill")]
    [Arguments("EuroPrice", "a euro price")]
    [Arguments("OneTimeFee", "a one time fee")]
    [Arguments("HourLog", "an hour log")]
    [Arguments("HonorRoll", "an honor roll")]
    [Arguments("Hotel", "a hotel")]
    public async Task the_article_of_an_entity_fits_the_sound_of_its_first_word(string entity, string expected)
    {
        // Act
        var lines = await MessagesAsync(entity, "Name:text");

        // Assert
        lines[0].ShouldBe($"add to other tenant: You can add {expected} only to your own tenant.");
    }

    private static async Task<List<string>> MessagesAsync(string entity, string fields)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = FindRepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[] { "run", "scripts/new-feature.cs", "--", entity, "--fields", fields, "--messages" })
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start).ShouldNotBeNull();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        process.ExitCode.ShouldBe(0, await error);
        return (await output).ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FullstackTemplate.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
