using System;
using System.Collections.Generic;

namespace Akeyless.Bootstrap
{
    /// <summary>
    /// Back-compat alias for <see cref="AppConfiguration"/>. Prefer <see cref="AppConfiguration"/> or
    /// <see cref="System.Configuration.ConfigurationManager"/> after <see cref="AkeylessFrameworkBootstrapper.EnrichConfigurationAtStartup"/>.
    /// </summary>
    [Obsolete("Use AppConfiguration or ConfigurationManager after EnrichConfigurationAtStartup. This type forwards to AppConfiguration.")]
    public static class AkeylessConfig
    {
        public static string GetAppSetting(string key) => AppConfiguration.GetAppSetting(key);

        public static string GetConnectionString(string name) => AppConfiguration.GetConnectionString(name);

        public static string Get(string name) => AppConfiguration.Get(name);

        public static bool TryGetAppSetting(string key, out string value) => AppConfiguration.TryGetAppSetting(key, out value);

        public static bool TryGetConnectionString(string name, out string value) => AppConfiguration.TryGetConnectionString(name, out value);

        public static bool TryGet(string name, out string value) => AppConfiguration.TryGet(name, out value);

        public static IReadOnlyCollection<string> GetLoadedLogicalKeys() => AppConfiguration.GetLoadedLogicalKeys();
    }
}
