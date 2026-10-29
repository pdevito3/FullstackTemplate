namespace FullstackTemplate.UnitTests.Databases.Auditing;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Databases.Auditing;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.AuditEntries;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

public class AuditSensitivityTests
{
    [Test]
    public void each_recorded_value_of_an_entity_and_each_property_of_an_action_is_classified()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);
        var vocabulary = AuditModel.Of(context.Model).Vocabulary;

        // Act
        var unclassified = vocabulary.EntityTypes
            .Concat(vocabulary.Actions)
            .SelectMany(known => known.Value
                .Where(value => value.Value == Sensitivity.Unclassified)
                .Select(value => $"{known.Key}.{value.Key}"))
            .ToList();

        // Assert
        unclassified.ShouldBeEmpty(
            "The audit log hides an unclassified value from each reader without read_audit_log_sensitive. Mark the property [Sensitive] or [NotSensitive].");
    }

    [Test]
    [Arguments(nameof(Values.Text), nameof(Sensitivity.Unclassified))]
    [Arguments(nameof(Values.Structure), nameof(Sensitivity.Unclassified))]
    [Arguments(nameof(Values.BothMarks), nameof(Sensitivity.Unclassified))]
    [Arguments(nameof(Values.BirthDate), nameof(Sensitivity.Unclassified))]
    [Arguments(nameof(Values.Salary), nameof(Sensitivity.Unclassified))]
    [Arguments(nameof(Values.Moment), nameof(Sensitivity.Unclassified))]
    [Arguments(nameof(Values.Score), nameof(Sensitivity.Unclassified))]
    [Arguments(nameof(Values.SensitiveNumber), nameof(Sensitivity.Sensitive))]
    [Arguments(nameof(Values.NotSensitiveText), nameof(Sensitivity.NotSensitive))]
    [Arguments(nameof(Values.Number), nameof(Sensitivity.NotSensitive))]
    [Arguments(nameof(Values.Flag), nameof(Sensitivity.NotSensitive))]
    [Arguments(nameof(Values.Key), nameof(Sensitivity.NotSensitive))]
    [Arguments(nameof(Values.Format), nameof(Sensitivity.NotSensitive))]
    public void mark_decides_and_with_no_mark_only_a_flag_an_enum_a_guid_or_an_integer_is_not_sensitive(string property,
        string expected)
    {
        // Arrange
        var member = typeof(Values).GetProperty(property)!;

        // Act
        var sensitivity = AuditSensitivity.Of(member, member.PropertyType, isForeignKey: false);

        // Assert
        sensitivity.ShouldBe(Enum.Parse<Sensitivity>(expected));
    }

    [Test]
    public void foreign_key_with_no_mark_is_not_sensitive_and_a_mark_still_decides()
    {
        // Arrange
        var text = typeof(Values).GetProperty(nameof(Values.Text))!;
        var bothMarks = typeof(Values).GetProperty(nameof(Values.BothMarks))!;

        // Act
        var unmarked = AuditSensitivity.Of(text, typeof(string), isForeignKey: true);
        var marked = AuditSensitivity.Of(bothMarks, typeof(string), isForeignKey: true);

        // Assert
        (unmarked, marked).ShouldBe((Sensitivity.NotSensitive, Sensitivity.Unclassified));
    }

    [Test]
    public void column_type_of_a_value_converter_does_not_decide()
    {
        // Arrange
        using var context = new ConvertedValuesContext();
        var values = AuditModel.Of(context.Model).ValuesOf(context.Model.FindEntityType(typeof(Payslip))!);

        // Act
        var sensitivities = values.Where(value => value.IsRecorded).ToDictionary(value => value.Path, value => value.Sensitivity);

        // Assert
        sensitivities.ShouldBe(new Dictionary<string, Sensitivity>
        {
            [nameof(Payslip.Salary)] = Sensitivity.Unclassified,
            [nameof(Payslip.Format)] = Sensitivity.NotSensitive,
        }, ignoreOrder: true);
    }

    [Test]
    public void action_type_that_shares_a_name_with_another_is_refused()
    {
        // Act
        var act = () => AuditVocabulary.Of(
            new Dictionary<Microsoft.EntityFrameworkCore.Metadata.IEntityType, IReadOnlyList<AuditedValue>>(),
            [typeof(UsersExported), typeof(UsersExportedAgain)]);

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message
            .ShouldBe("UsersExported and UsersExportedAgain have the audit action name 'users_exported'. Give each action a name of its own.");
    }

    [Test]
    public void stored_properties_of_an_action_are_known_and_its_subject_is_not()
    {
        // Act
        var action = AuditVocabulary.Of(
            new Dictionary<Microsoft.EntityFrameworkCore.Metadata.IEntityType, IReadOnlyList<AuditedValue>>(),
            [typeof(UsersExported)]).Actions["users_exported"];

        // Assert
        action.ShouldBe(new Dictionary<string, Sensitivity>
        {
            ["Format"] = Sensitivity.NotSensitive,
            ["Count"] = Sensitivity.NotSensitive,
            ["Reason"] = Sensitivity.Sensitive,
        }, ignoreOrder: true);
    }

    private sealed class Values
    {
        public string Text { get; init; } = "";
        public Values? Structure { get; init; }
        [Sensitive, NotSensitive] public string BothMarks { get; init; } = "";
        [Sensitive] public int SensitiveNumber { get; init; }
        [NotSensitive] public string NotSensitiveText { get; init; } = "";
        public long? Number { get; init; }
        public bool Flag { get; init; }
        public Guid Key { get; init; }
        public DateOnly? BirthDate { get; init; }
        public decimal Salary { get; init; }
        public DateTimeOffset Moment { get; init; }
        public double Score { get; init; }
        public ExportFormat Format { get; init; }
    }

    [AuditActionName("users_exported")]
    private sealed record UsersExportedAgain(AuditSubject Subject) : AuditAction(Subject);
}

/// <summary>The tenant of <see cref="AppDbContext"/> and a <see cref="Payslip"/> whose values have converters.</summary>
file sealed class ConvertedValuesContext() : AppDbContext(
    new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=unused").UseSnakeCaseNamingConvention().Options,
    TimeProvider.System, Substitute.For<ICurrentUserService>(), Substitute.For<ICallerIdentityResolver>(),
    Substitute.For<ITenantScopeProvider>(), new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()))
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.IgnoreTheAppEntities();
        modelBuilder.Entity<Payslip>(payslip =>
        {
            payslip.HasKey(p => p.Id);
            payslip.Property(p => p.Salary).HasConversion<long>();
            payslip.Property(p => p.Format).HasConversion<string>();
        });
    }
}

file sealed class Payslip() : BaseEntity("pay")
{
    public decimal Salary { get; private set; }
    public ExportFormat Format { get; private set; }
}
