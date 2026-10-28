namespace FullstackTemplate.Server.Databases;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

/// <summary>The <see cref="ConstraintError"/> of each unique index and each exclusion constraint of the schema.</summary>
public static class ConstraintErrors
{
    private const string RequestFieldAnnotation = "App:ConstraintError";
    private const string StaleVersionAnnotation = "App:StaleVersionConstraint";
    private const string ModelAnnotation = "App:ConstraintErrors";

    /// <summary>
    /// The exclusion constraints and the unique indexes that only a migration adds, such as an index on a column of a
    /// value object. The EF model does not have them, so <see cref="ForRequestField"/> and <see cref="ForStaleVersion"/>
    /// cannot name them.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, ConstraintError> NotInModel = new Dictionary<string, ConstraintError>();

    /// <summary>
    /// Gives a unique index its request field and the message of the duplicate check of the handlers. A save that the
    /// index stops then gives a 409 on the field with the message.
    /// </summary>
    public static IndexBuilder<TEntity> ForRequestField<TEntity>(this IndexBuilder<TEntity> builder, string field, string message)
        => builder.HasAnnotation(RequestFieldAnnotation, new ConstraintError.RequestField(field, message));

    /// <summary>
    /// Marks a unique index that stops a save only when another user changed the record after the caller loaded it,
    /// for example one current revision of each document. A save that the index stops then gives the stale-version
    /// 409, so the caller loads the record again.
    /// </summary>
    public static IndexBuilder<TEntity> ForStaleVersion<TEntity>(this IndexBuilder<TEntity> builder)
        => builder.HasAnnotation(StaleVersionAnnotation, new ConstraintError.StaleVersion());

    /// <summary>The errors by constraint name.</summary>
    public static IReadOnlyDictionary<string, ConstraintError> Of(IModel model)
        => model.FindRuntimeAnnotationValue(ModelAnnotation) as IReadOnlyDictionary<string, ConstraintError>
            ?? throw new InvalidOperationException($"The model has no constraint errors. Call {nameof(MapConstraintErrors)} in ConfigureConventions.");

    /// <summary>
    /// Moves the annotation of each index to one runtime annotation of the model. A runtime annotation is not in the
    /// design-time model, so the model snapshot does not change and the index needs no migration.
    /// </summary>
    public static void MapConstraintErrors(this ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.Conventions.Add(_ => new ConstraintErrorsConvention());

    private sealed class ConstraintErrorsConvention : IModelFinalizingConvention, IModelFinalizedConvention
    {
        private readonly List<(IReadOnlyIndex Index, ConstraintError Error)> _indexes = [];

        public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder,
            IConventionContext<IConventionModelBuilder> context)
        {
            foreach (var index in modelBuilder.Metadata.GetEntityTypes().SelectMany(e => e.GetDeclaredIndexes()))
            {
                var requestField = index.RemoveAnnotation(RequestFieldAnnotation)?.Value as ConstraintError;
                var staleVersion = index.RemoveAnnotation(StaleVersionAnnotation)?.Value as ConstraintError;
                if ((requestField ?? staleVersion) is not { } error)
                    continue;

                var name = $"{index.DisplayName()} of {index.DeclaringEntityType.DisplayName()}";
                if (requestField is not null && staleVersion is not null)
                    throw new InvalidOperationException(
                        $"The index {name} has both ForRequestField and ForStaleVersion. Keep the one that tells what a save that the index stops means.");
                if (!index.IsUnique)
                    throw new InvalidOperationException(
                        $"The index {name} has {(staleVersion is null ? nameof(ForRequestField) : nameof(ForStaleVersion))}, but it is not unique. Call IsUnique on the index.");
                _indexes.Add((index, error));
            }
        }

        /// <summary>Throws when an index of the model also has an entry in <see cref="NotInModel"/>.</summary>
        public IModel ProcessModelFinalized(IModel model)
        {
            var errors = new Dictionary<string, ConstraintError>(NotInModel);
            foreach (var (index, error) in _indexes)
            {
                var name = index.GetDatabaseName() ?? throw new InvalidOperationException(
                    $"The index {index.DisplayName()} of {index.DeclaringEntityType.DisplayName()} has a constraint error, but it is not on a table.");
                if (!errors.TryAdd(name, error))
                    throw new InvalidOperationException(
                        $"The index {name} has a constraint error in its configuration and an entry in NotInModel. Remove the entry.");
            }

            model.SetRuntimeAnnotation(ModelAnnotation, errors);
            return model;
        }
    }
}
