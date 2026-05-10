# Akeyless secrets for .NET Framework and .NET 8

Sample code for loading secrets from the Akeyless Gateway at application startup and keeping resolved values in process memory only. You declare **references** (not plaintext secrets) using the `akeyless://` URI scheme in configuration or environment variables; the library authenticates, calls the Gateway, and maps results to logical keys your code can read.

## How it works

1. **Discovery** — At startup, the library finds every configuration value that looks like `akeyless:///path/to/item` (plus optional `AKEYLESS_SECRET_NAMES`).
2. **Fetch** — It authenticates with `AKEYLESS_ACCESS_ID` / `AKEYLESS_ACCESS_KEY` and retrieves those items in one batch.
3. **Resolve** — Each reference is replaced in memory only. The **logical key** used at runtime is:
   - the **appSettings key** name, or
   - `ConnectionStrings:{name}` for connection strings, or
   - the **environment variable name** if the reference came from the environment, or
   - the **secret path** itself when using `AKEYLESS_SECRET_NAMES` only.

Paths are normalized with a leading `/` (for example `akeyless:///prod/db/password` → `/prod/db/password`).

## Requirements

- Network path from the host to Akeyless SaaS or your Gateway (`AKEYLESS_GW_URL`).
- Access ID and Access Key available to the process (for example IIS app pool environment variables).
- **.NET Framework:** 4.7.2 (or 4.6.1+) and NuGet package `akeyless` **2.20.1** (last line targeting `netstandard2.0` for Framework).
- **.NET 8:** current `akeyless` package as referenced by the sample project.

## Environment variables

| Variable | Description |
|----------|-------------|
| `AKEYLESS_GW_URL` | Gateway base URL (default `https://api.akeyless.io`). |
| `AKEYLESS_ACCESS_ID` | Access ID. |
| `AKEYLESS_ACCESS_KEY` | Access Key. |
| `AKEYLESS_SECRET_NAMES` | Optional fallback: `/path/one;/path/two` when you are not using `akeyless://` in config. Logical key equals the path. |
| `AKEYLESS_CACHE_TTL_SECONDS` | Optional; set to a positive number of seconds to periodically re-fetch secrets in memory (useful when values rotate). |

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

### 4. Read secrets in code

```csharp
string apiKey = Akeyless.Bootstrap.AkeylessConfig.Get("MyApiKey");
string sql = Akeyless.Bootstrap.AkeylessConfig.Get("ConnectionStrings:DefaultConnection");

if (Akeyless.Bootstrap.AkeylessConfig.TryGet("OptionalKey", out var value))
{
    // use value
}
```

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

### 2. Startup

The sample registers a singleton, loads secrets after the host is built, then runs the app:

```csharp
builder.Services.AddSingleton<AkeylessMemorySecrets>();

var app = builder.Build();

var secrets = app.Services.GetRequiredService<AkeylessMemorySecrets>();
secrets.LoadFromAkeyless(app.Configuration);
```

### 3. Read secrets

Inject `AkeylessMemorySecrets` (or resolve from `HttpContext.RequestServices` in minimal APIs):

```csharp
app.MapGet("/example", (AkeylessMemorySecrets s) =>
{
    var key = s.Get("Secrets:ApiKey");
    return Results.Ok("ok"); // do not return the secret
});
```

Optional refresh: set `AKEYLESS_CACHE_TTL_SECONDS`. The sample disposes the refresh timer when the application stops.

## Build this repository

```bash
dotnet build Akeyless.DotNet.Samples.sln -c Release
```

Run the Core sample locally (set env vars first):

```bash
dotnet run --project src/Akeyless.WebApp.Net8/Akeyless.WebApp.Net8.csproj
```

Then open `/health` — it returns only how many secrets were loaded, not names or values.

## Repository layout

- `src/Akeyless.Bootstrap.Net472` — .NET Framework library.
- `src/Akeyless.WebApp.Net8` — ASP.NET Core 8 sample.
- `examples/` — `web.config`, `Global.asax`, JSON, and optional trace listener examples.
- `docs/PRD-compliance.md` — Requirement mapping for the Legacy IIS PRD.

## Security note

Do not log secret values, connection strings, or raw API error bodies in production.

## License

Reference sample only; use and distribute according to your organization’s policies.
