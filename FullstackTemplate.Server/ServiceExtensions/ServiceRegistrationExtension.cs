using System.Reflection;

namespace FullstackTemplate.Server.ServiceExtensions;

/// <summary>
/// Marker interface for singleton lifetime (overrides default scoped).
/// </summary>
public interface ISingletonService;

/// <summary>
/// Marker interface for transient lifetime (overrides default scoped).
/// </summary>
public interface ITransientService;

public static class ServiceRegistrationExtension
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, Assembly? assembly = null)
    {
        assembly ??= Assembly.GetExecutingAssembly();

        services.Scan(scan => scan
            .FromAssemblies(assembly)
            // Explicit singleton registration
            .AddClasses(classes => classes.AssignableTo<ISingletonService>())
                .AsMatchingInterface()
                .WithSingletonLifetime()
            // Explicit transient registration
            .AddClasses(classes => classes.AssignableTo<ITransientService>())
                .AsMatchingInterface()
                .WithTransientLifetime()
            // Default: match IMyService to MyService as scoped
            .AddClasses(classes => classes
                .Where(type => !typeof(ISingletonService).IsAssignableFrom(type)
                            && !typeof(ITransientService).IsAssignableFrom(type)))
                .AsMatchingInterface()
                .WithScopedLifetime());

        return services;
    }
}
