using System.Collections.Generic;
using Akeyless.IIS.Agent;
using Akeyless.IIS.Agent.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Akeyless.Integration.Tests.Support;

public sealed class AgentWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _config;
    private readonly FakeGatewaySecretService _fakeGateway;

    public AgentWebApplicationFactory(
        Dictionary<string, string?> config,
        FakeGatewaySecretService? fakeGateway = null)
    {
        _config = config;
        _fakeGateway = fakeGateway ?? new FakeGatewaySecretService();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(_config);
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(IGatewaySecretService));
            services.AddSingleton<IGatewaySecretService>(_fakeGateway);
        });
    }

    public FakeGatewaySecretService FakeGateway => _fakeGateway;
}
