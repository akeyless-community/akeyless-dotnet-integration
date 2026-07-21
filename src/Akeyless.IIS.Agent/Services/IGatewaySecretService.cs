namespace Akeyless.IIS.Agent.Services;

/// <summary>
/// Resolves Akeyless item paths to secret values (implemented by <see cref="GatewaySecretService"/> or test doubles).
/// </summary>
public interface IGatewaySecretService
{
    Task<IReadOnlyDictionary<string, string>> ResolvePathsAsync(
        IReadOnlyList<string> normalizedPaths,
        CancellationToken cancellationToken);

    /// <summary>
    /// Probes Gateway reachability and AccessId/AccessKey authentication for readiness checks.
    /// Does not return secret values, tokens, or raw Gateway error bodies.
    /// </summary>
    Task<GatewayReadinessResult> CheckGatewayReadyAsync(CancellationToken cancellationToken);
}
