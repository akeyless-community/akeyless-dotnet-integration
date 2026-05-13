using System.Net;
using System.Text;
using System.Text.Json;
using Akeyless.Agent.Client;
using Xunit;

namespace Akeyless.Integration.Tests;

public sealed class AgentClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }

    [Fact]
    public void ResolvePaths_posts_json_and_maps_response()
    {
        var handler = new StubHandler(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.EndsWith("/api/v1/resolve", req.RequestUri?.AbsolutePath, StringComparison.Ordinal);
            var body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            Assert.Contains("paths", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("/prod/a", body);

            var json = JsonSerializer.Serialize(new ResolveByPathsResponse
            {
                PathToValue = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["/prod/a"] = "secret-a",
                },
            });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        });

        using var client = new AkeylessLocalAgentClient("http://127.0.0.1:9999", handler);
        var result = client.ResolvePaths(new[] { "/prod/a" });
        Assert.Single(result);
        Assert.Equal("secret-a", result["/prod/a"]);
    }

    [Fact]
    public void DiscoverAndResolve_posts_configuration_path()
    {
        var handler = new StubHandler(req =>
        {
            Assert.EndsWith("/api/v1/discover-and-resolve", req.RequestUri?.AbsolutePath, StringComparison.Ordinal);
            var json = JsonSerializer.Serialize(new DiscoverAndResolveResponse
            {
                LogicalKeyToValue = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["MyKey"] = "v",
                },
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        });

        using var client = new AkeylessLocalAgentClient("http://localhost:9999", handler);
        var result = client.DiscoverAndResolve(@"C:\inetpub\wwwroot\app\web.config");
        Assert.Equal("v", result["MyKey"]);
    }
}
