using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web.Configuration;
using System.Web.Hosting;

namespace Akeyless.Bootstrap
{
    /// <summary>
    /// Discovers <c>akeyless://</c> references per PRD: XML configuration first, then environment variables.
    /// Uses framework-merged configuration (typical <c>configSource</c> scenarios on IIS).
    /// </summary>
    public static class ConfigurationSecretDiscovery
    {
        /// <summary>
        /// Ordered discovery: web.config merge (when hosted), <see cref="ConfigurationManager"/>, then environment.
        /// Duplicate logical keys: first source wins.
        /// </summary>
        public static IReadOnlyList<SecretBinding> Discover()
        {
            var ordered = new List<SecretBinding>();
            var logicalKeysSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void TryAdd(string logicalKey, string configValue)
            {
                if (string.IsNullOrEmpty(logicalKey) || !SecretReferenceParser.TryParsePath(configValue, out var path))
                {
                    return;
                }

                if (!logicalKeysSeen.Add(logicalKey))
                {
                    return;
                }

                ordered.Add(new SecretBinding(logicalKey, path));
            }

            DiscoverWebConfiguration(TryAdd);
            DiscoverConfigurationManager(TryAdd, logicalKeysSeen);
            DiscoverEnvironment(TryAdd, logicalKeysSeen);

            return ordered;
        }

        private static void DiscoverWebConfiguration(Action<string, string> tryAdd)
        {
            if (!HostingEnvironment.IsHosted || string.IsNullOrEmpty(HostingEnvironment.ApplicationPhysicalPath))
            {
                return;
            }

            try
            {
                var webConfig = WebConfigurationManager.OpenWebConfiguration(HostingEnvironment.ApplicationPhysicalPath);
                var appSection = webConfig.GetSection("appSettings") as AppSettingsSection;
                if (appSection?.Settings != null)
                {
                    foreach (KeyValueConfigurationElement element in appSection.Settings)
                    {
                        tryAdd(element.Key, element.Value);
                    }
                }

                var connSection = webConfig.GetSection("connectionStrings") as ConnectionStringsSection;
                if (connSection?.ConnectionStrings != null)
                {
                    foreach (ConnectionStringSettings cs in connSection.ConnectionStrings)
                    {
                        if (cs == null || string.IsNullOrEmpty(cs.Name))
                        {
                            continue;
                        }

                        tryAdd("ConnectionStrings:" + cs.Name, cs.ConnectionString);
                    }
                }
            }
            catch (Exception ex)
            {
                AkeylessIntegrationLog.Warning("Web.config discovery skipped: " + ex.GetType().Name);
            }
        }

        private static void DiscoverConfigurationManager(Action<string, string> tryAdd, HashSet<string> logicalKeysSeen)
        {
            try
            {
                var keys = ConfigurationManager.AppSettings.AllKeys ?? Array.Empty<string>();
                foreach (var key in keys)
                {
                    if (logicalKeysSeen.Contains(key))
                    {
                        continue;
                    }

                    tryAdd(key, ConfigurationManager.AppSettings[key]);
                }

                foreach (ConnectionStringSettings cs in ConfigurationManager.ConnectionStrings)
                {
                    if (cs == null || string.IsNullOrEmpty(cs.Name))
                    {
                        continue;
                    }

                    var logicalKey = "ConnectionStrings:" + cs.Name;
                    if (logicalKeysSeen.Contains(logicalKey))
                    {
                        continue;
                    }

                    tryAdd(logicalKey, cs.ConnectionString);
                }
            }
            catch (Exception ex)
            {
                AkeylessIntegrationLog.Warning("ConfigurationManager discovery skipped: " + ex.GetType().Name);
            }
        }

        private static void DiscoverEnvironment(Action<string, string> tryAdd, HashSet<string> logicalKeysSeen)
        {
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                var name = entry.Key as string;
                var value = entry.Value as string;
                if (string.IsNullOrEmpty(name) || logicalKeysSeen.Contains(name))
                {
                    continue;
                }

                tryAdd(name, value);
            }
        }

        /// <summary>
        /// Builds bindings from <c>AKEYLESS_SECRET_NAMES</c> only (logical key equals secret path).
        /// </summary>
        public static IReadOnlyList<SecretBinding> FromEnvironmentSecretList(string rawList)
        {
            if (string.IsNullOrWhiteSpace(rawList))
            {
                return Array.Empty<SecretBinding>();
            }

            return rawList
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .Select(s => new SecretBinding(s, s.StartsWith("/", StringComparison.Ordinal) ? s : "/" + s))
                .ToList();
        }
    }
}
