using System.Linq.Expressions;
using System.Reflection;

namespace FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;

internal interface IDerivation<TSeed, T>
{
    void Reserve(T entity, GraphContext<TSeed> context);
    void Apply(T entity, GraphContext<TSeed> context, IReadOnlyList<T> step);
}

internal sealed class Derivation<TSeed, T, TProp> : IDerivation<TSeed, T>
{
    private readonly Func<T, TProp> get;
    private readonly Action<T, TProp> set;
    private readonly Func<T, GraphContext<TSeed>, IReadOnlyList<T>, TProp>? value;
    private readonly Sequence<TProp>? sequence;

    internal Derivation(
        Expression<Func<T, TProp>> property,
        Func<T, GraphContext<TSeed>, IReadOnlyList<T>, TProp>? value,
        Sequence<TProp>? sequence = null
    )
    {
        ArgumentNullException.ThrowIfNull(property);
        if (
            property.Body
                is not MemberExpression
                {
                    Member: PropertyInfo info,
                    Expression: ParameterExpression
                }
            || info.SetMethod is null
            || info.GetMethod is null
            || info.SetMethod.IsStatic
        )
        {
            throw new ArgumentException(
                "Select a directly writable instance property, for example entity => entity.Name.",
                nameof(property)
            );
        }
        get = property.Compile();
        var input = Expression.Parameter(typeof(TProp), "value");
        set = Expression
            .Lambda<Action<T, TProp>>(
                Expression.Assign(property.Body, input),
                property.Parameters[0],
                input
            )
            .Compile();
        this.value = value;
        this.sequence = sequence;
    }

    public void Reserve(T entity, GraphContext<TSeed> context)
    {
        var current = get(entity);
        if (sequence is not null && !EqualityComparer<TProp>.Default.Equals(current, default))
        {
            context.Session.Reserve(sequence, context, current);
        }
    }

    public void Apply(T entity, GraphContext<TSeed> context, IReadOnlyList<T> step)
    {
        if (EqualityComparer<TProp>.Default.Equals(get(entity), default))
        {
            set(entity, sequence is null ? value!(entity, context, step) : context.Next(sequence));
        }
    }
}
