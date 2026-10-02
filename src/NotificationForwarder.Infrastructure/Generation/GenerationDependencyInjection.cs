using NotificationForwarder.Application.Notifications.Common.Services.Implementation;
using NotificationForwarder.Infrastructure.Generation.Services.Implementation;

namespace NotificationForwarder.Infrastructure.Generation;

public static class GenerationDependencyInjection
{
    public static IServiceCollection AddGeneration(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<AiOptions>().Bind(configuration.GetSection(AiOptions.SectionName)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<AiOptions>, AiOptionsValidator>();

        services.AddTransient<OpenAiResponseHandler>();
        services.AddHttpClient(OpenAiAlertGenerator.HttpClientName, (provider, client) =>
        {
            client.BaseAddress = provider.GetRequiredService<IOptions<AiOptions>>().Value.BaseUrl;
            client.Timeout = Timeout.InfiniteTimeSpan; // the generator applies its own deadline
            client.MaxResponseContentBufferSize = 64 * 1024;
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        })
        .AddHttpMessageHandler<OpenAiResponseHandler>();
        services.AddScoped<OpenAiAlertGenerator>();

        // Chosen per resolution so that configuration supplied late (user secrets, test hosts) is honoured.
        // With Ai enabled, OpenAI is wrapped so any expected failure degrades to the template instead of losing the alert.
        return services.AddScoped<IAlertGenerator>(provider =>
            provider.GetRequiredService<IOptions<AiOptions>>().Value.Enabled
                ? new FallbackAlertGenerator(
                    provider.GetRequiredService<OpenAiAlertGenerator>(),
                    provider.GetRequiredService<TemplateAlertGenerator>(),
                    provider.GetRequiredService<ILogger<FallbackAlertGenerator>>())
                : provider.GetRequiredService<TemplateAlertGenerator>());
    }
}
