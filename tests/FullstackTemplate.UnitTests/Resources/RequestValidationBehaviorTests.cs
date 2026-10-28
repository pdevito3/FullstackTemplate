namespace FullstackTemplate.UnitTests.Resources;

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users.Dtos;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.Server.Resources.Entrypoints.Http;
using MediatR;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Shouldly;

public class RequestValidationBehaviorTests
{
    [Test]
    public async Task a_page_out_of_range_gives_an_error_for_each_field()
    {
        // Arrange
        var parameters = new TenantParametersDto { PageNumber = 0, PageSize = -1 };

        // Act
        var exception = await Should.ThrowAsync<RequestValidationException>(
            () => Send(new GetTenantList.Query(parameters)));

        // Assert
        exception.Errors.Keys.ShouldBe(["pageNumber", "pageSize"], ignoreOrder: true);
        exception.Errors["pageNumber"].ShouldBe(["The field pageNumber must be between 1 and 4294967."]);
        exception.Errors["pageSize"].ShouldBe(["The field pageSize must be between 1 and 2147483647."]);
    }

    [Test]
    public async Task a_missing_field_of_the_dto_gives_the_json_name()
    {
        // Act
        var exception = await Should.ThrowAsync<RequestValidationException>(
            () => Send(new AddUser.Command(new UserForCreationDto { FirstName = "Ada" })));

        // Assert
        exception.Errors.Keys.ShouldBe(["lastName", "identifier", "email", "username", "role"], ignoreOrder: true);
        exception.Errors["lastName"].ShouldBe(["The lastName field is required."]);
    }

    [Test]
    public async Task a_missing_field_of_an_element_of_a_list_gives_the_list_and_the_index()
    {
        // Arrange
        var order = new OrderDto { Number = "ord-1", Lines = [new LineDto { Name = "Bolt" }, new LineDto { Name = null! }] };

        // Act
        var exception = await Should.ThrowAsync<RequestValidationException>(() => Send(new AddOrder(order)));

        // Assert
        exception.Errors.ShouldHaveSingleItem().Key.ShouldBe("lines[1].name");
        exception.Errors["lines[1].name"].ShouldBe(["The lines[1].name field is required."]);
    }

    [Test]
    public async Task a_missing_field_at_any_depth_gives_its_path()
    {
        // Arrange
        var order = new OrderDto
        {
            Number = "ord-1",
            ShipTo = new AddressDto { City = null! },
            Lines =
            [
                new LineDto { Name = "Bolt", Parts = [new PartDto { Name = "Head" }, new PartDto { Name = null! }] },
                new LineDto { Name = "Nut" },
            ],
        };

        // Act
        var exception = await Should.ThrowAsync<RequestValidationException>(() => Send(new AddOrder(order)));

        // Assert
        exception.Errors.Keys.ShouldBe(["shipTo.city", "lines[0].parts[1].name"], ignoreOrder: true);
    }

    [Test]
    public async Task a_missing_field_of_an_element_of_a_list_of_the_command_gives_the_list_and_the_index()
    {
        // Act
        var exception = await Should.ThrowAsync<RequestValidationException>(
            () => Send(new AddLines([new LineDto { Name = null! }])));

        // Assert
        exception.Errors.ShouldHaveSingleItem().Key.ShouldBe("lines[0].name");
    }

    [Test]
    public async Task a_missing_field_of_an_element_of_a_collection_class_gives_the_list_and_the_index()
    {
        // Arrange
        var batch = new BatchDto { Lines = [new LineDto { Name = "Bolt" }, new LineDto { Name = null! }] };

        // Act
        var exception = await Should.ThrowAsync<RequestValidationException>(() => Send(new AddBatch(batch)));

        // Assert
        exception.Errors.ShouldHaveSingleItem().Key.ShouldBe("lines[1].name");
    }

    [Test]
    public async Task a_graph_with_a_cycle_gives_the_error_of_each_node_once()
    {
        // Arrange
        var parent = new NodeDto { Name = "root" };
        var child = new NodeDto { Name = null!, Owner = parent };
        parent.Children.Add(child);

        // Act
        var exception = await Should.ThrowAsync<RequestValidationException>(() => Send(new AddNode(parent)));

        // Assert
        exception.Errors.ShouldHaveSingleItem().Key.ShouldBe("children[0].name");
    }

    [Test]
    public async Task an_order_with_valid_lines_goes_to_the_handler()
    {
        // Arrange
        var order = new OrderDto
        {
            Number = "ord-1",
            Lines = [new LineDto { Name = "Bolt", Parts = [new PartDto { Name = "Head" }] }, new LineDto { Name = "Nut" }],
        };

        // Act
        var sent = await Send(new AddOrder(order));

        // Assert
        sent.ShouldBeTrue();
    }

    [Test]
    public async Task a_missing_field_of_the_command_gives_the_json_name()
    {
        // Act
        var exception = await Should.ThrowAsync<RequestValidationException>(
            () => Send(new UpdateUserRole.Command("usr_1", null!)));

        // Assert
        exception.Errors.ShouldHaveSingleItem().Key.ShouldBe("role");
    }

    [Test]
    public async Task an_empty_string_goes_to_the_handler()
    {
        // Act
        var sent = await Send(new AddTenant.Command(new TenantForCreationDto { Name = "" }));

        // Assert
        sent.ShouldBeTrue();
    }

    [Test]
    public async Task a_large_page_size_is_limited_and_goes_to_the_handler()
    {
        // Act
        var sent = await Send(new GetTenantList.Query(new TenantParametersDto { PageSize = 5000 }));

        // Assert
        sent.ShouldBeTrue();
    }

    /// <summary>
    /// The HTTP list endpoints keep <c>[Range]</c> on their parameters only for the OpenAPI contract.
    /// The behavior applies the rules of the DTO, so each copy must be the same as the rule of the DTO that the
    /// endpoint builds.
    /// </summary>
    [Test]
    public void each_range_of_an_endpoint_parameter_is_the_range_of_the_dto()
    {
        // Arrange
        var endpoints = typeof(IEndpoint).Assembly.GetTypes()
            .Where(type => typeof(IEndpoint).IsAssignableFrom(type) && !type.IsInterface)
            .ToList();

        // Act
        var mismatches = RangeMismatches(endpoints);

        // Assert
        endpoints.SelectMany(RangedParameters).ShouldNotBeEmpty();
        mismatches.ShouldBeEmpty();
    }

    [Test]
    public void the_range_guard_compares_a_parameter_with_the_dto_that_the_endpoint_builds()
    {
        // Act
        var matching = RangeMismatches([typeof(GetDueListEndpoint), typeof(GetOverdueListEndpoint)]);
        var mismatched = RangeMismatches([typeof(GetOverdueListWithTheDueRangeEndpoint)]);
        var unmatched = RangeMismatches([typeof(GetDueListWithAnExtraRangeEndpoint)]);

        // Assert
        matching.ShouldBeEmpty();
        mismatched.ShouldBe(["GetOverdueListWithTheDueRangeEndpoint.withinDays: (1, 365, False, False), but OverdueParametersDto has (1, 90, False, False)"]);
        unmatched.ShouldBe(["GetDueListWithAnExtraRangeEndpoint.limit: 0 types that the endpoint builds have a range on limit"]);
    }

    private static IEnumerable<(ParameterInfo Parameter, RangeAttribute Range)> RangedParameters(Type endpoint)
        => endpoint.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .SelectMany(method => method.GetParameters())
            .Where(parameter => parameter.IsDefined(typeof(RangeAttribute)))
            .Select(parameter => (parameter, parameter.GetCustomAttribute<RangeAttribute>()!));

    private static List<string> RangeMismatches(IEnumerable<Type> endpoints)
    {
        var modules = new Dictionary<Assembly, ModuleDefinition>();
        var mismatches = new List<string>();
        foreach (var endpoint in endpoints)
        {
            var ranged = RangedParameters(endpoint).ToList();
            if (ranged.Count == 0)
                continue;

            if (!modules.TryGetValue(endpoint.Assembly, out var module))
                modules[endpoint.Assembly] = module = ModuleDefinition.ReadModule(new MemoryStream(File.ReadAllBytes(endpoint.Assembly.Location)));
            var built = BuiltTypes(endpoint, module);

            foreach (var (parameter, range) in ranged)
            {
                var dtoRanges = built
                    .Select(type => (Type: type, Range: type
                        .GetProperty(parameter.Name!, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                        ?.GetCustomAttribute<RangeAttribute>()))
                    .Where(item => item.Range is not null)
                    .ToList();
                var name = $"{endpoint.Name}.{parameter.Name}";
                if (dtoRanges is not [var dto])
                    mismatches.Add($"{name}: {dtoRanges.Count} types that the endpoint builds have a range on {parameter.Name}");
                else if (!Bounds(range).Equals(Bounds(dto.Range!)))
                    mismatches.Add($"{name}: {Bounds(range)}, but {dto.Type.Name} has {Bounds(dto.Range!)}");
            }
        }

        return mismatches;
    }

    private static (object Minimum, object Maximum, bool MinimumIsExclusive, bool MaximumIsExclusive) Bounds(RangeAttribute range)
        => (range.Minimum, range.Maximum, range.MinimumIsExclusive, range.MaximumIsExclusive);

    /// <summary>
    /// The type of each object that a method of the endpoint creates or gets from a call. The IL of an async method
    /// is in a nested state machine, so the nested types count too.
    /// </summary>
    private static HashSet<Type> BuiltTypes(Type endpoint, ModuleDefinition module)
    {
        var types = new Stack<TypeDefinition>([(TypeDefinition)module.LookupToken(endpoint.MetadataToken)]);
        var built = new HashSet<Type>();
        while (types.TryPop(out var type))
        {
            foreach (var nested in type.NestedTypes)
                types.Push(nested);
            foreach (var instruction in type.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions))
            {
                if (instruction.OpCode.Code is not (Code.Call or Code.Callvirt or Code.Newobj))
                    continue;
                var method = endpoint.Module.ResolveMethod(((MethodReference)instruction.Operand).MetadataToken.ToInt32())!;
                built.Add(method is MethodInfo info ? info.ReturnType : method.DeclaringType!);
            }
        }

        return built;
    }

    [Test]
    public void each_string_of_a_request_dto_that_is_not_nullable_is_a_required_field()
    {
        // Arrange
        var requests = typeof(RequestValidationBehavior<,>).Assembly.GetTypes()
            .Where(type => typeof(IBaseRequest).IsAssignableFrom(type) && type is { IsClass: true, IsAbstract: false });

        // Act
        var requestDtos = RequestDtos(requests);

        // Assert
        requestDtos.ShouldContain(typeof(UserForCreationDto));
        UnmarkedStrings(requestDtos).ShouldBeEmpty();
    }

    [Test]
    public void the_required_field_guard_finds_a_string_of_a_dto_in_a_list_at_any_depth()
    {
        // Act
        var unmarked = UnmarkedStrings(RequestDtos([typeof(AddKit)]));

        // Assert
        unmarked.ShouldBe(["KitPartDto.Name"]);
    }

    private static HashSet<Type> RequestDtos(IEnumerable<Type> requests)
    {
        var dtos = new HashSet<Type>();
        var owners = new Stack<Type>(requests);
        while (owners.TryPop(out var owner))
        {
            foreach (var property in owner.GetProperties())
            {
                var type = ListElement(property.PropertyType) ?? property.PropertyType;
                if (type.IsClass && type != typeof(string) && type.Assembly == owner.Assembly && dtos.Add(type))
                    owners.Push(type);
            }
        }

        return dtos;
    }

    private static Type? ListElement(Type type) => type == typeof(string)
        ? null
        : type.GetInterfaces().Append(type)
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()[0];

    private static List<string> UnmarkedStrings(IEnumerable<Type> dtos)
    {
        var nullability = new NullabilityInfoContext();
        return dtos
            .SelectMany(dto => dto.GetProperties().Select(property => (Dto: dto, Property: property)))
            .Where(item => item.Property.PropertyType == typeof(string)
                && item.Property.SetMethod is not null
                && nullability.Create(item.Property).WriteState == NullabilityState.NotNull
                && item.Property.GetCustomAttribute<RequiredFieldAttribute>() is null)
            .Select(item => $"{item.Dto.Name}.{item.Property.Name}")
            .ToList();
    }

    private sealed record AddOrder(OrderDto Dto) : IRequest<bool>;

    private sealed record AddLines(IReadOnlyList<LineDto> Lines) : IRequest<bool>;

    private sealed record OrderDto
    {
        [RequiredField]
        public string Number { get; init; } = default!;

        public AddressDto? ShipTo { get; init; }

        public IReadOnlyList<LineDto> Lines { get; init; } = [];
    }

    private sealed record AddressDto
    {
        [RequiredField]
        public string City { get; init; } = default!;
    }

    private sealed record LineDto
    {
        [RequiredField]
        public string Name { get; init; } = default!;

        public PartDto[] Parts { get; init; } = [];
    }

    private sealed record PartDto
    {
        [RequiredField]
        public string Name { get; init; } = default!;
    }

    private sealed record AddBatch(BatchDto Dto) : IRequest<bool>;

    private sealed record BatchDto
    {
        public LineList Lines { get; init; } = [];
    }

    private sealed class LineList : List<LineDto>;

    private sealed record AddNode(NodeDto Dto) : IRequest<bool>;

    private sealed class NodeDto
    {
        [RequiredField]
        public string Name { get; init; } = default!;

        public NodeDto? Owner { get; init; }

        public List<NodeDto> Children { get; } = [];
    }

    private sealed record AddKit(KitDto Dto) : IRequest<bool>;

    private sealed record KitDto
    {
        [RequiredField]
        public string Name { get; init; } = default!;

        public IReadOnlyList<KitPartDto> Parts { get; init; } = [];
    }

    private sealed record KitPartDto
    {
        public string Name { get; init; } = default!;

        public string? Note { get; init; }
    }

    private sealed record GetDueList(DueParametersDto Parameters) : IRequest<bool>;

    private sealed record GetOverdueList(OverdueParametersDto Parameters) : IRequest<bool>;

    private sealed class DueParametersDto : BasePaginationParameters
    {
        [Range(1, 365)]
        public int WithinDays { get; set; } = 30;

        public static DueParametersDto From(int? pageNumber, int? pageSize, int? withinDays)
        {
            var parameters = new DueParametersDto { WithinDays = withinDays ?? 30 };
            parameters.SetPage(pageNumber, pageSize);
            return parameters;
        }
    }

    // Same property name as DueParametersDto, with a different range
    private sealed class OverdueParametersDto : BasePaginationParameters
    {
        [Range(1, 90)]
        public int WithinDays { get; set; } = 30;

        public static OverdueParametersDto From(int? pageNumber, int? pageSize, int? withinDays)
        {
            var parameters = new OverdueParametersDto { WithinDays = withinDays ?? 30 };
            parameters.SetPage(pageNumber, pageSize);
            return parameters;
        }
    }

    private static class GetDueListEndpoint
    {
        public static async Task<bool> Handle(
            [Range(1, BasePaginationParameters.MaxPageNumber)] int? pageNumber,
            [Range(1, int.MaxValue)] int? pageSize,
            [Range(1, 365)] int? withinDays,
            IMediator mediator,
            CancellationToken cancellationToken)
            => await mediator.Send(new GetDueList(DueParametersDto.From(pageNumber, pageSize, withinDays)), cancellationToken);
    }

    private static class GetOverdueListEndpoint
    {
        public static async Task<bool> Handle([Range(1, 90)] int? withinDays, IMediator mediator, CancellationToken cancellationToken)
            => await mediator.Send(new GetOverdueList(OverdueParametersDto.From(null, null, withinDays)), cancellationToken);
    }

    private static class GetOverdueListWithTheDueRangeEndpoint
    {
        public static async Task<bool> Handle([Range(1, 365)] int? withinDays, IMediator mediator, CancellationToken cancellationToken)
            => await mediator.Send(new GetOverdueList(OverdueParametersDto.From(null, null, withinDays)), cancellationToken);
    }

    private static class GetDueListWithAnExtraRangeEndpoint
    {
        public static async Task<bool> Handle([Range(0, 10)] int? limit, IMediator mediator, CancellationToken cancellationToken)
            => await mediator.Send(new GetDueList(DueParametersDto.From(null, limit, null)), cancellationToken);
    }

    private static async Task<bool> Send<TResponse>(IRequest<TResponse> request)
    {
        var sent = false;
        await new RequestValidationBehavior<IRequest<TResponse>, TResponse>().Handle(request, _ =>
        {
            sent = true;
            return Task.FromResult(default(TResponse)!);
        }, CancellationToken.None);
        return sent;
    }
}
