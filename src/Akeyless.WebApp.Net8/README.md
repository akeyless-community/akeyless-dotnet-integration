# ASP.NET Core 8+

Uses the current **`akeyless`** NuGet client (**5.x**). **`builder.Configuration.AddAkeylessResolvedSecrets()`** resolves every **`akeyless://`** value against the Gateway and adds an **in-memory** configuration layer so **`IConfiguration`** / **`IOptions<T>`** see the final strings—no separate secret service in feature code.

See **`examples/net8/appsettings.akeyless.example.json`** for JSON shape.

## Run locally

Set environment variables when you use `akeyless://` references (example PowerShell):

```powershell
$env:AKEYLESS_ACCESS_ID="..."
$env:AKEYLESS_ACCESS_KEY="..."
# Optional:
# $env:AKEYLESS_SECRET_NAMES="/path/one;/path/two"
# $env:AKEYLESS_GW_URL="https://your-gateway.example.com:8080/v2"
```

```bash
dotnet run --project src/Akeyless.WebApp.Net8/Akeyless.WebApp.Net8.csproj
```

Open `/health` for a simple status payload (no secret material).

## IIS hosting

Host like any ASP.NET Core 8 app (in-process or out-of-process). Configure the same environment variables for the app pool or site.

## Relationship to .NET Framework 4.7.2

Use **`Akeyless.Bootstrap.Net472`** with **`Global.asax`** and **`AkeylessConfig`** (merged reads). This project is the **.NET 8** counterpart using **`ConfigurationManager.AddInMemoryCollection`** via **`AddAkeylessResolvedSecrets`**.
