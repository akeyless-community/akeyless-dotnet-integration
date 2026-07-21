using System.Collections.Generic;
using System.Linq;
using akeyless.Api;
using akeyless.Model;
using AkeylessClientConfiguration = akeyless.Client.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Akeyless.IIS.Agent.Services;

/// <summary>
/// Persistent Gateway client, token reuse, and per-path memory cache (PRD: connection reuse + TTL cache).
/// </summary>
public sealed class GatewaySecretService : IGatewaySecretService
{
    private static readonly TimeSpan ReadinessCacheTtl = TimeSpan.FromSeconds(15);
    private const string ReadinessCacheKey = "gateway:readiness";

    private readonly AgentOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GatewaySecretService> _logger;
    private readonly object _tokenLock = new();
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresUtc = DateTimeOffset.MinValue;
    private V2Api? _api;

    public GatewaySecretService(IOptions<AgentOptions> options, IMemoryCache cache, ILogger<GatewaySecretService> logger)
    {
        _options = options.Value;
        _cache = cache;
        _logger = logger;
    }

    public Task<GatewayReadinessResult> CheckGatewayReadyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_cache.TryGetValue(ReadinessCacheKey, out GatewayReadinessResult? cached) && cached != null)
        {
            return Task.FromResult(cached);
        }

        if (string.IsNullOrWhiteSpace(_options.AccessId) || string.IsNullOrWhiteSpace(_options.AccessKey))
        {
            return Task.FromResult(CacheReadiness(GatewayReadinessResult.MissingCredentials()));
        }

        if (string.IsNullOrWhiteSpace(_options.GatewayUrl))
        {
            return Task.FromResult(CacheReadiness(
                GatewayReadinessResult.Unreachable("GatewayUrl is not configured.")));
        }

        try
        {
            // Force a fresh Auth against the configured Gateway (validates URL + credentials).
            lock (_tokenLock)
            {
                _cachedToken = null;
                _tokenExpiresUtc = DateTimeOffset.MinValue;
            }

            _ = GetOrRefreshToken();
            var result = GatewayReadinessResult.Ok();
            _logger.LogInformation("Gateway readiness probe succeeded.");
            return Task.FromResult(CacheReadiness(result));
        }
        catch (Exception ex)
        {
            var result = ClassifyReadinessFailure(ex);
            _logger.LogWarning(
                "Gateway readiness probe failed: gateway={Gateway} detail={Detail}",
                result.Gateway,
                result.Detail);
            return Task.FromResult(CacheReadiness(result));
        }
    }

    private GatewayReadinessResult CacheReadiness(GatewayReadinessResult result)
    {
        _cache.Set(ReadinessCacheKey, result, ReadinessCacheTtl);
        return result;
    }

    private static GatewayReadinessResult ClassifyReadinessFailure(Exception ex)
    {
        var message = ex.Message ?? string.Empty;
        var combined = ex.ToString();

        if (message.Contains("credentials missing", StringComparison.OrdinalIgnoreCase) ||
            (message.Contains("AccessId", StringComparison.OrdinalIgnoreCase) &&
             message.Contains("AccessKey", StringComparison.OrdinalIgnoreCase)))
        {
            return GatewayReadinessResult.MissingCredentials();
        }

        if (message.Contains("authentication failed", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("401", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("403", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("Forbidden", StringComparison.OrdinalIgnoreCase))
        {
            return GatewayReadinessResult.AuthFailed("Gateway rejected AccessId/AccessKey authentication.");
        }

        if (combined.Contains("NameResolutionFailure", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("No such host", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("nodename nor servname", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("getaddrinfo", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("DNS", StringComparison.OrdinalIgnoreCase))
        {
            return GatewayReadinessResult.Unreachable("Gateway host could not be resolved (check GatewayUrl).");
        }

        if (combined.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("Timeout", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("actively refused", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("Connection refused", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("NetworkUnreachable", StringComparison.OrdinalIgnoreCase))
        {
            return GatewayReadinessResult.Unreachable("Gateway could not be reached (network or GatewayUrl).");
        }

        return GatewayReadinessResult.Unreachable("Gateway connectivity or authentication check failed.");
    }

    public Task<IReadOnlyDictionary<string, string>> ResolvePathsAsync(
        IReadOnlyList<string> normalizedPaths,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (normalizedPaths.Count == 0)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var toFetch = new List<string>();
        foreach (var p in normalizedPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var cacheKey = "path:" + p;
            if (_cache.TryGetValue(cacheKey, out string? cached) && cached != null)
            {
                result[p] = cached;
            }
            else
            {
                toFetch.Add(p);
            }
        }

        if (toFetch.Count == 0)
        {
            _logger.LogInformation("Gateway resolve: all {Count} path(s) served from agent cache.", result.Count);
            return Task.FromResult<IReadOnlyDictionary<string, string>>(result);
        }

        var token = GetOrRefreshToken();
        var api = GetApi();
        var body = new GetSecretValue(names: toFetch.ToList(), token: token);
        var raw = api.GetSecretValue(body) ?? new Dictionary<string, object>();

        var ttl = TimeSpan.FromSeconds(Math.Max(1, _options.CacheTtlSeconds));
        foreach (var kv in raw)
        {
            if (string.IsNullOrEmpty(kv.Key))
            {
                continue;
            }

            var nk = kv.Key.StartsWith("/", StringComparison.Ordinal) ? kv.Key : "/" + kv.Key;
            var val = kv.Value?.ToString() ?? string.Empty;
            result[nk] = val;
            _cache.Set("path:" + nk, val, ttl);
        }

        foreach (var path in toFetch)
        {
            if (!result.ContainsKey(path))
            {
                throw new InvalidOperationException("Gateway did not return value for path: " + path);
            }
        }

        _logger.LogInformation(
            "Gateway resolve: fetched {Fetched} path(s), {CachedHit} from cache (no values logged).",
            toFetch.Count,
            result.Count - toFetch.Count);
        return Task.FromResult<IReadOnlyDictionary<string, string>>(result);
    }

    private V2Api GetApi()
    {
        if (_api != null)
        {
            return _api;
        }

        var cfg = new AkeylessClientConfiguration { BasePath = _options.GatewayUrl.TrimEnd('/') };
        _api = new V2Api(cfg);
        return _api;
    }

    private string GetOrRefreshToken()
    {
        lock (_tokenLock)
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTimeOffset.UtcNow < _tokenExpiresUtc.AddMinutes(-2))
            {
                return _cachedToken;
            }
        }

        if (string.IsNullOrEmpty(_options.AccessId) || string.IsNullOrEmpty(_options.AccessKey))
        {
            throw new InvalidOperationException(
                "Agent Gateway credentials missing: set AkeylessAgent:AccessId and AkeylessAgent:AccessKey (or environment variables).");
        }

        var api = GetApi();
        var auth = api.Auth(new Auth(accessId: _options.AccessId, accessKey: _options.AccessKey));
        if (auth?.Token is not { Length: > 0 } token)
        {
            throw new InvalidOperationException("Gateway authentication failed.");
        }

        lock (_tokenLock)
        {
            _cachedToken = token;
            _tokenExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(15);
        }

        _logger.LogInformation("Gateway auth token refreshed (token value not logged).");
        return token;
    }
}
