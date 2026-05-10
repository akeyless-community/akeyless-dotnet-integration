using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Akeyless.Bootstrap
{
    /// <summary>
    /// In-process store for secret values fetched from Akeyless. Values are not written to disk by this library.
    /// </summary>
    public static class AppSecrets
    {
        private static readonly object Gate = new object();
        private static IReadOnlyDictionary<string, string> _values =
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        /// <summary>
        /// Resolved secrets keyed by logical configuration name (case-insensitive).
        /// </summary>
        public static IReadOnlyDictionary<string, string> Values
        {
            get
            {
                lock (Gate)
                {
                    return _values;
                }
            }
        }

        public static bool TryGet(string logicalKey, out string value)
        {
            lock (Gate)
            {
                return _values.TryGetValue(logicalKey, out value);
            }
        }

        public static IReadOnlyCollection<string> GetLogicalKeys()
        {
            lock (Gate)
            {
                return _values.Keys.ToList();
            }
        }

        internal static void ReplaceAll(IDictionary<string, string> next)
        {
            var copy = new Dictionary<string, string>(next, StringComparer.OrdinalIgnoreCase);
            lock (Gate)
            {
                _values = new ReadOnlyDictionary<string, string>(copy);
            }
        }
    }
}
