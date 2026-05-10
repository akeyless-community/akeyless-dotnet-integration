# ASP.NET Core 8+

Uses the current **`akeyless`** NuGet client (**5.x**). Discovery matches the PRD **`akeyless://`** pattern in **`IConfiguration`** (e.g. `appsettings.json` + environment variables). See **`examples/net8/appsettings.akeyless.example.json`**.

## Run locally

Set environment variables (example PowerShell):

```powershell
$env:AKEYLESS_ACCESS_ID="..."
$env:AKEYLESS_ACCESS_KEY="..."
# Optional if all secrets use akeyless:// in appsettings / env:
# $env:AKEYLESS_SECRET_NAMES="/path/one;/path/two"
# $env:AKEYLESS_CACHE_TTL_SECONDS="300"
# $env:AKEYLESS_GW_URL="https://your-gateway.example.com:8080/v2"
```

```bash
dotnet run --project src/Akeyless.WebApp.Net8/Akeyless.WebApp.Net8.csproj
```

Open `/health` — the response includes **only a count** of loaded secrets, not names or values.

Use **`AkeylessMemorySecrets.Get("Section:Key")`** with the same logical path as in configuration (colon-separated segments).

## IIS hosting

Host like any ASP.NET Core 8 app (in-process or out-of-process). Configure the same environment variables for the app pool or site.

## Relationship to .NET Framework 4.7.2

Most applications should use **`Akeyless.Bootstrap.Net472`** with **`Global.asax`** and **`AkeylessConfig`**. This project covers the **small set** of **.NET 8+** services.
