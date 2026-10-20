namespace FullstackTemplate.Server.Domain.Tenants.Grpc.V1;

// "Grpc" alone would bind to the Grpc segment of this namespace, so the gRPC library needs the global alias.
using global::Grpc.Core;
using Features;

public sealed partial class TenantsGrpcService
{
    public override async Task<GetTenantResponse> GetTenant(GetTenantRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(new GetTenant.Query(request.Id), context.CancellationToken);
        return new GetTenantResponse { Tenant = result.ToMessage() };
    }
}
