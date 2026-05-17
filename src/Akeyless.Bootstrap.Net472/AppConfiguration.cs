using System;
using System.Collections.Generic;
using System.Configuration;

namespace Akeyless.Bootstrap
{
    /// <summary>
    /// Unified configuration access after <see cref="AkeylessFrameworkBootstrapper.EnrichConfigurationAtStartup"/>.
    /// Returns resolved values from <see cref="ConfigurationManager"/> when enrichment succeeded, otherwise
    /// the in-memory overlay populated at startup. Callers do not need to know whether a value came from Akeyless.
    /// </summary>
    public static class AppConfiguration
    {
        private const string ConnectionStringsPrefix = "ConnectionStrings:";

        public static string GetAppSetting(string key)
        {
            if (!TryGetAppSetting(key, out var value))
            {
                throw new KeyNotFoundException("No configuration value for app setting key: " + key);
            }

            return value;
        }

        public static string GetConnectionString(string name)
        {
            if (!TryGetConnectionString(name, out var value))
            {
                throw new KeyNotFoundException("No connection string for name: " + name);
            }

            return value;
        }

        public static string Get(string name)
        {
            if (!TryGet(name, out var value))
            {
                throw new KeyNotFoundException("No configuration value for key: " + name);
            }

            return value;
        }

        /// <summary>
        /// Reads <see cref="ConfigurationManager.AppSettings"/> when it contains a resolved value;
        /// otherwise uses the startup overlay for keys that could not be patched in-place.
        /// </summary>
        public static bool TryGetAppSetting(string key, out string value)
        {
            if (TryGetResolvedFromConfigurationManager(ConfigurationManager.AppSettings[key], out value))
            {
                return true;
            }

            return AppSecrets.TryGet(key, out value);
        }

        /// <summary>
        /// Reads <see cref="ConfigurationManager.ConnectionStrings"/> when enriched; otherwise the startup overlay.
        /// </summary>
        public static bool TryGetConnectionString(string name, out string value)
        {
            var fromManager = ConfigurationManager.ConnectionStrings[name]?.ConnectionString;
            if (TryGetResolvedFromConfigurationManager(fromManager, out value))
            {
                return true;
            }

            return AppSecrets.TryGet(ConnectionStringsPrefix + name, out value);
        }

        /// <summary>
        /// Unified lookup: <paramref name="name"/> may be an appSettings key or <c>ConnectionStrings:Name</c>.
        /// </summary>
        public static bool TryGet(string name, out string value)
        {
            if (name.StartsWith(ConnectionStringsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var csName = name.Substring(ConnectionStringsPrefix.Length);
                return TryGetConnectionString(csName, out value);
            }

            return TryGetAppSetting(name, out value);
        }

        public static IReadOnlyCollection<string> GetLoadedLogicalKeys()
        {
            return AppSecrets.GetLogicalKeys();
        }

        private static bool TryGetResolvedFromConfigurationManager(string raw, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(raw))
            {
                return false;
            }

            if (SecretReferenceParser.TryParsePath(raw, out _))
            {
                return false;
            }

            value = raw;
            return true;
        }
    }
}
