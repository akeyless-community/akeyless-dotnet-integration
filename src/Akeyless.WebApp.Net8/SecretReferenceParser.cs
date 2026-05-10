namespace Akeyless.WebApp.Net8;

/// <summary>
/// PRD-style secret references: <c>akeyless:///path/to/secret</c>.
/// </summary>
public static class SecretReferenceParser
{
    public const string Scheme = "akeyless://";

    public static bool TryParsePath(string? value, out string secretPath)
    {
        secretPath = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (!trimmed.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = trimmed[Scheme.Length..].Trim();
        if (rest.Length == 0)
        {
            return false;
        }

        secretPath = rest.StartsWith('/') ? rest : "/" + rest;
        return true;
    }
}
