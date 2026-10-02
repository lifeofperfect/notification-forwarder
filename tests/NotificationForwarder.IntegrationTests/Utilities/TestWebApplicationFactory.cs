using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NotificationForwarder.Application.Notifications.Common.Services.Implementation;
using NotificationForwarder.Infrastructure.Delivery.Services.Implementation;
using NotificationForwarder.Infrastructure.Generation.Services.Implementation;

namespace NotificationForwarder.IntegrationTests.Utilities;

/// <summary>
/// Boots the real application: routing, validation, the backlog, the worker, the rate gate and the AI fallback all run
/// as in production. By default the two process boundaries are stubbed, the AI provider (OpenAI) and the alert sender
/// (Discord). With <see cref="UseRealAdapters"/> the real OpenAI and Discord classes run too, and only the network
/// underneath them is faked. The clock is fake so the rate limit can be tested without waiting.
/// </summary>
public sealed class TestWebApplicationFactory(ITestOutputHelper output) : WebApplicationFactory<Program>
{
    public static readonly DateTimeOffset StartTime = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    public const string Route = "/apis/notification-forwarder/v1/notifications";

    public const string ApiKey = "test-only-not-a-real-key";

    public const string WebhookUrl = "https://discord.com/api/webhooks/1/abc";

    /// <summary>
    /// Stands in for OpenAI. The real <see cref="FallbackAlertGenerator"/> wraps it, so a stub that throws
    /// <see cref="AlertGenerationException"/> exercises the template fallback as in production.
    /// Answers with <see cref="StubAlert"/> unless a test reconfigures it. Ignored with <see cref="UseRealAdapters"/>.
    /// </summary>
    public IAlertGenerator AlertGenerator { get; } = CreateGeneratorStub();

    /// <summary>Stands in for Discord. Ignored with <see cref="UseRealAdapters"/>.</summary>
    public IAlertSender AlertSender { get; } = Substitute.For<IAlertSender>();

    /// <summary>The network under the real OpenAI adapter; only used with <see cref="UseRealAdapters"/>.</summary>
    public FakeHttpMessageHandler OpenAiHttp { get; init; } = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, OpenAiCompletion("Stubbed kind", "Stubbed title", "Stubbed message."));

    /// <summary>The network under the real Discord adapter; only used with <see cref="UseRealAdapters"/>.</summary>
    public FakeHttpMessageHandler DiscordHttp { get; init; } = FakeHttpMessageHandler.Returning(HttpStatusCode.OK);

    public FakeTimeProvider Clock { get; } = new(StartTime);

    /// <summary>Overridable per test class; the default matches production.</summary>
    public int QueueCapacity { get; init; } = 1000;

    /// <summary>Keep the real OpenAI and Discord adapters and fake only their HTTP transport.</summary>
    public bool UseRealAdapters { get; init; }

    public static GeneratedAlert StubAlert { get; } = GeneratedAlert.Create("Stubbed kind", "Stubbed title", "Stubbed message.");

    /// <summary>Every notification the stubbed sender has been asked to deliver, in order.</summary>
    public IReadOnlyList<NotificationEntity> SentNotifications =>
        AlertSender.ReceivedCalls().Select(call => (NotificationEntity)call.GetArguments()[0]!).ToList();

    public int GeneratorCalls => AlertGenerator.ReceivedCalls().Count();

    public static string OpenAiCompletion(string kind, string title, string message) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    finish_reason = "stop",
                    message = new { role = "assistant", content = System.Text.Json.JsonSerializer.Serialize(new { kind, title, message }) }
                }
            }
        });

    /// <summary>Polls in real time, since the worker runs on its own thread, until the condition holds or ten seconds pass.</summary>
    public static async Task<bool> WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(20, cancellationToken);
        }

        return condition();
    }

    public Task<bool> WaitForSendsAsync(int expected, CancellationToken cancellationToken) =>
        WaitUntilAsync(() => SentNotifications.Count >= expected, cancellationToken);

    private static IAlertGenerator CreateGeneratorStub()
    {
        var generator = Substitute.For<IAlertGenerator>();
        generator.GenerateAsync(Arg.Any<NotificationEntity>(), Arg.Any<CancellationToken>()).Returns(StubAlert);
        return generator;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Production);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Queue:Capacity"] = QueueCapacity.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Ai:Enabled"] = UseRealAdapters ? "true" : "false",
            ["Ai:ApiKey"] = ApiKey,
            ["Ai:Model"] = "test-model",
            ["Discord:Enabled"] = UseRealAdapters ? "true" : "false",
            ["Discord:WebhookUrl"] = WebhookUrl
        }));
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(new XUnitLoggerProvider(output));
        });
        builder.ConfigureTestServices(services =>
        {
            services.ReplaceSingleton<TimeProvider>(Clock);
            if (UseRealAdapters)
            {
                services.AddHttpClient(OpenAiAlertGenerator.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => OpenAiHttp);
                services.AddHttpClient(nameof(DiscordWebhookAlertSender)).ConfigurePrimaryHttpMessageHandler(() => DiscordHttp);
                return;
            }

            services.ReplaceSingleton<IAlertGenerator>(
                new FallbackAlertGenerator(AlertGenerator, new TemplateAlertGenerator(), NullLogger<FallbackAlertGenerator>.Instance));
            services.ReplaceSingleton(AlertSender);
        });
    }
}
