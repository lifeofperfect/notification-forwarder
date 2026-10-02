using System.Net;

namespace NotificationForwarder.Infrastructure.Delivery;

public sealed class DiscordOptionsValidator : IValidateOptions<DiscordOptions>
{
    public ValidateOptionsResult Validate(string? name, DiscordOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        List<string> failures = [];
        if (options.WebhookUrl is null || !options.WebhookUrl.IsAbsoluteUri)
        {
            failures.Add("Discord:WebhookUrl must be an absolute URL when Discord is enabled.");
        }
        else if (options.WebhookUrl.Scheme != "https" && !(options.WebhookUrl.Scheme == "http" && IsLoopback(options.WebhookUrl)))
        {
            failures.Add("Discord:WebhookUrl must use https; plain http is allowed only for loopback addresses.");
        }

        if (string.IsNullOrWhiteSpace(options.Username) || options.Username.Length > 80)
        {
            failures.Add("Discord:Username must be between 1 and 80 characters.");
        }

        if (options.RequestTimeout < TimeSpan.FromMilliseconds(100) || options.RequestTimeout > TimeSpan.FromSeconds(60))
        {
            failures.Add("Discord:RequestTimeout must be between 100 milliseconds and 60 seconds.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsLoopback(Uri url) =>
        url.IsLoopback || (IPAddress.TryParse(url.Host, out var address) && IPAddress.IsLoopback(address));
}
