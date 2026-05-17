using System;
using System.Collections.Generic;
using System.Configuration;
using System.Reflection;
using System.Web.Configuration;

namespace Akeyless.Bootstrap
{
    /// <summary>
    /// Applies resolved secret values onto <see cref="ConfigurationManager"/> so existing application code
    /// can keep using standard configuration APIs after startup enrichment.
    /// </summary>
    internal static class ConfigurationManagerEnricher
    {
        private const string ConnectionStringsPrefix = "ConnectionStrings:";

        /// <summary>
        /// Overwrites <c>akeyless://</c> placeholders in <see cref="ConfigurationManager"/> where the platform allows.
        /// Keys that cannot be patched in-place remain available via <see cref="AppSecrets"/> and <see cref="AppConfiguration"/>.
        /// </summary>
        internal static void ApplyResolvedValues(IReadOnlyDictionary<string, string> resolved)
        {
            if (resolved == null || resolved.Count == 0)
            {
                return;
            }

            var appSettingsPatched = 0;
            var connectionStringsPatched = 0;

            foreach (var pair in resolved)
            {
                if (TryApplyConnectionString(pair.Key, pair.Value))
                {
                    connectionStringsPatched++;
                    continue;
                }

                if (TryApplyAppSetting(pair.Key, pair.Value))
                {
                    appSettingsPatched++;
                }
            }

            AkeylessIntegrationLog.Info(
                "Configuration enrichment applied: appSettings=" + appSettingsPatched +
                ", connectionStrings=" + connectionStringsPatched +
                ", totalBindings=" + resolved.Count + ".");
        }

        private static bool TryApplyConnectionString(string logicalKey, string resolvedValue)
        {
            if (!logicalKey.StartsWith(ConnectionStringsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var name = logicalKey.Substring(ConnectionStringsPrefix.Length);
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            try
            {
                var settings = ConfigurationManager.ConnectionStrings[name];
                if (settings == null)
                {
                    return false;
                }

                settings.ConnectionString = resolvedValue;
                return string.Equals(settings.ConnectionString, resolvedValue, StringComparison.Ordinal);
            }
            catch (Exception ex)
            {
                AkeylessIntegrationLog.Warning(
                    "Could not enrich connection string '" + name + "' on ConfigurationManager: " + ex.Message);
                return false;
            }
        }

        private static bool TryApplyAppSetting(string logicalKey, string resolvedValue)
        {
            if (logicalKey.StartsWith(ConnectionStringsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (TryApplyAppSettingViaWebConfiguration(logicalKey, resolvedValue))
            {
                return true;
            }

            if (TryApplyAppSettingViaReflection(logicalKey, resolvedValue))
            {
                return true;
            }

            return false;
        }

        private static bool TryApplyAppSettingViaWebConfiguration(string key, string resolvedValue)
        {
            try
            {
                var webConfig = WebConfigurationManager.OpenWebConfiguration("~");
                var element = webConfig.AppSettings.Settings[key];
                if (element == null)
                {
                    return false;
                }

                element.Value = resolvedValue;
                return IsResolvedAppSetting(key, resolvedValue);
            }
            catch (Exception ex)
            {
                AkeylessIntegrationLog.Warning(
                    "WebConfigurationManager could not enrich app setting '" + key + "': " + ex.Message);
                return false;
            }
        }

        private static bool TryApplyAppSettingViaReflection(string key, string resolvedValue)
        {
            try
            {
                var appSettings = ConfigurationManager.AppSettings;
                if (appSettings == null)
                {
                    return false;
                }

                if (!IsReadOnlyNameValueCollection(appSettings))
                {
                    appSettings[key] = resolvedValue;
                    return IsResolvedAppSetting(key, resolvedValue);
                }

                var inner = GetInnerNameValueCollection(appSettings);
                if (inner == null)
                {
                    return false;
                }

                inner[key] = resolvedValue;
                return IsResolvedAppSetting(key, resolvedValue);
            }
            catch (Exception ex)
            {
                AkeylessIntegrationLog.Warning(
                    "Reflection could not enrich app setting '" + key + "': " + ex.Message);
                return false;
            }
        }

        private static bool IsResolvedAppSetting(string key, string expectedValue)
        {
            var current = ConfigurationManager.AppSettings[key];
            return !string.IsNullOrEmpty(current)
                && string.Equals(current, expectedValue, StringComparison.Ordinal)
                && !SecretReferenceParser.TryParsePath(current, out _);
        }

        private static bool IsReadOnlyNameValueCollection(System.Collections.Specialized.NameValueCollection collection)
        {
            return collection != null
                && collection.GetType().FullName != null
                && collection.GetType().FullName.IndexOf("ReadOnlyNameValueCollection", StringComparison.Ordinal) >= 0;
        }

        private static System.Collections.Specialized.NameValueCollection GetInnerNameValueCollection(
            System.Collections.Specialized.NameValueCollection readOnlyCollection)
        {
            var type = readOnlyCollection.GetType();
            foreach (var fieldName in new[] { "_hashtable", "_collection", "col" })
            {
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field?.GetValue(readOnlyCollection) is System.Collections.Specialized.NameValueCollection inner)
                {
                    return inner;
                }
            }

            return null;
        }
    }
}
