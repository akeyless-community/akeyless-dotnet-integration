using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Akeyless.IIS.Agent.Services;

/// <summary>
/// PRD: intelligent configuration parsing — web.config / app.config, optional external appSettings file and configSource.
/// </summary>
public sealed class ConfigurationDiscoveryService
{
    private readonly ILogger<ConfigurationDiscoveryService> _logger;

    public ConfigurationDiscoveryService(ILogger<ConfigurationDiscoveryService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Returns logical key → Akeyless item path for every <c>akeyless://</c> reference found.
    /// </summary>
    public IReadOnlyList<(string LogicalKey, string SecretPath)> DiscoverBindings(string configurationFilePath)
    {
        var baseDir = Path.GetDirectoryName(configurationFilePath);
        if (string.IsNullOrEmpty(baseDir))
        {
            throw new ArgumentException("Invalid configuration file path.", nameof(configurationFilePath));
        }

        var list = new List<(string, string)>();
        LoadInto(configurationFilePath, baseDir, list);
        _logger.LogInformation("Discovery found {Count} secret reference(s) in configuration chain.", list.Count);
        return list;
    }

    private static void LoadInto(string configPath, string baseDir, List<(string LogicalKey, string SecretPath)> list)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("Configuration file not found: " + configPath);
        }

        var doc = XDocument.Load(configPath, LoadOptions.PreserveWhitespace);

        var appSettingsEl = doc.Root?.Element("appSettings");
        if (appSettingsEl != null)
        {
            var externalFile = appSettingsEl.Attribute("file")?.Value;
            if (!string.IsNullOrWhiteSpace(externalFile))
            {
                var merged = Path.GetFullPath(Path.Combine(baseDir, externalFile));
                ParseAppSettingsFile(merged, list);
            }
            else
            {
                var configSource = appSettingsEl.Attribute("configSource")?.Value;
                if (!string.IsNullOrWhiteSpace(configSource))
                {
                    var merged = Path.GetFullPath(Path.Combine(baseDir, configSource));
                    ParseAppSettingsFile(merged, list);
                }
                else
                {
                    ParseAppSettingsElement(appSettingsEl, list);
                }
            }
        }

        var connEl = doc.Root?.Element("connectionStrings");
        if (connEl != null)
        {
            var configSource = connEl.Attribute("configSource")?.Value;
            if (!string.IsNullOrWhiteSpace(configSource))
            {
                var merged = Path.GetFullPath(Path.Combine(baseDir, configSource));
                ParseConnectionStringsFile(merged, list);
            }
            else
            {
                ParseConnectionStringsElement(connEl, list);
            }
        }
    }

    private static void ParseAppSettingsElement(XElement appSettingsEl, List<(string LogicalKey, string SecretPath)> list)
    {
        foreach (var add in appSettingsEl.Elements("add"))
        {
            var key = add.Attribute("key")?.Value;
            var value = add.Attribute("value")?.Value;
            TryAddAppSetting(key, value, list);
        }
    }

    private static void ParseAppSettingsFile(string path, List<(string LogicalKey, string SecretPath)> list)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var doc = XDocument.Load(path);
        var root = doc.Root;
        if (root == null)
        {
            return;
        }

        foreach (var add in root.Elements("add"))
        {
            var key = add.Attribute("key")?.Value;
            var value = add.Attribute("value")?.Value;
            TryAddAppSetting(key, value, list);
        }
    }

    private static void ParseConnectionStringsElement(XElement connEl, List<(string LogicalKey, string SecretPath)> list)
    {
        foreach (var add in connEl.Elements("add"))
        {
            var name = add.Attribute("name")?.Value;
            var conn = add.Attribute("connectionString")?.Value;
            TryAddConnectionString(name, conn, list);
        }
    }

    private static void ParseConnectionStringsFile(string path, List<(string LogicalKey, string SecretPath)> list)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var doc = XDocument.Load(path);
        var root = doc.Root;
        if (root == null)
        {
            return;
        }

        foreach (var add in root.Elements("add"))
        {
            var name = add.Attribute("name")?.Value;
            var conn = add.Attribute("connectionString")?.Value;
            TryAddConnectionString(name, conn, list);
        }
    }

    private static void TryAddAppSetting(string? key, string? value, List<(string LogicalKey, string SecretPath)> list)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
        {
            return;
        }

        if (!SecretReferenceParser.TryParsePath(value, out var path))
        {
            return;
        }

        list.Add((key, path));
    }

    private static void TryAddConnectionString(string? name, string? connectionString, List<(string LogicalKey, string SecretPath)> list)
    {
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(connectionString))
        {
            return;
        }

        if (!SecretReferenceParser.TryParsePath(connectionString, out var path))
        {
            return;
        }

        list.Add(("ConnectionStrings:" + name, path));
    }
}
