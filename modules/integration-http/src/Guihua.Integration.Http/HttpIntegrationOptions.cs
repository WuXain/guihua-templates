using Microsoft.Extensions.Options;

namespace Guihua.Integration.Http;

public sealed class HttpIntegrationOptions
{
    public const string SectionName = "IntegrationHttp";

    public Uri? BaseAddress { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    public bool EnableResilience { get; set; }

    public int MaxRetries { get; set; } = 2;

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);
}

internal sealed class HttpIntegrationOptionsValidator : IValidateOptions<HttpIntegrationOptions>
{
    public ValidateOptionsResult Validate(string? name, HttpIntegrationOptions options)
    {
        var failures = new List<string>();

        if (options.BaseAddress is null || !options.BaseAddress.IsAbsoluteUri)
        {
            failures.Add($"{nameof(HttpIntegrationOptions.BaseAddress)} must be an absolute URI.");
        }
        else if (options.BaseAddress.Scheme != Uri.UriSchemeHttp
            && options.BaseAddress.Scheme != Uri.UriSchemeHttps)
        {
            failures.Add($"{nameof(HttpIntegrationOptions.BaseAddress)} must use http or https.");
        }

        if (options.Timeout <= TimeSpan.Zero || options.Timeout == Timeout.InfiniteTimeSpan)
        {
            failures.Add($"{nameof(HttpIntegrationOptions.Timeout)} must be greater than zero.");
        }

        if (options.EnableResilience && options.MaxRetries < 0)
        {
            failures.Add($"{nameof(HttpIntegrationOptions.MaxRetries)} cannot be negative.");
        }

        if (options.EnableResilience && options.RetryDelay < TimeSpan.Zero)
        {
            failures.Add($"{nameof(HttpIntegrationOptions.RetryDelay)} cannot be negative.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
