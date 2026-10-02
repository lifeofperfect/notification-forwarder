using System.Net;

namespace NotificationForwarder.Infrastructure.Generation;

public sealed class AiOptionsValidator : IValidateOptions<AiOptions>
{
    public ValidateOptionsResult Validate(string? name, AiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        List<string> failures = [];
        if (string.IsNullOrWhiteSpace(options.ApiKey) || options.ApiKey.Length > 512
            || options.ApiKey.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            failures.Add("Ai:ApiKey must be a non-blank credential without whitespace or control characters.");
        }

        if (string.IsNullOrWhiteSpace(options.Model) || options.Model.Length > 100
            || options.Model.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_' or '.')))
        {
            failures.Add("Ai:Model must be an OpenAI model ID of at most 100 ASCII letters, digits, dots, underscores or hyphens.");
        }

        if (!options.BaseUrl.IsAbsoluteUri || !options.BaseUrl.AbsoluteUri.EndsWith('/'))
        {
            failures.Add("Ai:BaseUrl must be an absolute URL ending with a slash.");
        }
        else if (options.BaseUrl.Scheme != "https" && !(options.BaseUrl.Scheme == "http" && IsLoopback(options.BaseUrl)))
        {
            failures.Add("Ai:BaseUrl must use https; plain http is allowed only for loopback addresses, so the key is never sent in clear.");
        }

        if (options.RequestTimeout < TimeSpan.FromMilliseconds(100) || options.RequestTimeout > TimeSpan.FromSeconds(60))
        {
            failures.Add("Ai:RequestTimeout must be between 100 milliseconds and 60 seconds.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsLoopback(Uri url) =>
        url.IsLoopback || (IPAddress.TryParse(url.Host, out var address) && IPAddress.IsLoopback(address));
}
