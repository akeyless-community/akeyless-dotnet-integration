using Akeyless.IIS.Agent.Services;
using Xunit;

namespace Akeyless.Integration.Tests;

public sealed class SecretReferenceParserTests
{
    [Theory]
    [InlineData("akeyless:///prod/x", "/prod/x")]
    [InlineData("akeyless://prod/x", "/prod/x")]
    [InlineData("  AKEYLESS:////deep/path  ", "//deep/path")]
    public void TryParsePath_normalizes(string input, string expectedPath)
    {
        Assert.True(SecretReferenceParser.TryParsePath(input, out var path));
        Assert.Equal(expectedPath, path);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://vault/x")]
    [InlineData("akeyless://")]
    public void TryParsePath_rejects(string input)
    {
        Assert.False(SecretReferenceParser.TryParsePath(input, out _));
    }
}
