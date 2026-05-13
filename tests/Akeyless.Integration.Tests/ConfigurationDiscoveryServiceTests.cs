using Akeyless.IIS.Agent.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Akeyless.Integration.Tests;

public sealed class ConfigurationDiscoveryServiceTests
{
    private readonly ConfigurationDiscoveryService _sut =
        new(NullLogger<ConfigurationDiscoveryService>.Instance);

    [Fact]
    public void Discovers_appSettings_and_connectionStrings_in_single_file()
    {
        var dir = Path.Combine(Path.GetTempPath(), "akeyless-disc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "web.config");
            File.WriteAllText(
                path,
                """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <appSettings>
                    <add key="K1" value="akeyless:///a/1" />
                  </appSettings>
                  <connectionStrings>
                    <add name="Main" connectionString="akeyless:///b/2" providerName="System.Data.SqlClient" />
                  </connectionStrings>
                </configuration>
                """);

            var bindings = _sut.DiscoverBindings(path);
            Assert.Equal(2, bindings.Count);
            Assert.Contains(bindings, b => b.LogicalKey == "K1" && b.SecretPath == "/a/1");
            Assert.Contains(bindings, b => b.LogicalKey == "ConnectionStrings:Main" && b.SecretPath == "/b/2");
        }
        finally
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch
            {
                // ignore
            }
        }
    }

    [Fact]
    public void Follows_appSettings_configSource()
    {
        var dir = Path.Combine(Path.GetTempPath(), "akeyless-cs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var main = Path.Combine(dir, "web.config");
            var child = Path.Combine(dir, "child.config");
            File.WriteAllText(
                main,
                """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <appSettings configSource="child.config" />
                </configuration>
                """);
            File.WriteAllText(
                child,
                """
                <?xml version="1.0" encoding="utf-8"?>
                <appSettings>
                  <add key="X" value="akeyless:///from/child" />
                </appSettings>
                """);

            var bindings = _sut.DiscoverBindings(main);
            Assert.Single(bindings);
            Assert.Equal("X", bindings[0].LogicalKey);
            Assert.Equal("/from/child", bindings[0].SecretPath);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch
            {
                // ignore
            }
        }
    }
}
