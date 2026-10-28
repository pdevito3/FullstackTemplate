namespace FullstackTemplate.UnitTests.EntityGraphs;

using FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;
using Shouldly;

/// <summary>Engine tests with small local types, so they do not depend on the domain.</summary>
public class EntityGraphCoreTests
{
    private static readonly Sequence<int> Numbers = Sequence.Of(1, n => n + 1);

    private static readonly Blueprint<Seed, Org> OrgBp = Blueprint
        .For<Seed, Org>(_ => new Org())
        .Derive(o => o.Number, Numbers)
        .Derive(o => o.Name, (o, _) => $"Org {o.Number}");

    private static readonly ChildBlueprint<Seed, Org, Member> MemberBp = Blueprint
        .ChildOf<Seed, Org, Member>((org, _) => new Member { Org = org })
        .Derive(m => m.Number, Numbers.PerParent());

    private static readonly ChildBlueprint<Seed, Member, Job> JobBp = Blueprint
        .ChildOf<Seed, Member, Job>((member, ctx) => new Job { Member = member, Org = ctx.Ancestor<Org>() })
        .Derive(t => t.Number, Numbers.Per<Org>());

    private static readonly InputBlueprint<Seed, TicketInput, Ticket> TicketBp = Blueprint
        .FromInput<Seed, TicketInput, Ticket>(() => new(), (input, _) => new Ticket { Number = input.Number!.Value })
        .Derive(i => i.Number, (_, ctx) => LowestFree(ctx.All<Ticket>().Select(t => t.Number)));

    private static readonly ChildInputBlueprint<Seed, Org, TicketInput, Ticket> OrgTicketBp = Blueprint
        .ChildFromInput<Seed, Org, TicketInput, Ticket>(() => new(), (_, input, _) => new Ticket { Number = input.Number!.Value })
        .Derive(i => i.Number, (_, ctx) => LowestFree(ctx.All<Ticket>().Select(t => t.Number)));

    private static readonly Blueprint<Seed, Member> LooseMemberBp = Blueprint
        .For<Seed, Member>(_ => new Member())
        .Derive(m => m.Number, (_, ctx) => LowestFree(ctx.All<Member>().Select(m => m.Number)));

    private static int LowestFree(IEnumerable<int> taken)
    {
        var used = taken.ToHashSet();
        var number = 1;
        while (used.Contains(number))
        {
            number++;
        }
        return number;
    }

    private static GraphBuilder<Seed, Seed> Start(
        GraphOptions<Seed>? options = null,
        IGraphStore? store = null,
        TimeProvider? time = null
    ) => EntityGraph.Start(new Seed(new Org { Name = "Seed org" }), options ?? new(), store, time);

    [Test]
    public void sequence_skips_explicit_values_in_the_same_step()
    {
        // Act
        Start().Add(OrgBp, out var orgs, o => o.Number = 1, _ => { }, _ => { });

        // Assert
        orgs.Select(o => o.Number).ShouldBe([1, 2, 3]);
        orgs[1].Name.ShouldBe("Org 2");
    }

    [Test]
    public void sequence_continues_across_steps_in_one_session()
    {
        // Act
        var graph = Start();
        graph.Add(OrgBp, out var first, 2);
        graph.Add(OrgBp, out var second, 2);

        // Assert
        second.Select(o => o.Number).ShouldBe([3, 4]);
    }

    [Test]
    public void sequences_restart_in_each_session()
    {
        // Act
        Start().AddOne(OrgBp, out var first);
        Start().AddOne(OrgBp, out var second);

        // Assert
        first.Number.ShouldBe(1);
        second.Number.ShouldBe(1);
    }

    [Test]
    public void per_parent_sequence_restarts_for_each_parent()
    {
        // Act
        Start()
            .Add(OrgBp, out var orgs, 2)
            .Add(MemberBp, out var members, 2);

        // Assert
        members.Select(m => m.Number).ShouldBe([1, 2, 1, 2]);
    }

    [Test]
    public void per_ancestor_sequence_counts_across_children_of_one_ancestor()
    {
        // Act
        Start()
            .Add(OrgBp, out var orgs, 2)
            .Add(MemberBp, out _, 2)
            .Add(JobBp, out var jobs);

        // Assert
        jobs.Where(t => t.Org == orgs[0]).Select(t => t.Number).ShouldBe([1, 2]);
        jobs.Where(t => t.Org == orgs[1]).Select(t => t.Number).ShouldBe([1, 2]);
    }

    [Test]
    public void time_is_frozen_for_the_session()
    {
        // Arrange
        var bp = Blueprint.For<Seed, Org>(ctx => new Org { CreatedAt = ctx.Now });

        // Act
        var graph = Start();
        graph.AddOne(bp, out var first);
        Thread.Sleep(5);
        graph.AddOne(bp, out var second);

        // Assert
        second.CreatedAt.ShouldBe(first.CreatedAt);
        first.CreatedAt.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Test]
    public void time_comes_from_the_provider_with_ago_and_from_now()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var options = new GraphOptions<Seed>();
        var bp = Blueprint.For<Seed, Org>(ctx => new Org
        {
            CreatedAt = ctx.Ago(TimeSpan.FromDays(1)),
            ExpiresAt = ctx.FromNow(TimeSpan.FromDays(1)),
        });

        // Act
        Start(options, time: new FixedTime(now)).AddOne(bp, out var org);

        // Assert
        org.CreatedAt.ShouldBe(now.AddDays(-1));
        org.ExpiresAt.ShouldBe(now.AddDays(1));
    }

    [Test]
    public void time_precision_truncates_now()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 1, 2, 3, 4, 5, 678, TimeSpan.Zero);
        var options = new GraphOptions<Seed>().TimePrecision(TimeSpan.FromSeconds(1));
        var bp = Blueprint.For<Seed, Org>(ctx => new Org { CreatedAt = ctx.Now });

        // Act
        Start(options, time: new FixedTime(now)).AddOne(bp, out var org);

        // Assert
        org.CreatedAt.ShouldBe(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
    }

    [Test]
    public void options_default_fills_only_unset_values_on_matching_types()
    {
        // Arrange
        var options = new GraphOptions<Seed>().Default<IHasLabel, string?>(e => e.Label, (_, _) => "default");

        // Act
        Start(options)
            .Add(OrgBp, out var orgs, o => o.Label = "explicit", _ => { });

        // Assert
        orgs[0].Label.ShouldBe("explicit");
        orgs[1].Label.ShouldBe("default");
    }

    [Test]
    public void new_id_throws_without_an_id_generator()
    {
        // Arrange
        var bp = Blueprint.For<Seed, Org>(ctx => new Org { Id = ctx.NewId("org") });

        // Act
        var act = () => Start().Add(bp, out _, 1);

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldContain("GraphOptions.Ids");
    }

    [Test]
    public void ids_option_replaces_the_id_generator()
    {
        // Arrange
        var next = 0;
        var options = new GraphOptions<Seed>().Ids(prefix => $"{prefix}_{++next}");
        var bp = Blueprint.For<Seed, Org>(ctx => new Org { Id = ctx.NewId("org") });

        // Act
        Start(options).Add(bp, out var orgs, 2);

        // Assert
        orgs.Select(o => o.Id).ShouldBe(["org_1", "org_2"]);
    }

    [Test]
    public void options_are_frozen_after_start()
    {
        // Arrange
        var options = new GraphOptions<Seed>();
        Start(options);

        // Act
        var act = () => options.TimePrecision(TimeSpan.Zero);

        // Assert
        Should.Throw<InvalidOperationException>(act);
    }

    [Test]
    public async Task seed_entities_are_owned_and_saved_first()
    {
        // Arrange
        var store = new RecordingStore();
        var options = new GraphOptions<Seed>().SeedEntities(s => [s.Org]);
        var seed = new Seed(new Org { Name = "Seed org" });

        // Act
        await EntityGraph.Start(seed, options, store)
            .Under(seed.Org)
            .AddOne(MemberBp, out var member)
            .BuildAsync();

        // Assert
        member.Org.ShouldBe(seed.Org);
        store.Saves.ShouldHaveSingleItem();
        store.Saves[0].ShouldBe([seed.Org, member]);
    }

    [Test]
    public async Task save_hooks_can_change_rows_and_run_around_the_store()
    {
        // Arrange
        var store = new RecordingStore();
        var extra = new Org { Name = "Extra" };
        var calls = new List<string>();
        var options = new GraphOptions<Seed>()
            .BeforeSave((ctx, _) =>
            {
                calls.Add($"before {ctx.Entities.Count}");
                ctx.Entities.Add(extra);
                return ValueTask.CompletedTask;
            })
            .AfterSave((ctx, _) =>
            {
                calls.Add($"after {store.Saves.Count} {ctx.Session.All<Org>().Count}");
                return ValueTask.CompletedTask;
            });

        // Act
        await Start(options, store).AddOne(OrgBp, out var org).BuildAsync();

        // Assert
        calls.ShouldBe(["before 1", "after 1 1"]);
        store.Saves[0].ShouldBe([org, extra]);
    }

    [Test]
    public async Task transient_outputs_are_not_saved()
    {
        // Arrange
        var store = new RecordingStore();

        // Act
        var graph = Start(store: store);
        graph.Add(OrgBp.AsTransient(), out var fromBlueprint);
        graph.Add(OrgBp, out var fromStep).Transient();
        graph.Add(OrgBp, out var saved);
        await graph.BuildAsync();

        // Assert
        store.Saves[0].ShouldBe([saved[0]]);
        graph.All<Org>().Count.ShouldBe(3);
    }

    [Test]
    public async Task aggregates_save_their_parts_and_not_themselves()
    {
        // Arrange
        var store = new RecordingStore();
        var bp = Blueprint.For<Seed, OrgWithOwner>(_ =>
        {
            var org = new Org { Name = "Aggregate org" };
            return new OrgWithOwner(org, new Member { Org = org });
        });

        // Act
        await Start(store: store).AddOne(bp, out var aggregate).BuildAsync();

        // Assert
        store.Saves[0].ShouldBe([aggregate.Org, aggregate.Owner]);
    }

    [Test]
    public void context_aware_customizers_see_the_index_and_seed()
    {
        // Act
        var graph = Start();
        graph.Add(OrgBp, out var orgs, (o, ctx) => o.Name = $"{ctx.Seed.Org.Name} {ctx.Index}", (o, ctx) => o.Name = $"n{ctx.Index}");
        graph.AddOne(OrgBp, out var last, (o, ctx) => o.Label = ctx.Last<Org>().Name);

        // Assert
        orgs.Select(o => o.Name).ShouldBe(["Seed org 0", "n1"]);
        last.Label.ShouldBe("n1");
    }

    [Test]
    public void with_trait_runs_on_every_output()
    {
        // Arrange
        var bp = OrgBp.With(o => o.Label = "trait");

        // Act
        Start().Add(bp, out var orgs, 2);

        // Assert
        orgs.ShouldAllBe(o => o.Label == "trait");
    }

    [Test]
    public void under_accepts_a_single_owned_parent()
    {
        // Act
        var graph = Start();
        graph.Add(OrgBp, out var orgs, 2);
        graph.Under(orgs[1]).AddOne(MemberBp, out var member);

        // Assert
        member.Org.ShouldBe(orgs[1]);
    }

    [Test]
    public void value_derive_sees_the_explicit_values_of_later_siblings_in_the_same_step()
    {
        // Act
        Start().Add(TicketBp, out var tickets, _ => { }, _ => { }, t => t.Number = 1);

        // Assert
        tickets.Select(t => t.Number).ShouldBe([2, 3, 1]);
    }

    [Test]
    public void child_value_derive_sees_the_explicit_values_of_later_siblings_in_the_same_step()
    {
        // Act
        Start().AddOne(OrgBp, out _).Add(OrgTicketBp, out var tickets, _ => { }, _ => { }, t => t.Number = 1);

        // Assert
        tickets.Select(t => t.Number).ShouldBe([2, 3, 1]);
    }

    [Test]
    public void entity_value_derive_sees_the_explicit_values_of_later_siblings_in_the_same_step()
    {
        // Act
        Start().Add(LooseMemberBp, out var members, _ => { }, m => m.Number = 1);

        // Assert
        members.Select(m => m.Number).ShouldBe([2, 1]);
    }

    public sealed record Seed(Org Org);

    public interface IHasLabel
    {
        string? Label { get; set; }
    }

    public sealed class Org : IHasLabel
    {
        public string Id { get; set; } = default!;
        public string? Name { get; set; }
        public int Number { get; set; }
        public string? Label { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }

    public sealed class Member
    {
        public Org Org { get; set; } = default!;
        public int Number { get; set; }
    }

    public sealed class Job
    {
        public Member Member { get; set; } = default!;
        public Org Org { get; set; } = default!;
        public int Number { get; set; }
    }

    public sealed class TicketInput
    {
        public int? Number { get; set; }
    }

    public sealed class Ticket
    {
        public int Number { get; set; }
    }

    public sealed record OrgWithOwner(Org Org, Member Owner) : IGraphAggregate
    {
        public IEnumerable<object> GetEntities() => [Org, Owner];
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingStore : IGraphStore
    {
        public List<List<object>> Saves { get; } = [];

        public ValueTask SaveAsync(IReadOnlyList<object> entities, CancellationToken cancellationToken)
        {
            Saves.Add([.. entities]);
            return ValueTask.CompletedTask;
        }
    }
}
