using System.Net;
using System.Text.Json;
using Akeyless.Agent.Client;
using Akeyless.IIS.Agent;
using Akeyless.IIS.Agent.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService(options => options.ServiceName = "Akeyless IIS Agent");

builder.Services
    .AddOptions<AgentOptions>()
    .Bind(builder.Configuration.GetSection(AgentOptions.SectionName))
    .Validate(o =>
    {
        if (string.IsNullOrWhiteSpace(o.ListenUrl))
        {
            return false;
        }

        try
        {
            var u = new Uri(o.ListenUrl);
            if (!u.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
                (!IPAddress.TryParse(u.Host, out var addr) || !IPAddress.IsLoopback(addr)))
            {
                return false;
            }
        }
        catch
        {
            return false;
        }

        return true;
    }, "ListenUrl must be a loopback URL (127.0.0.1 or localhost).")
    .ValidateOnStart();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IGatewaySecretService, GatewaySecretService>();
builder.Services.AddSingleton<ConfigurationDiscoveryService>();
builder.Services.AddSingleton<AllowedPathValidator>();

if (OperatingSystem.IsWindows())
{
    builder.Logging.AddEventLog(settings => settings.SourceName = "Akeyless IIS Agent");
}

var agentSection = builder.Configuration.GetSection(AgentOptions.SectionName);
var listenUrl = agentSection["ListenUrl"] ?? "http://127.0.0.1:17890";
builder.WebHost.UseUrls(listenUrl);

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    app.Use(async (context, next) =>
    {
        var remote = context.Connection.RemoteIpAddress;
        if (remote is null || !IPAddress.IsLoopback(remote))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(context).ConfigureAwait(false);
    });
}

var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

app.MapGet("/health", () => Results.Ok(new { status = "ok", role = "akeyless-iis-agent" }));

app.MapPost("/api/v1/resolve", async (ResolveByPathsRequest req, IGatewaySecretService gw, CancellationToken ct) =>
{
    if (req.Paths == null || req.Paths.Count == 0)
    {
        return Results.Json(new ResolveByPathsResponse());
    }

    var normalized = req.Paths
        .Where(p => !string.IsNullOrWhiteSpace(p))
        .Select(p => p.Trim().StartsWith("/", StringComparison.Ordinal) ? p.Trim() : "/" + p.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    var dict = await gw.ResolvePathsAsync(normalized, ct).ConfigureAwait(false);
    return Results.Json(new ResolveByPathsResponse { PathToValue = new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase) }, json);
});

app.MapPost(
    "/api/v1/discover-and-resolve",
    async (DiscoverAndResolveRequest req, AllowedPathValidator pathValidator, ConfigurationDiscoveryService discovery, IGatewaySecretService gw, CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(req.ConfigurationFilePath))
        {
            return Results.BadRequest(new { error = "ConfigurationFilePath is required." });
        }

        if (!pathValidator.IsConfigurationFileAllowed(req.ConfigurationFilePath, out var err))
        {
            return Results.Json(new { error = err }, statusCode: StatusCodes.Status403Forbidden);
        }

        var bindings = discovery.DiscoverBindings(req.ConfigurationFilePath);
        if (bindings.Count == 0)
        {
            return Results.Json(new DiscoverAndResolveResponse());
        }

        var uniquePaths = bindings.Select(b => b.SecretPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var pathValues = await gw.ResolvePathsAsync(uniquePaths, ct).ConfigureAwait(false);
        var logical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (logicalKey, secretPath) in bindings)
        {
            if (!pathValues.TryGetValue(secretPath, out var val))
            {
                return Results.Problem("Missing value for path: " + secretPath);
            }

            logical[logicalKey] = val;
        }

        return Results.Json(new DiscoverAndResolveResponse { LogicalKeyToValue = logical }, json);
    });

app.Run();

public partial class Program
{
}
