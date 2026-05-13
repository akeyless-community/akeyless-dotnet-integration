using System;
using System.Collections.Generic;

namespace Akeyless.Agent.Client;

public sealed class ResolveByPathsRequest
{
    public List<string> Paths { get; set; } = new();
}

public sealed class ResolveByPathsResponse
{
    /// <summary>Normalized secret path (leading /) to resolved value.</summary>
    public Dictionary<string, string> PathToValue { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed class DiscoverAndResolveRequest
{
    /// <summary>Full path to web.config or app.config on disk.</summary>
    public string ConfigurationFilePath { get; set; } = string.Empty;
}

/// <summary>Logical configuration key (e.g. appSettings key or ConnectionStrings:Name) to resolved value.</summary>
public sealed class DiscoverAndResolveResponse
{
    public Dictionary<string, string> LogicalKeyToValue { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
