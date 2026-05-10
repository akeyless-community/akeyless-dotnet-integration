using System;

namespace Akeyless.Bootstrap
{
    /// <summary>
    /// Maps a configuration or environment logical name to an Akeyless item path (no <c>akeyless://</c> prefix).
    /// </summary>
    public sealed class SecretBinding
    {
        public SecretBinding(string logicalKey, string secretPath)
        {
            LogicalKey = logicalKey ?? throw new ArgumentNullException(nameof(logicalKey));
            SecretPath = secretPath ?? throw new ArgumentNullException(nameof(secretPath));
        }

        public string LogicalKey { get; }

        public string SecretPath { get; }
    }
}
