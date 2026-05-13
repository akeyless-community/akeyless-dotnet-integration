using System;
using System.Collections.Generic;
using System.Linq;
using Akeyless.Agent.Client;
using akeyless.Api;
using akeyless.Model;
using AkeylessClientConfiguration = akeyless.Client.Configuration;

namespace Akeyless.WebApp.Net8;

internal static class SecretPathResolver
{
    public static Dictionary<string, string> FetchPathToValues(IReadOnlyList<string> uniquePaths)
    {
        if (uniquePaths == null || uniquePaths.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var agentUrl = Environment.GetEnvironmentVariable("AKEYLESS_AGENT_URL");
        if (!string.IsNullOrWhiteSpace(agentUrl))
        {
            using var client = new AkeylessLocalAgentClient(agentUrl.Trim());
            var fromAgent = client.ResolvePaths(uniquePaths);
            return fromAgent.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
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
                "Set AKEYLESS_AGENT_URL for the local agent, or set AKEYLESS_ACCESS_ID and AKEYLESS_ACCESS_KEY for direct Gateway access.");
        }

        var cfg = new AkeylessClientConfiguration { BasePath = gatewayUrl.TrimEnd('/') };
        var api = new V2Api(cfg);
        var authResult = api.Auth(new Auth(accessId: accessId, accessKey: accessKey));
        if (authResult?.Token is not { Length: > 0 })
        {
            throw new InvalidOperationException("Akeyless authentication did not return a token.");
        }

        var body = new GetSecretValue(names: uniquePaths.ToList(), token: authResult.Token);
        var raw = api.GetSecretValue(body) ?? new Dictionary<string, object>();

        var pathToValue = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in raw)
        {
            if (string.IsNullOrEmpty(kv.Key))
            {
                continue;
            }

            var normalizedKey = kv.Key.StartsWith("/", StringComparison.Ordinal) ? kv.Key : "/" + kv.Key;
            pathToValue[normalizedKey] = kv.Value?.ToString() ?? string.Empty;
        }

        return pathToValue;
    }
}
