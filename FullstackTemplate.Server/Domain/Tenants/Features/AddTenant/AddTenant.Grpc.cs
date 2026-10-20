namespace FullstackTemplate.Server.Domain.Tenants.Grpc.V1;

// "Grpc" alone would bind to the Grpc segment of this namespace, so the gRPC library needs the global alias.
using global::Grpc.Core;
using Dtos;
using Features;

public sealed partial class TenantsGrpcService
{
    public override async Task<AddTenantResponse> AddTenant(AddTenantRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(
            new AddTenant.Command(new TenantForCreationDto { Name = request.Name }),
            context.CancellationToken);
        return new AddTenantResponse { Tenant = result.ToMessage() };
    }
}
