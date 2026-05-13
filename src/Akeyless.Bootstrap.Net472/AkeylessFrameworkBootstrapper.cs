using System;
using System.Collections.Generic;
using System.Linq;

namespace Akeyless.Bootstrap
{
    /// <summary>
    /// Loads secrets from Akeyless at startup (PRD: discovery from XML + env), keeps them in memory, optional TTL refresh.
    /// </summary>
    public static class AkeylessFrameworkBootstrapper
    {
        private static readonly object LoadLock = new object();
        private static System.Threading.Timer _refreshTimer;

        /// <summary>
        /// Full load: discover <c>akeyless://</c> references, authenticate, fetch, populate <see cref="AppSecrets"/>,
        /// start optional cache refresh timer (<c>AKEYLESS_CACHE_TTL_SECONDS</c>).
        /// </summary>
        public static void LoadSecretsAtStartup()
        {
            lock (LoadLock)
            {
                if (!TryLoadAndApply(isInitialLoad: true, out var ex))
                {
                    throw ex ?? new InvalidOperationException("Akeyless bootstrap failed.");
                }

                StartOrRestartRefreshTimer();
            }
        }

        /// <summary>
        /// Re-runs discovery and fetch (e.g. for rotation). On failure, previous cache is left intact.
        /// </summary>
        public static bool TryRefreshSecrets(out Exception error)
        {
            lock (LoadLock)
            {
                return TryLoadAndApply(isInitialLoad: false, out error);
            }
        }

        private static void StartOrRestartRefreshTimer()
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
                        TryRefreshSecrets(out Exception _);
                    }
                    catch (Exception ex)
                    {
                        AkeylessIntegrationLog.Error("Scheduled Akeyless refresh failed.", ex);
                    }
                },
                null,
                period,
                period);

            AkeylessIntegrationLog.Info("Secret cache TTL refresh enabled, intervalSeconds=" + ttl);
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

        private static bool TryLoadAndApply(bool isInitialLoad, out Exception error)
        {
            error = null;
            try
            {
                var bindings = ConfigurationSecretDiscovery.Discover().ToList();
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
                            "No secret references found. Use akeyless:// in web.config/appSettings, connectionStrings, " +
                            "environment variable values, or set AKEYLESS_SECRET_NAMES.");
                    }

                    AkeylessIntegrationLog.Warning("Refresh skipped: no secret references discovered.");
                    return true;
                }

                var uniquePaths = bindings
                    .Select(b => b.SecretPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

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

                var pathToValue = SecretPathResolver.FetchPathToValues(uniquePaths);

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

                AppSecrets.ReplaceAll(logical);
                AkeylessIntegrationLog.Info("Akeyless secrets loaded into memory, count=" + logical.Count + ".");
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                if (isInitialLoad)
                {
                    AkeylessIntegrationLog.Error("Initial Akeyless load failed.", ex);
                }
                else
                {
                    AkeylessIntegrationLog.Error("Akeyless refresh failed; retaining previous cache.", ex);
                }

                return false;
            }
        }
    }
}
