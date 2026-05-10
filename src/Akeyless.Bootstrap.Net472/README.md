# .NET Framework 4.7.2 (IIS / ASP.NET)

Implements PRD-oriented behavior for **legacy IIS**: discover **`akeyless://`** references in **web.config** (including typical **`configSource`** merges via `WebConfigurationManager`), **`ConfigurationManager`**, and **environment variables**, then fetch from the **Akeyless Gateway** into **memory only**.

## Package constraint

This project references **`akeyless` 2.20.1** ( **`netstandard2.0`** ), the last line compatible with **.NET Framework**. Newer **`akeyless` 5.x** packages target **`net6.0`** only.

## Developer API (PRD)

- **`AkeylessFrameworkBootstrapper.LoadSecretsAtStartup()`** — call from **`Global.asax`** **`Application_Start`**.
- **`AkeylessConfig.Get("LogicalKey")`** / **`TryGet`** — read resolved secrets (e.g. appSettings key or `ConnectionStrings:Name`).

## Environment variables

| Variable | Purpose |
|----------|---------|
| `AKEYLESS_GW_URL` | Gateway base URL (default `https://api.akeyless.io`). |
| `AKEYLESS_ACCESS_ID` / `AKEYLESS_ACCESS_KEY` | API-key style authentication to the Gateway. |
| `AKEYLESS_SECRET_NAMES` | Optional list of paths if you are not using `akeyless://` references in XML/env. |
| `AKEYLESS_CACHE_TTL_SECONDS` | Optional; if **> 0**, periodically re-fetches secrets in memory (rotation-friendly). |

## Configuration examples

See repository **`examples/net472/web.config.snippet.xml`**. Optional **`TRACE.example.config`** for routing **`AkeylessIntegrationLog`** to listeners (no secret values are written by the sample logger calls).

## Recycle behavior

IIS **app pool recycle** clears memory; **`Application_Start`** runs again and secrets are reloaded.
