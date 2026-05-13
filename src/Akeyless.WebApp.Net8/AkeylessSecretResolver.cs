using akeyless.Api;
using akeyless.Model;
using AkeylessClientConfiguration = akeyless.Client.Configuration;

namespace Akeyless.WebApp.Net8;

/// <summary>
/// Fetches secrets from the Gateway for all <c>akeyless://</c> bindings discovered in configuration.
/// </summary>
public static class AkeylessSecretResolver
{
    /// <summary>
    /// Returns a flat dictionary of configuration keys to resolved secret values (suitable for <see cref="ConfigurationManager.AddInMemoryCollection"/>).
    /// </summary>
    public static Dictionary<string, string> Resolve(IConfiguration configuration, ILogger? logger = null)
    {
        var bindings = ConfigurationSecretDiscovery.DiscoverFromConfiguration(configuration).ToList();
        foreach (var extra in ConfigurationSecretDiscovery.FromEnvironmentSecretList(
                     Environment.GetEnvironmentVariable("AKEYLESS_SECRET_NAMES")))
        {
            if (bindings.Any(b => string.Equals(b.LogicalKey, extra.LogicalKey, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            bindings.Add(extra);
        }

        if (bindings.Count == 0)
        {
            logger?.LogInformation("Akeyless: no akeyless:// bindings found; skipping Gateway call.");
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var gatewayUrl = Environment.GetEnvironmentVariable("AKEYLESS_GW_URL");
        if (string.IsNullOrWhiteSpace(gatewayUrl))
        {
            gatewayUrl = "https://api.akeyless.io";
        }

        var accessId = Environment.GetEnvironmentVariable("AKEYLESS_ACCESS_ID");
        var accessKey = Environment.GetEnvironmentVariable("AKEYLESS_ACCESS_KEY");
        if (string.IsNullOrEmpty(accessId) || string.IsNullOrEmpty(accessKey))
        {
            throw new InvalidOperationException(
                "Set AKEYLESS_ACCESS_ID and AKEYLESS_ACCESS_KEY in the environment when using akeyless:// references.");
        }

        var uniquePaths = bindings
            .Select(b => b.SecretPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var cfg = new AkeylessClientConfiguration { BasePath = gatewayUrl };
        var api = new V2Api(cfg);

        var authResult = api.Auth(new Auth(accessId: accessId, accessKey: accessKey));
        if (authResult?.Token is not { Length: > 0 })
        {
            throw new InvalidOperationException("Akeyless authentication did not return a token.");
        }

        var body = new GetSecretValue(names: uniquePaths, token: authResult.Token);
        var raw = api.GetSecretValue(body) ?? new Dictionary<string, object>();

        var pathToValue = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in raw)
        {
            if (string.IsNullOrEmpty(kv.Key))
            {
                continue;
            }

            var normalizedKey = kv.Key.StartsWith("/", StringComparison.Ordinal) ? kv.Key : "/" + kv.Key;
            pathToValue[normalizedKey] = kv.Value?.ToString() ?? string.Empty;
        }

        var logical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var bind in bindings)
        {
            if (!pathToValue.TryGetValue(bind.SecretPath, out var secretValue))
            {
                throw new InvalidOperationException(
                    "Gateway did not return secret for path: " + bind.SecretPath + " (logical key: " + bind.LogicalKey + ").");
            }

            logical[bind.LogicalKey] = secretValue;
        }

        logger?.LogInformation("Akeyless: merged {Count} resolved secret(s) into configuration.", logical.Count);
        return logical;
    }
}
