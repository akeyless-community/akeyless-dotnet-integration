using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Akeyless.WebApp.Net8;

/// <summary>
/// Enriches configuration during host setup so <see cref="IConfiguration"/> consumers see resolved values.
/// </summary>
public static class AkeylessConfigurationBuilderExtensions
{
    /// <summary>
    /// Builds the current configuration, resolves all <c>akeyless://</c> values, and adds them as an in-memory layer.
    /// Use in <c>ConfigureAppConfiguration</c> when not using <see cref="AkeylessConfigurationExtensions.AddAkeylessResolvedSecrets"/>.
    /// </summary>
    public static IConfigurationBuilder AddAkeylessResolvedSecrets(
        this IConfigurationBuilder configurationBuilder,
        ILogger? logger = null)
    {
        var interim = configurationBuilder.Build();
        var resolved = AkeylessSecretResolver.Resolve(interim, logger);
        if (resolved.Count > 0)
        {
            var nullablePairs = resolved.Select(kv => new KeyValuePair<string, string?>(kv.Key, kv.Value));
            configurationBuilder.AddInMemoryCollection(nullablePairs);
        }

        return configurationBuilder;
    }
}
