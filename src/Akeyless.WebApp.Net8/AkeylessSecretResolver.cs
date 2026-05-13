namespace Akeyless.WebApp.Net8;

/// <summary>
/// Fetches secrets from the local agent or Gateway for all <c>akeyless://</c> bindings discovered in configuration.
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
            logger?.LogInformation("Akeyless: no akeyless:// bindings found; skipping resolution.");
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var agentUrl = Environment.GetEnvironmentVariable("AKEYLESS_AGENT_URL");
        if (string.IsNullOrWhiteSpace(agentUrl))
        {
            var accessId = Environment.GetEnvironmentVariable("AKEYLESS_ACCESS_ID");
            var accessKey = Environment.GetEnvironmentVariable("AKEYLESS_ACCESS_KEY");
            if (string.IsNullOrEmpty(accessId) || string.IsNullOrEmpty(accessKey))
            {
                throw new InvalidOperationException(
                    "Set AKEYLESS_AGENT_URL for the local agent, or set AKEYLESS_ACCESS_ID and AKEYLESS_ACCESS_KEY for direct Gateway access.");
            }
        }

        var uniquePaths = bindings
            .Select(b => b.SecretPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var pathToValue = SecretPathResolver.FetchPathToValues(uniquePaths);

        var logical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var bind in bindings)
        {
            if (!pathToValue.TryGetValue(bind.SecretPath, out var secretValue))
            {
                throw new InvalidOperationException(
                    "Secret resolution did not return value for path: " + bind.SecretPath + " (logical key: " + bind.LogicalKey + ").");
            }

            logical[bind.LogicalKey] = secretValue;
        }

        logger?.LogInformation("Akeyless: merged {Count} resolved secret(s) into configuration.", logical.Count);
        return logical;
    }
}
