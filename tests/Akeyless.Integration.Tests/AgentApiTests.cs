using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Akeyless.Agent.Client;
using Akeyless.Integration.Tests.Support;
using Akeyless.IIS.Agent.Services;
using Xunit;

namespace Akeyless.Integration.Tests;

public sealed class AgentApiTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonRead = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _tempRoot;
    private readonly AgentWebApplicationFactory _factory;

    public AgentApiTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "akeyless-agent-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        var cfg = new Dictionary<string, string?>
        {
            ["AkeylessAgent:ListenUrl"] = "http://127.0.0.1:0",
            ["AkeylessAgent:GatewayUrl"] = "https://invalid.test",
            ["AkeylessAgent:AccessId"] = "test",
            ["AkeylessAgent:AccessKey"] = "test",
            ["AkeylessAgent:CacheTtlSeconds"] = "60",
            ["AkeylessAgent:AllowedConfigurationRoots:0"] = _tempRoot,
        };

        _factory = new AgentWebApplicationFactory(cfg);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // ignore
        }

        _factory.Dispose();
    }

    [Fact]
    public async Task Health_returns_ok()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("akeyless-iis-agent", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Resolve_normalizes_paths_and_returns_values()
    {
        var client = _factory.CreateClient();
        var req = new ResolveByPathsRequest { Paths = new List<string> { "prod/x", "/prod/y" } };
        var res = await client.PostAsJsonAsync("/api/v1/resolve", req);
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        var payload = await res.Content.ReadFromJsonAsync<ResolveByPathsResponse>(JsonRead);
        Assert.NotNull(payload?.PathToValue);
        Assert.Equal("resolved-value-for:/prod/x", payload.PathToValue["/prod/x"]);
        Assert.Equal("resolved-value-for:/prod/y", payload.PathToValue["/prod/y"]);
    }

    [Fact]
    public async Task Resolve_empty_paths_returns_empty_object()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/v1/resolve", new ResolveByPathsRequest { Paths = new() });
        res.EnsureSuccessStatusCode();
        var payload = await res.Content.ReadFromJsonAsync<ResolveByPathsResponse>(JsonRead);
        Assert.NotNull(payload);
        Assert.Empty(payload.PathToValue);
    }

    [Fact]
    public async Task Discover_and_resolve_reads_web_config_under_allowlist()
    {
        var webConfig = Path.Combine(_tempRoot, "web.config");
        await File.WriteAllTextAsync(
            webConfig,
            """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <appSettings>
                <add key="ApiKey" value="akeyless:///prod/app/api-key" />
              </appSettings>
              <connectionStrings>
                <add name="Db" connectionString="akeyless:///prod/app/db" providerName="System.Data.SqlClient" />
              </connectionStrings>
            </configuration>
            """);

        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync(
            "/api/v1/discover-and-resolve",
            new DiscoverAndResolveRequest { ConfigurationFilePath = webConfig });

        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        var payload = await res.Content.ReadFromJsonAsync<DiscoverAndResolveResponse>(JsonRead);
        Assert.NotNull(payload?.LogicalKeyToValue);
        Assert.Equal("resolved-value-for:/prod/app/api-key", payload.LogicalKeyToValue["ApiKey"]);
        Assert.Equal("resolved-value-for:/prod/app/db", payload.LogicalKeyToValue["ConnectionStrings:Db"]);
    }

    [Fact]
    public async Task Discover_and_resolve_rejects_path_outside_allowlist()
    {
        var outside = Path.Combine(Path.GetTempPath(), "akeyless-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        try
        {
            var webConfig = Path.Combine(outside, "web.config");
            await File.WriteAllTextAsync(
                webConfig,
                """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration><appSettings>
                  <add key="K" value="akeyless:///x" />
                </appSettings></configuration>
                """);

            var client = _factory.CreateClient();
            var res = await client.PostAsJsonAsync(
                "/api/v1/discover-and-resolve",
                new DiscoverAndResolveRequest { ConfigurationFilePath = webConfig });

            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }
        finally
        {
            try
            {
                Directory.Delete(outside, true);
            }
            catch
            {
                // ignore
            }
        }
    }
}
