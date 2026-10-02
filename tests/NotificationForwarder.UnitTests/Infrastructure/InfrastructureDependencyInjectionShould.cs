using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationForwarder.Application;
using NotificationForwarder.Infrastructure;
using NotificationForwarder.Infrastructure.Delivery.Services.Implementation;

namespace NotificationForwarder.UnitTests.Infrastructure;

/// <summary>The provider switches are the one piece of wiring the component tests bypass, so they are pinned here.</summary>
public sealed class InfrastructureDependencyInjectionShould
{
    private static ServiceProvider Build(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(setting => setting.Key, setting => (string?)setting.Value))
            .Build();
        var services = new ServiceCollection().AddLogging().AddSingleton(TimeProvider.System);
        services.AddApplication().AddInfrastructure(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public void UseTheTemplateGeneratorAndTheLogSenderWhenProvidersAreDisabled()
    {
        using var provider = Build(("Ai:Enabled", "false"), ("Discord:Enabled", "false"));
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IAlertGenerator>().ShouldBeOfType<TemplateAlertGenerator>();
        scope.ServiceProvider.GetRequiredService<IAlertSender>().ShouldBeOfType<LoggingAlertSender>();
    }

    [Fact]
    public void WrapOpenAiInTheFallbackAndUseDiscordWhenProvidersAreEnabled()
    {
        using var provider = Build(
            ("Ai:Enabled", "true"), ("Ai:ApiKey", "test-only"), ("Ai:Model", "test-model"),
            ("Discord:Enabled", "true"), ("Discord:WebhookUrl", "https://discord.com/api/webhooks/1/abc"));
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IAlertGenerator>().ShouldBeOfType<FallbackAlertGenerator>();
        scope.ServiceProvider.GetRequiredService<IAlertSender>().ShouldBeOfType<DiscordWebhookAlertSender>();
    }
}
