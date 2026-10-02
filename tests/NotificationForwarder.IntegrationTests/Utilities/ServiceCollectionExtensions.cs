using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NotificationForwarder.IntegrationTests.Utilities;

internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// Replaces every registration of <typeparamref name="TService"/> with one instance. Throws when production never
    /// registered the service, so a test cannot stub something the application does not actually use.
    /// </summary>
    public static IServiceCollection ReplaceSingleton<TService>(this IServiceCollection services, TService implementation)
        where TService : class
    {
        if (services.All(descriptor => descriptor.ServiceType != typeof(TService)))
        {
            throw new InvalidOperationException($"No service of type '{typeof(TService).Name}' is registered. Did you forget to register the production dependency?");
        }

        services.RemoveAll<TService>();
        return services.AddSingleton(implementation);
    }
}
