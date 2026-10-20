namespace FullstackTemplate.Server.Domain.Tenants.Grpc.V1;

// "Grpc" alone would bind to the Grpc segment of this namespace, so the gRPC library needs the global alias.
using global::Grpc.Core;
using Dtos;
using Features;

public sealed partial class TenantsGrpcService
{
    public override async Task<ListTenantsResponse> ListTenants(ListTenantsRequest request, ServerCallContext context)
    {
        var parameters = TenantParametersDto.From(
            request.HasPageNumber ? request.PageNumber : null,
            request.HasPageSize ? request.PageSize : null,
            request.HasFilters ? request.Filters : null,
            request.HasSortOrder ? request.SortOrder : null);

        var result = await mediator.Send(
            new GetTenantList.Query(parameters),
            context.CancellationToken);

        var response = new ListTenantsResponse { Pagination = result.ToPaginationMessage() };
        response.Tenants.AddRange(result.Select(tenant => tenant.ToMessage()));
        return response;
    }
}
