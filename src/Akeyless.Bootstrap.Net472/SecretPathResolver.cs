using System;
using System.Collections.Generic;
using System.Linq;
using Akeyless.Agent.Client;
using akeyless.Api;
using akeyless.Client;
using akeyless.Model;

namespace Akeyless.Bootstrap
{
    /// <summary>
    /// Resolves Akeyless item paths via the local agent (<c>AKEYLESS_AGENT_URL</c>) or directly against the Gateway.
    /// </summary>
    internal static class SecretPathResolver
    {
        /// <summary>
        /// Returns normalized secret path → value.
        /// </summary>
        public static Dictionary<string, string> FetchPathToValues(IReadOnlyList<string> uniquePaths)
        {
            if (uniquePaths == null || uniquePaths.Count == 0)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var agentUrl = Environment.GetEnvironmentVariable("AKEYLESS_AGENT_URL");
            if (!string.IsNullOrWhiteSpace(agentUrl))
            {
                using (var client = new AkeylessLocalAgentClient(agentUrl.Trim()))
                {
                    var fromAgent = client.ResolvePaths(uniquePaths);
                    return fromAgent.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
                }
            }

            return FetchFromGatewayDirect(uniquePaths);
        }

        private static Dictionary<string, string> FetchFromGatewayDirect(IReadOnlyList<string> uniquePaths)
        {
            var gatewayUrl = Environment.GetEnvironmentVariable("AKEYLESS_GW_URL");
            if (string.IsNullOrWhiteSpace(gatewayUrl))
            {
                gatewayUrl = "https://api.akeyless.io";
            }

            var accessId = Environment.GetEnvironmentVariable("AKEYLESS_ACCESS_ID");
            var accessKey = Environment.GetEnvironmentVariable("AKEYLESS_ACCESS_KEY");
            if (string.IsNullOrEmpty(accessId) || string.IsNullOrEmpty(accessKey))
            {
                throw new InvalidOperationException(
                    "Set AKEYLESS_AGENT_URL for the IIS agent, or set AKEYLESS_ACCESS_ID and AKEYLESS_ACCESS_KEY for direct Gateway access.");
            }

            var cfg = new Configuration { BasePath = gatewayUrl.TrimEnd('/') };
            var api = new V2Api(cfg);
            var authResult = api.Auth(new Auth(accessId: accessId, accessKey: accessKey));
            if (authResult == null || string.IsNullOrEmpty(authResult.Token))
            {
                throw new InvalidOperationException("Akeyless authentication did not return a token.");
            }

            var body = new GetSecretValue(names: uniquePaths.ToList(), token: authResult.Token);
            var raw = api.GetSecretValue(body) ?? new Dictionary<string, string>();

            var pathToValue = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in raw)
            {
                var normalizedKey = string.IsNullOrEmpty(kv.Key)
                    ? kv.Key
                    : (kv.Key.StartsWith("/", StringComparison.Ordinal) ? kv.Key : "/" + kv.Key);
                pathToValue[normalizedKey] = kv.Value ?? string.Empty;
            }

            return pathToValue;
        }
    }
}
