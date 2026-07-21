using System;
using System.Collections.Generic;
using Akeyless.IIS.Agent.Services;

namespace Akeyless.Integration.Tests.Support;

/// <summary>Deterministic secret resolution for agent HTTP tests (no real Gateway).</summary>
public sealed class FakeGatewaySecretService : IGatewaySecretService
{
    private readonly Func<IReadOnlyList<string>, IReadOnlyDictionary<string, string>> _resolver;

    public FakeGatewaySecretService(
        Func<IReadOnlyList<string>, IReadOnlyDictionary<string, string>>? resolver = null)
    {
        _resolver = resolver ?? (paths =>
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in paths)
            {
                d[p] = "resolved-value-for:" + p;
            }

            return d;
        });
    }

    /// <summary>When set, readiness returns this result; otherwise returns healthy.</summary>
    public GatewayReadinessResult? ReadinessResult { get; set; }

    public int ReadinessCallCount { get; private set; }

    public Task<IReadOnlyDictionary<string, string>> ResolvePathsAsync(
        IReadOnlyList<string> normalizedPaths,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_resolver(normalizedPaths));
    }

    public Task<GatewayReadinessResult> CheckGatewayReadyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReadinessCallCount++;
        return Task.FromResult(ReadinessResult ?? GatewayReadinessResult.Ok());
    }
}
