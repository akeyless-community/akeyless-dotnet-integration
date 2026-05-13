using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Akeyless.WebApp.Net8;

/// <summary>
/// Enriches <see cref="ConfigurationManager"/> so <see cref="IConfiguration"/> consumers see resolved secret values
/// instead of <c>akeyless://</c> placeholders—no separate secret API in application code.
/// </summary>
public static class AkeylessConfigurationExtensions
{
    /// <summary>
    /// Discovers <c>akeyless://</c> values, fetches from the Gateway, and adds an in-memory layer that overrides those keys.
    /// Call once at startup, immediately after <see cref="WebApplication.CreateBuilder(string[])"/>.
    /// </summary>
    public static ConfigurationManager AddAkeylessResolvedSecrets(
        this ConfigurationManager configuration,
        ILogger? logger = null)
    {
        var resolved = AkeylessSecretResolver.Resolve(configuration, logger);
        if (resolved.Count > 0)
        {
            var nullablePairs = resolved.Select(kv => new KeyValuePair<string, string?>(kv.Key, kv.Value));
            configuration.AddInMemoryCollection(nullablePairs);
        }

        return configuration;
    }
}
