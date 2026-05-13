using System;
using System.Collections.Generic;
using System.Configuration;

namespace Akeyless.Bootstrap
{
    /// <summary>
    /// Single read surface after <see cref="AkeylessFrameworkBootstrapper.LoadSecretsAtStartup"/>:
    /// resolved Akeyless values first, then standard <see cref="ConfigurationManager"/> (appSettings / connection strings).
    /// Application code does not need to branch on where a value originated.
    /// </summary>
    public static class AkeylessConfig
    {
        private const string ConnectionStringsPrefix = "ConnectionStrings:";

        /// <summary>
        /// Gets an app setting or resolved secret by key. Tries Akeyless-resolved values first, then <c>ConfigurationManager.AppSettings</c>.
        /// </summary>
        public static string GetAppSetting(string key)
        {
            if (!TryGetAppSetting(key, out var value))
            {
                throw new KeyNotFoundException("No configuration value for app setting key: " + key);
            }

            return value;
        }

        /// <summary>
        /// Gets a connection string by name. Tries Akeyless (logical key <c>ConnectionStrings:name</c>) first, then <see cref="ConfigurationManager.ConnectionStrings"/>.
        /// </summary>
        public static string GetConnectionString(string name)
        {
            if (!TryGetConnectionString(name, out var value))
            {
                throw new KeyNotFoundException("No connection string for name: " + name);
            }

            return value;
        }

        /// <summary>
        /// Legacy name: same as <see cref="GetAppSetting"/> for a simple key, or use <c>ConnectionStrings:Name</c> for connection strings.
        /// </summary>
        public static string Get(string name)
        {
            if (!TryGet(name, out var value))
            {
                throw new KeyNotFoundException("No configuration value for key: " + name);
            }

            return value;
        }

        public static bool TryGetAppSetting(string key, out string value)
        {
            if (AppSecrets.TryGet(key, out value))
            {
                return true;
            }

            value = ConfigurationManager.AppSettings[key];
            return !string.IsNullOrEmpty(value);
        }

        public static bool TryGetConnectionString(string name, out string value)
        {
            var logical = ConnectionStringsPrefix + name;
            if (AppSecrets.TryGet(logical, out value))
            {
                return true;
            }

            var cs = ConfigurationManager.ConnectionStrings[name];
            value = cs?.ConnectionString;
            return !string.IsNullOrEmpty(value);
        }

        /// <summary>
        /// Unified lookup: <paramref name="name"/> may be an appSettings key or <c>ConnectionStrings:Name</c>.
        /// </summary>
        public static bool TryGet(string name, out string value)
        {
            if (AppSecrets.TryGet(name, out value))
            {
                return true;
            }

            if (name.StartsWith(ConnectionStringsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var csName = name.Substring(ConnectionStringsPrefix.Length);
                var cs = ConfigurationManager.ConnectionStrings[csName];
                value = cs?.ConnectionString;
                return !string.IsNullOrEmpty(value);
            }

            value = ConfigurationManager.AppSettings[name];
            return !string.IsNullOrEmpty(value);
        }

        /// <summary>
        /// Logical keys that were loaded from the Gateway (not values).
        /// </summary>
        public static IReadOnlyCollection<string> GetLoadedLogicalKeys()
        {
            return AppSecrets.GetLogicalKeys();
        }
    }
}
