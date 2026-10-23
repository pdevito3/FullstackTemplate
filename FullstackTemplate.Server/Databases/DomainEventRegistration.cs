namespace FullstackTemplate.Server.Databases;

using System.Reflection;
using Domain;

public static class DomainEventRegistration
{
    public static IServiceCollection AddDomainEvents(this IServiceCollection services, Assembly assembly)
    {
        services.AddScoped<DomainEventDispatcher>();
        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(IDomainEventHandler<>)), publicOnly: false)
                .AsImplementedInterfaces(type => type.IsGenericType
                                                 && type.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))
                .WithScopedLifetime());
        return services;
    }
}
