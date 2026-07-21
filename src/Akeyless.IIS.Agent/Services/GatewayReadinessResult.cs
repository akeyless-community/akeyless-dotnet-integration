namespace Akeyless.IIS.Agent.Services;

/// <summary>
/// Result of a Gateway connectivity / authentication readiness probe (no secrets).
/// </summary>
public sealed class GatewayReadinessResult
{
    public bool IsReady { get; init; }

    /// <summary>Machine-readable reason: <c>ok</c>, <c>missing_credentials</c>, <c>unreachable</c>, <c>auth_failed</c>.</summary>
    public string Gateway { get; init; } = "unreachable";

    /// <summary>Safe, non-secret detail for operators (no Access Key, token, or raw Gateway bodies).</summary>
    public string? Detail { get; init; }

    public static GatewayReadinessResult Ok() =>
        new() { IsReady = true, Gateway = "reachable", Detail = "Gateway authentication succeeded." };

    public static GatewayReadinessResult MissingCredentials() =>
        new()
        {
            IsReady = false,
            Gateway = "missing_credentials",
            Detail = "AccessId or AccessKey is not configured on the agent.",
        };

    public static GatewayReadinessResult Unreachable(string safeDetail) =>
        new() { IsReady = false, Gateway = "unreachable", Detail = safeDetail };

    public static GatewayReadinessResult AuthFailed(string safeDetail) =>
        new() { IsReady = false, Gateway = "auth_failed", Detail = safeDetail };
}
