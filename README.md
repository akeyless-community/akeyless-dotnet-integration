# Akeyless secrets for .NET Framework and .NET 8

Sample code for loading secrets from the Akeyless Gateway at application startup and keeping resolved values in process memory only. You declare **references** (not plaintext secrets) using the `akeyless://` URI scheme in configuration or environment variables; the library authenticates, calls the Gateway, and **enriches** configuration so application code can keep using **one** configuration surface.

## How it works

1. **Discovery** — At startup, the library finds every configuration value that looks like `akeyless:///path/to/item` (plus optional `AKEYLESS_SECRET_NAMES`).
2. **Fetch** — Either the **local IIS Agent** (`AKEYLESS_AGENT_URL`, recommended for production) resolves paths over **loopback HTTP**, or each process calls the **Gateway** directly using `AKEYLESS_ACCESS_ID` / `AKEYLESS_ACCESS_KEY`.
3. **Enrich** — Resolved values override the placeholders for consumers:
   - **.NET 8:** an in-memory configuration layer is added on top of `appsettings.json` / environment so **`IConfiguration`**, **`IOptions<T>`**, and **`builder.Configuration`** see the **resolved** string—same keys as before, no parallel “secret API” in feature code.
   - **.NET Framework:** resolved values are merged in **`AkeylessConfig`**, which reads **Akeyless first**, then **`ConfigurationManager`** (`AppSettings` / `ConnectionStrings`). Use **`AkeylessConfig`** (or a thin wrapper that delegates to it) as the **single** read path for settings that may be backed by Akeyless or plain XML.

Logical keys for discovery match runtime lookup:

- **appSettings** key name (for example `MyApiKey`),
- **`ConnectionStrings:{name}`** for connection strings,
- **environment variable name** when the env **value** is an `akeyless://` reference,
- the **secret path** when using **`AKEYLESS_SECRET_NAMES`** only.

Paths are normalized with a leading `/` (for example `akeyless:///prod/db/password` → `/prod/db/password`).

## Windows IIS Agent (PRD)

The **`Akeyless.IIS.Agent`** project is a **.NET 8** executable intended to run as a **Windows Service** on IIS servers. It:

- Listens only on **loopback** (default `http://127.0.0.1:17890`; validated at startup).
- Authenticates to the **Akeyless Gateway** and maintains an **in-memory cache** with TTL (`AkeylessAgent:CacheTtlSeconds`).
- Exposes **`POST /api/v1/resolve`** (batch paths → values) and **`POST /api/v1/discover-and-resolve`** (parse an allowlisted `web.config` path and resolve all `akeyless://` entries).

**IIS application pools** should set **`AKEYLESS_AGENT_URL`** to the agent base URL. They **do not** need `AKEYLESS_ACCESS_ID` / `AKEYLESS_ACCESS_KEY` when the agent is used.

Publish the agent, configure `appsettings.json` (or environment variables such as `AkeylessAgent__AccessId`), install as a service — see **`scripts/install-windows-service.example.md`**.

## Requirements

- **With agent:** IIS worker must reach **loopback** to the agent URL; the **agent** host must reach **`AkeylessAgent:GatewayUrl`**.
- **Without agent:** process must reach **`AKEYLESS_GW_URL`** and expose **`AKEYLESS_ACCESS_ID`** / **`AKEYLESS_ACCESS_KEY`** (for example on the app pool).
- **.NET Framework:** 4.7.2 (or 4.6.1+) and NuGet package `akeyless` **2.20.1** (last line targeting `netstandard2.0` for Framework).
- **.NET 8:** current `akeyless` package as referenced by the sample projects.

## Environment variables

| Variable | Description |
|----------|-------------|
| `AKEYLESS_AGENT_URL` | **Recommended:** base URL of the local agent (e.g. `http://127.0.0.1:17890`). When set, **Gateway credentials are not required** on the app pool. |
| `AKEYLESS_GW_URL` | Gateway base URL for **direct** mode (no agent). Default `https://api.akeyless.io`. |
| `AKEYLESS_ACCESS_ID` | Access ID (direct mode only, unless agent URL is unset). |
| `AKEYLESS_ACCESS_KEY` | Access Key (direct mode only). |
| `AKEYLESS_SECRET_NAMES` | Optional fallback: `/path/one;/path/two` when you are not using `akeyless://` in config. Logical key equals the path. |
| `AKEYLESS_CACHE_TTL_SECONDS` | Optional **(.NET Framework library only)**; if set to a positive number of seconds, periodically re-fetches secrets into the in-memory overlay (see `AkeylessFrameworkBootstrapper`). |

## Usage: .NET Framework 4.7.2 (IIS / ASP.NET)

### 1. Add the library

Reference the `Akeyless.Bootstrap.Net472` project from your web application, or add the same package reference (`akeyless` 2.20.1) and copy the source files if you prefer a single project.

### 2. Declare references in `web.config`

Store **references**, not secret values:

```xml
<configuration>
  <appSettings>
    <add key="MyApiKey" value="akeyless:///prod/myapp/api-key" />
  </appSettings>
  <connectionStrings>
    <add name="DefaultConnection"
         connectionString="akeyless:///prod/myapp/sql-connection-string"
         providerName="System.Data.SqlClient" />
  </connectionStrings>
</configuration>
```

More patterns: `examples/net472/web.config.snippet.xml`.

### 3. Load at application start

In `Global.asax.cs`, call the bootstrapper before the rest of your startup logic:

```csharp
protected void Application_Start()
{
    Akeyless.Bootstrap.AkeylessFrameworkBootstrapper.LoadSecretsAtStartup();
    // Register routes, DI, etc.
}
```

See `examples/net472/Global.asax.cs.example` for a paste-friendly template.

### 4. Read configuration (single surface)

**`ConfigurationManager` is not rewritten in place** (that is unsupported for most keys). After bootstrap, use **`AkeylessConfig`**, which returns the resolved secret when the key was an `akeyless://` reference and otherwise returns the same value **`ConfigurationManager`** would expose for plain settings:

```csharp
string apiKey = Akeyless.Bootstrap.AkeylessConfig.GetAppSetting("MyApiKey");
string sql = Akeyless.Bootstrap.AkeylessConfig.GetConnectionString("DefaultConnection");

// Or unified key form (connection strings use the ConnectionStrings: prefix):
string sql2 = Akeyless.Bootstrap.AkeylessConfig.Get("ConnectionStrings:DefaultConnection");

if (Akeyless.Bootstrap.AkeylessConfig.TryGetAppSetting("OptionalKey", out var value))
{
    // use value
}
```

Point existing static configuration helpers at **`AkeylessConfig`** (or delegate to it) so callers never distinguish Akeyless-backed keys from ordinary keys.

After an IIS app pool recycle, `Application_Start` runs again and secrets are reloaded.

### 5. Environment-only fallback

If you cannot put `akeyless://` in XML, set `AKEYLESS_SECRET_NAMES` to a list of full paths. Keys in memory will match those paths (with a leading `/`).

## Usage: ASP.NET Core 8

### 1. Configuration shape

Use `akeyless://` in `appsettings.json` (or environment variables). Nested keys use `:` when reading:

```json
{
  "Secrets": {
    "ApiKey": "akeyless:///prod/myapp/api-key"
  },
  "ConnectionStrings": {
    "DefaultConnection": "akeyless:///prod/myapp/sql-connection-string"
  }
}
```

Example file: `examples/net8/appsettings.akeyless.example.json`.

### 2. Startup (one line)

Call **`AddAkeylessResolvedSecrets`** immediately after **`WebApplication.CreateBuilder`**. It reads the current configuration, resolves all `akeyless://` values from the Gateway, and registers an **in-memory** configuration source **last**, so resolved values **override** placeholders for the rest of the host lifetime:

```csharp
using Akeyless.WebApp.Net8;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddAkeylessResolvedSecrets();

var app = builder.Build();
```

Optional: pass an **`ILogger`** into the overload for structured diagnostics (counts only).

### 3. Consume configuration normally

Use **`IConfiguration`**, **`IOptions<T>`**, or **`builder.Configuration`** as you already do—no injected secret service in application code:

```csharp
app.MapGet("/example", (IConfiguration config) =>
{
    var key = config["Secrets:ApiKey"];
    return Results.Ok("ok"); // do not return the secret
});
```

If there are **no** `akeyless://` bindings, the Gateway is not called and startup continues unchanged.

**TTL note:** periodic refresh is implemented on the **.NET Framework** bootstrapper (`AKEYLESS_CACHE_TTL_SECONDS`). The **.NET 8** enrichment path in this sample is **one-shot** at host build; add a hosted refresh if you need the same behavior on Core.

## Tests

```bash
dotnet test Akeyless.DotNet.Samples.sln -c Release
```

`tests/Akeyless.Integration.Tests` (xUnit) covers:

- **IIS Agent HTTP API** (`/health`, `/api/v1/resolve`, `/api/v1/discover-and-resolve`) using `WebApplicationFactory` with a **fake** gateway (no real Akeyless calls).
- **`Akeyless.Agent.Client`** HTTP serialization against a stub handler.
- **`SecretReferenceParser`**, **`AllowedPathValidator`**, **`ConfigurationDiscoveryService`** (XML + `configSource`), and **ASP.NET Core `ConfigurationSecretDiscovery`**.

GitHub Actions runs the same command on **push** and **pull request** (`.github/workflows/dotnet.yml`).

## Build this repository

```bash
dotnet build Akeyless.DotNet.Samples.sln -c Release
```

Run the Core sample locally:

```bash
dotnet run --project src/Akeyless.WebApp.Net8/Akeyless.WebApp.Net8.csproj
```

Open `/health` for a trivial JSON response (no secret material).

## Repository layout

- `src/Akeyless.IIS.Agent` — **Windows Service** host: loopback REST, Gateway auth, cache, optional XML discovery.
- `src/Akeyless.Agent.Client` — **netstandard2.0** HTTP client for the agent (referenced by Framework + Core samples).
- `src/Akeyless.Bootstrap.Net472` — .NET Framework library.
- `src/Akeyless.WebApp.Net8` — ASP.NET Core 8 sample.
- `scripts/` — example Windows service install notes.
- `examples/` — `web.config`, `Global.asax`, JSON, optional trace listener.
- `tests/Akeyless.Integration.Tests` — xUnit tests + CI scenarios for agent, client, and discovery helpers.

## Security note

Do not log secret values, connection strings, or raw API error bodies in production.

## License

Reference sample only; use and distribute according to your organization’s policies.
