# Akeyless secrets for .NET Framework and .NET 8

Sample code for loading secrets from the Akeyless Gateway at application startup and holding them in memory only. Secrets are referenced with the `akeyless://` URI scheme in configuration or environment variables, then resolved over HTTPS (no local agent in this repository).

## Features

- Discovery of `akeyless://` references in `web.config` / `app.config` (appSettings and connectionStrings, including typical merged `configSource` layouts on IIS), in ASP.NET Core configuration, and in environment values.
- In-memory storage of resolved values; optional periodic refresh via `AKEYLESS_CACHE_TTL_SECONDS`.
- .NET Framework 4.7.2 library with `AkeylessConfig.Get` for reads; ASP.NET Core 8 sample using `AkeylessMemorySecrets`.

## Requirements

- Windows IIS or another host able to reach your Akeyless SaaS endpoint or Gateway over HTTPS.
- Gateway authentication credentials for the process (this sample uses Access ID and Access Key).
- .NET Framework 4.7.2 (or 4.6.1+) for the legacy library; the pinned `akeyless` NuGet client for Framework targets `netstandard2.0`. .NET 8 for the Core sample uses the current `akeyless` package.

## Environment variables

| Variable | Description |
|----------|-------------|
| `AKEYLESS_GW_URL` | Gateway base URL (default `https://api.akeyless.io`). |
| `AKEYLESS_ACCESS_ID` | Access ID. |
| `AKEYLESS_ACCESS_KEY` | Access Key. |
| `AKEYLESS_SECRET_NAMES` | Optional list of secret paths (semicolon or comma separated) if not all secrets use `akeyless://` in config. |
| `AKEYLESS_CACHE_TTL_SECONDS` | Optional; if greater than zero, secrets are refreshed in memory on that interval. |

## Layout

- `src/Akeyless.Bootstrap.Net472` — Framework library; call `AkeylessFrameworkBootstrapper.LoadSecretsAtStartup()` from `Application_Start` (see `examples/net472/`).
- `src/Akeyless.WebApp.Net8` — Minimal ASP.NET Core 8 app with a `/health` endpoint that reports only how many secrets were loaded.
- `docs/PRD-compliance.md` — Mapping to the Legacy IIS product requirements document.

## Build

```bash
dotnet build Akeyless.DotNet.Samples.sln -c Release
```

Do not log secret values or raw API responses that may contain sensitive data.

## License

Reference sample only; use and distribute according to your organization’s policies.
