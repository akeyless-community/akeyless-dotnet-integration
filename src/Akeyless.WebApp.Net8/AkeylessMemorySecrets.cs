using System.Collections.ObjectModel;
using akeyless.Api;
using akeyless.Model;
using AkeylessClientConfiguration = akeyless.Client.Configuration;

namespace Akeyless.WebApp.Net8;

/// <summary>
/// PRD-aligned: discover <c>akeyless://</c> in configuration + env, fetch from Gateway, keep values in memory only.
/// </summary>
public sealed class AkeylessMemorySecrets : IDisposable
{
    private readonly ILogger<AkeylessMemorySecrets> _logger;
    private readonly object _gate = new();
    private System.Threading.Timer? _refreshTimer;
    private IReadOnlyDictionary<string, string> _secrets =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    public AkeylessMemorySecrets(ILogger<AkeylessMemorySecrets> logger)
    {
        _logger = logger;
    }

    public int LoadedCount
    {
        get
        {
            lock (_gate)
            {
                return _secrets.Count;
            }
        }
    }

    /// <summary>PRD-style accessor by logical configuration key (case-insensitive).</summary>
    public string Get(string name)
    {
        if (!TryGet(name, out var value) || value is null)
        {
            throw new KeyNotFoundException("No secret loaded for logical key: " + name);
        }

        return value;
    }

    public string GetRequired(string name) => Get(name);

    public bool TryGet(string name, out string? value)
    {
        lock (_gate)
        {
            return _secrets.TryGetValue(name, out value);
        }
    }

    /// <summary>
    /// Loads secrets using <paramref name="configuration"/> (appsettings, env, command line) plus <c>AKEYLESS_SECRET_NAMES</c>.
    /// </summary>
    public void LoadFromAkeyless(IConfiguration configuration)
    {
        lock (_gate)
        {
            if (!TryLoadAndApply(configuration, isInitialLoad: true, out var ex))
            {
                throw ex ?? new InvalidOperationException("Akeyless bootstrap failed.");
            }

            StartOrRestartRefreshTimer(configuration);
        }
    }

    /// <summary>On failure, existing cache is retained.</summary>
    public bool TryRefreshSecrets(IConfiguration configuration, out Exception? error)
    {
        lock (_gate)
        {
            return TryLoadAndApply(configuration, isInitialLoad: false, out error);
        }
    }

    public void Dispose()
    {
        _refreshTimer?.Dispose();
    }

    private void StartOrRestartRefreshTimer(IConfiguration configuration)
    {
        _refreshTimer?.Dispose();
        _refreshTimer = null;

        var ttl = ParseCacheTtlSeconds();
        if (ttl <= 0)
        {
            return;
        }

        var period = TimeSpan.FromSeconds(ttl);
        _refreshTimer = new System.Threading.Timer(
            _ =>
            {
                try
                {
                    TryRefreshSecrets(configuration, out Exception? _);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Scheduled Akeyless refresh failed.");
                }
            },
            null,
            period,
            period);

        _logger.LogInformation("Akeyless cache TTL refresh enabled, intervalSeconds={Seconds}", ttl);
    }

    private static int ParseCacheTtlSeconds()
    {
        var raw = Environment.GetEnvironmentVariable("AKEYLESS_CACHE_TTL_SECONDS");
        if (string.IsNullOrWhiteSpace(raw) || !int.TryParse(raw.Trim(), out var seconds))
        {
            return 0;
        }

        return seconds < 0 ? 0 : seconds;
    }

    private bool TryLoadAndApply(IConfiguration configuration, bool isInitialLoad, out Exception? error)
    {
        error = null;
        try
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
                if (isInitialLoad)
                {
                    throw new InvalidOperationException(
                        "No secret references found. Use akeyless:// in configuration or set AKEYLESS_SECRET_NAMES.");
                }

                _logger.LogWarning("Akeyless refresh skipped: no bindings discovered.");
                return true;
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
                    "Set AKEYLESS_ACCESS_ID and AKEYLESS_ACCESS_KEY in the environment.");
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

            _secrets = new ReadOnlyDictionary<string, string>(logical);
            _logger.LogInformation("Akeyless secrets loaded into memory, count={Count}.", _secrets.Count);
            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            if (isInitialLoad)
            {
                _logger.LogError(ex, "Initial Akeyless load failed.");
            }
            else
            {
                _logger.LogWarning(ex, "Akeyless refresh failed; retaining previous cache.");
            }

            return false;
        }
    }
}
