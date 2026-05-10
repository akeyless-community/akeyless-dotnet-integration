namespace Akeyless.WebApp.Net8;

/// <summary>
/// Discovers <c>akeyless://</c> entries from <see cref="IConfiguration"/> (e.g. appsettings.json) plus environment fallbacks.
/// </summary>
public static class ConfigurationSecretDiscovery
{
    public sealed record SecretBinding(string LogicalKey, string SecretPath);

    /// <summary>
    /// Walks configuration keys; leaf values that are <c>akeyless://</c> references become bindings. Logical keys use ':' segments (ASP.NET Core convention).
    /// </summary>
    public static IReadOnlyList<SecretBinding> DiscoverFromConfiguration(IConfiguration configuration)
    {
        var ordered = new List<SecretBinding>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void TryAdd(string logicalKey, string? value)
        {
            if (string.IsNullOrEmpty(logicalKey) || !SecretReferenceParser.TryParsePath(value, out var path))
            {
                return;
            }

            if (!seen.Add(logicalKey))
            {
                return;
            }

            ordered.Add(new SecretBinding(logicalKey, path));
        }

        void Walk(IConfiguration config, string prefix)
        {
            foreach (var child in config.GetChildren())
            {
                var keyPath = string.IsNullOrEmpty(prefix)
                    ? child.Key
                    : prefix + ConfigurationPath.KeyDelimiter + child.Key;
                if (child.Value != null)
                {
                    TryAdd(keyPath, child.Value);
                }
                else
                {
                    Walk(child, keyPath);
                }
            }
        }

        Walk(configuration, string.Empty);

        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var name = entry.Key as string;
            var value = entry.Value as string;
            if (string.IsNullOrEmpty(name) || seen.Contains(name))
            {
                continue;
            }

            TryAdd(name, value);
        }

        return ordered;
    }

    public static IReadOnlyList<SecretBinding> FromEnvironmentSecretList(string? rawList)
    {
        if (string.IsNullOrWhiteSpace(rawList))
        {
            return Array.Empty<SecretBinding>();
        }

        return rawList
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Select(s => new SecretBinding(s, s.StartsWith('/') ? s : "/" + s))
            .ToList();
    }
}
