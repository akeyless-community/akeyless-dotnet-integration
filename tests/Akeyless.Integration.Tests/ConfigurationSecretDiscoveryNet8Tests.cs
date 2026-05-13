using Akeyless.WebApp.Net8;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Akeyless.Integration.Tests;

public sealed class ConfigurationSecretDiscoveryNet8Tests
{
    [Fact]
    public void Discovers_nested_keys_and_environment_values()
    {
        var envName = "AKEYLESS_TEST_ENV_REF_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        Environment.SetEnvironmentVariable(envName, "akeyless:///env/secret");
        try
        {
            var dict = new Dictionary<string, string?>
            {
                ["Section:Key1"] = "akeyless:///app/1",
                ["ConnectionStrings:Default"] = "akeyless:///db/c",
            };
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(dict!).Build();
            var bindings = ConfigurationSecretDiscovery.DiscoverFromConfiguration(cfg);
            // DiscoverFromConfiguration always merges process environment variables, so total count is not fixed.
            Assert.Contains(bindings, b => b.LogicalKey == "Section:Key1" && b.SecretPath == "/app/1");
            Assert.Contains(bindings, b => b.LogicalKey == "ConnectionStrings:Default" && b.SecretPath == "/db/c");

            var withEnv = new ConfigurationBuilder().AddInMemoryCollection(dict!).AddEnvironmentVariables().Build();
            var all = ConfigurationSecretDiscovery.DiscoverFromConfiguration(withEnv);
            Assert.Contains(all, b => b.LogicalKey == envName && b.SecretPath == "/env/secret");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, null);
        }
    }

    [Fact]
    public void FromEnvironmentSecretList_parses_paths()
    {
        var list = ConfigurationSecretDiscovery.FromEnvironmentSecretList("/a;/b");
        Assert.Equal(2, list.Count);
        Assert.Equal("/a", list[0].SecretPath);
        Assert.Equal("/b", list[1].SecretPath);
    }
}
