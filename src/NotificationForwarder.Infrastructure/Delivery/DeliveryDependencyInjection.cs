using NotificationForwarder.Infrastructure.Delivery.Services.Implementation;

namespace NotificationForwarder.Infrastructure.Delivery;

public static class DeliveryDependencyInjection
{
    public static IServiceCollection AddDelivery(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<DiscordOptions>().Bind(configuration.GetSection(DiscordOptions.SectionName)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<DiscordOptions>, DiscordOptionsValidator>();

        services.AddHttpClient<DiscordWebhookAlertSender>(client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan; // the sender applies its own deadline
            client.MaxResponseContentBufferSize = 16 * 1024;
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        });
        services.AddSingleton<LoggingAlertSender>();

        // Chosen per resolution so that configuration supplied late (user secrets, test hosts) is honoured.
        return services.AddScoped<IAlertSender>(provider =>
            provider.GetRequiredService<IOptions<DiscordOptions>>().Value.Enabled
                ? provider.GetRequiredService<DiscordWebhookAlertSender>()
                : provider.GetRequiredService<LoggingAlertSender>());
    }
}
