using System;

namespace Akeyless.Bootstrap
{
    /// <summary>
    /// Parses PRD-style secret references: <c>akeyless:///path/to/secret</c>.
    /// </summary>
    public static class SecretReferenceParser
    {
        public const string Scheme = "akeyless://";

        /// <summary>
        /// Returns true if <paramref name="value"/> is an Akeyless reference and sets <paramref name="secretPath"/>
        /// to the normalized item path (leading '/').
        /// </summary>
        public static bool TryParsePath(string value, out string secretPath)
        {
            secretPath = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            if (!trimmed.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var rest = trimmed.Substring(Scheme.Length).Trim();
            if (rest.Length == 0)
            {
                return false;
            }

            secretPath = rest.StartsWith("/", StringComparison.Ordinal) ? rest : "/" + rest;
            return true;
        }
    }
}
