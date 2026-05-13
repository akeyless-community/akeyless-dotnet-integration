namespace Akeyless.IIS.Agent;

public sealed class AgentOptions
{
    public const string SectionName = "AkeylessAgent";

    /// <summary>Gateway base URL (e.g. https://api.akeyless.io or your gateway).</summary>
    public string GatewayUrl { get; set; } = "https://api.akeyless.io";

    /// <summary>API key auth (PRD: also supports cert/UID in future).</summary>
    public string AccessId { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    /// <summary>Sliding cache TTL per secret path in the agent process.</summary>
    public int CacheTtlSeconds { get; set; } = 300;

    /// <summary>HTTP listen URL; must use loopback (e.g. http://127.0.0.1:17890).</summary>
    public string ListenUrl { get; set; } = "http://127.0.0.1:17890";

    /// <summary>
    /// Full directory roots allowed for <c>/api/v1/discover-and-resolve</c> (e.g. <c>C:\\inetpub\\wwwroot</c>).
    /// </summary>
    public List<string> AllowedConfigurationRoots { get; set; } = new();
}
