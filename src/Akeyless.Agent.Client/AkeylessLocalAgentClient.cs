using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace Akeyless.Agent.Client;

/// <summary>
/// Calls the local Akeyless IIS Agent (localhost REST). Does not log secret values.
/// </summary>
public sealed class AkeylessLocalAgentClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public AkeylessLocalAgentClient(string agentBaseUrl, HttpMessageHandler? handler = null)
    {
        if (string.IsNullOrWhiteSpace(agentBaseUrl))
        {
            throw new ArgumentException("Agent base URL is required.", nameof(agentBaseUrl));
        }

        _baseUrl = agentBaseUrl.TrimEnd('/');
        _http = handler == null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(120);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// POST /api/v1/resolve — resolves Akeyless item paths to values.
    /// </summary>
    public IReadOnlyDictionary<string, string> ResolvePaths(IEnumerable<string> normalizedPaths)
    {
        return ResolvePathsAsync(normalizedPaths, CancellationToken.None).GetAwaiter().GetResult();
    }

    public async Task<IReadOnlyDictionary<string, string>> ResolvePathsAsync(
        IEnumerable<string> normalizedPaths,
        CancellationToken cancellationToken)
    {
        var paths = normalizedPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (paths.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var req = new ResolveByPathsRequest { Paths = paths };
        var json = JsonSerializer.Serialize(req, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync(_baseUrl + "/api/v1/resolve", content, cancellationToken)
            .ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        var parsed = JsonSerializer.Deserialize<ResolveByPathsResponse>(body, JsonOptions);
        return parsed?.PathToValue ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// POST /api/v1/discover-and-resolve — agent parses XML (allowlisted paths only) and resolves all references.
    /// </summary>
    public IReadOnlyDictionary<string, string> DiscoverAndResolve(string configurationFilePath)
    {
        return DiscoverAndResolveAsync(configurationFilePath, CancellationToken.None).GetAwaiter().GetResult();
    }

    public async Task<IReadOnlyDictionary<string, string>> DiscoverAndResolveAsync(
        string configurationFilePath,
        CancellationToken cancellationToken)
    {
        var req = new DiscoverAndResolveRequest { ConfigurationFilePath = configurationFilePath };
        var json = JsonSerializer.Serialize(req, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var resp = await _http
            .PostAsync(_baseUrl + "/api/v1/discover-and-resolve", content, cancellationToken)
            .ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        var parsed = JsonSerializer.Deserialize<DiscoverAndResolveResponse>(body, JsonOptions);
        return parsed?.LogicalKeyToValue ??
               new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public void Dispose() => _http.Dispose();
}
