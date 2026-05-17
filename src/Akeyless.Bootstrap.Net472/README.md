# .NET Framework 4.7.2 (IIS / ASP.NET)

Implements PRD-oriented behavior for **legacy IIS**: discover **`akeyless://`** references in **web.config** (including typical **`configSource`** merges via `WebConfigurationManager`), **`ConfigurationManager`**, and **environment variables**, then fetch from the **local agent** or **Gateway** and **enrich configuration at startup**.

## Package constraint

This project references **`akeyless` 2.20.1** ( **`netstandard2.0`** ), the last line compatible with **.NET Framework**. Newer **`akeyless` 5.x** packages target **`net6.0`** only.

## Developer API

- **`AkeylessFrameworkBootstrapper.EnrichConfigurationAtStartup()`** — call once from **`Global.asax`** **`Application_Start`** (alias: **`LoadSecretsAtStartup()`**).
- After enrichment, application code uses **`ConfigurationManager`** as today, or a single static helper that delegates to **`AppConfiguration`** (`Get` / `TryGet` / `GetAppSetting` / `GetConnectionString`). No per-feature Akeyless branching.
- **`AkeylessConfig`** — obsolete alias of **`AppConfiguration`** for older samples.

## Environment variables

| Variable | Purpose |
|----------|---------|
| `AKEYLESS_AGENT_URL` | **Recommended:** local IIS agent base URL (no Gateway credentials on the app pool). |
| `AKEYLESS_GW_URL` | Gateway base URL (default `https://api.akeyless.io`) for direct mode. |
| `AKEYLESS_ACCESS_ID` / `AKEYLESS_ACCESS_KEY` | API-key authentication (direct mode only). |
| `AKEYLESS_SECRET_NAMES` | Optional list of paths if you are not using `akeyless://` references in XML/env. |
| `AKEYLESS_CACHE_TTL_SECONDS` | Optional; if **> 0**, periodically re-fetches and re-enriches configuration (rotation-friendly). |

## Configuration examples

See repository **`examples/net472/web.config.snippet.xml`**. Optional **`TRACE.example.config`** for routing **`AkeylessIntegrationLog`** to listeners (no secret values are written by the sample logger calls).

## Recycle behavior

IIS **app pool recycle** clears memory; **`Application_Start`** runs again and configuration is re-enriched.
