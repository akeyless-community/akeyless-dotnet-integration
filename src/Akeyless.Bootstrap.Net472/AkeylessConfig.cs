using System;
using System.Collections.Generic;

namespace Akeyless.Bootstrap
{
    /// <summary>
    /// PRD developer surface: read secrets resolved at startup (and optionally refreshed on TTL) from memory only.
    /// </summary>
    public static class AkeylessConfig
    {
        /// <summary>
        /// Gets a resolved secret by logical configuration name (e.g. appSettings key or <c>ConnectionStrings:Name</c>).
        /// </summary>
        public static string Get(string name)
        {
            if (!TryGet(name, out var value))
            {
                throw new KeyNotFoundException("No secret loaded for logical key: " + name);
            }

            return value;
        }

        /// <summary>
        /// Attempts to get a resolved secret. Keys are compared with <see cref="StringComparer.OrdinalIgnoreCase"/>.
        /// </summary>
        public static bool TryGet(string name, out string value)
        {
            return AppSecrets.TryGet(name, out value);
        }

        /// <summary>
        /// All resolved logical keys (not values). Useful for diagnostics without exposing secret material.
        /// </summary>
        public static IReadOnlyCollection<string> GetLoadedLogicalKeys()
        {
            return AppSecrets.GetLogicalKeys();
        }
    }
}
