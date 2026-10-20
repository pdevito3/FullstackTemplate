namespace FullstackTemplate.Server.Domain.Tenants.Grpc.V1;

using MediatR;
using Microsoft.AspNetCore.Authorization;

/// <summary>
/// The gRPC v1 adapter of the tenant features. Each RPC is a partial method in the folder of its feature, for
/// example <c>Features/GetTenant/GetTenant.Grpc.cs</c>. Each RPC maps the message to a DTO, sends the same MediatR
/// request as the HTTP endpoint, and maps the DTO back. <c>DomainErrorInterceptor</c> maps the domain exceptions
/// to gRPC status codes.
/// </summary>
[Authorize]
public sealed partial class TenantsGrpcService(IMediator mediator) : TenantsService.TenantsServiceBase;
