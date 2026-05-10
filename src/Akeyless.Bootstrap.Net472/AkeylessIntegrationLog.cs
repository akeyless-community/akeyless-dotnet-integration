using System;
using System.Diagnostics;

namespace Akeyless.Bootstrap
{
    /// <summary>
    /// PRD-aligned diagnostics: log phases and counts, never secret values.
    /// </summary>
    public static class AkeylessIntegrationLog
    {
        private const string Prefix = "[Akeyless.Bootstrap] ";

        public static void Info(string message)
        {
            Trace.TraceInformation(Prefix + message);
        }

        public static void Warning(string message)
        {
            Trace.TraceWarning(Prefix + message);
        }

        public static void Error(string message, Exception ex)
        {
            Trace.TraceError(Prefix + message + (ex != null ? " — " + ex.GetType().Name : string.Empty));
        }
    }
}
