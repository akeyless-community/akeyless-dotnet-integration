using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Akeyless.IIS.Agent.Services;

/// <summary>
/// Restricts agent-side XML discovery to paths under configured directory roots (path traversal mitigation).
/// </summary>
public sealed class AllowedPathValidator
{
    private readonly AgentOptions _options;
    private readonly ILogger<AllowedPathValidator> _logger;

    public AllowedPathValidator(IOptions<AgentOptions> options, ILogger<AllowedPathValidator> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigurationFileAllowed(string configurationFilePath, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(configurationFilePath))
        {
            errorMessage = "Path is required.";
            return false;
        }

        string full;
        try
        {
            full = Path.GetFullPath(configurationFilePath);
        }
        catch
        {
            errorMessage = "Invalid path.";
            return false;
        }

        var roots = _options.AllowedConfigurationRoots;
        if (roots == null || roots.Count == 0)
        {
            _logger.LogWarning("AllowedConfigurationRoots is empty; discover-and-resolve is disabled.");
            errorMessage = "Agent administrator must configure AllowedConfigurationRoots.";
            return false;
        }

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            string fullRoot;
            try
            {
                fullRoot = Path.GetFullPath(root.Trim());
            }
            catch
            {
                continue;
            }

            var sep = Path.DirectorySeparatorChar;
            var prefix = fullRoot.EndsWith(sep) ? fullRoot : fullRoot + sep;
            if (full.Equals(fullRoot, StringComparison.OrdinalIgnoreCase) ||
                full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        errorMessage = "Path is not under an allowed configuration root.";
        return false;
    }
}
