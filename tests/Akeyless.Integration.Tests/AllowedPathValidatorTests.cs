using Akeyless.IIS.Agent;
using Akeyless.IIS.Agent.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Akeyless.Integration.Tests;

public sealed class AllowedPathValidatorTests
{
    [Fact]
    public void Allows_file_under_configured_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "akeyless-allow-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var opts = Options.Create(
                new AgentOptions { AllowedConfigurationRoots = new List<string> { root } });
            var v = new AllowedPathValidator(opts, NullLogger<AllowedPathValidator>.Instance);
            var file = Path.Combine(root, "sub", "web.config");
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "<configuration/>");

            Assert.True(v.IsConfigurationFileAllowed(file, out var err), err);
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch
            {
                // ignore
            }
        }
    }

    [Fact]
    public void Rejects_when_no_roots_configured()
    {
        var opts = Options.Create(new AgentOptions { AllowedConfigurationRoots = new List<string>() });
        var v = new AllowedPathValidator(opts, NullLogger<AllowedPathValidator>.Instance);
        Assert.False(v.IsConfigurationFileAllowed(Path.Combine(Path.GetTempPath(), "any-web.config"), out _));
    }

    [Fact]
    public void Rejects_path_outside_root()
    {
        var a = Path.Combine(Path.GetTempPath(), "akeyless-siteA-" + Guid.NewGuid().ToString("N"));
        var b = Path.Combine(Path.GetTempPath(), "akeyless-siteB-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(a);
        Directory.CreateDirectory(b);
        try
        {
            var opts = Options.Create(new AgentOptions { AllowedConfigurationRoots = new List<string> { a } });
            var v = new AllowedPathValidator(opts, NullLogger<AllowedPathValidator>.Instance);
            var file = Path.Combine(b, "web.config");
            File.WriteAllText(file, "<configuration/>");
            Assert.False(v.IsConfigurationFileAllowed(file, out _));
        }
        finally
        {
            try
            {
                Directory.Delete(a, true);
                Directory.Delete(b, true);
            }
            catch
            {
                // ignore
            }
        }
    }
}
