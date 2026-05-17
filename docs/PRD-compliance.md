# PRD coverage: Akeyless Secrets Provider for Legacy IIS (.NET)

This document maps the repository to **PRD: Akeyless Secrets Provider for Legacy IIS (.NET)**.

## Compliance summary

| PRD area | Status | Notes |
|----------|--------|--------|
| **Target OS** (Windows Server 2016 / 2019 / 2022) | Implemented | Agent runs as **.NET 8** host (Windows Service); IIS apps use Framework / Core samples on supported Windows versions. |
| **Frameworks** (.NET Framework 4.5+, .NET 6/8+) | Partial / documented | Sample **4.7.2** + **.NET 8**; NuGet `akeyless` **2.20.1** → practical **4.6.1+** for Framework. |
| **Input: web.config / app.config** | Implemented | **In-app** discovery (`WebConfigurationManager` / `IConfiguration`); **agent** optional `discover-and-resolve` parses XML + `configSource` / `file` merges under allowlist. |
| **Input: AppSettings / ConnectionStrings** | Implemented | Both stacks + agent discovery. |
| **Input: Environment variables** | Implemented | Env values with `akeyless://` + `AKEYLESS_SECRET_NAMES`. |
| **Recursive configSource / deep FS** | Implemented (agent) | `ConfigurationDiscoveryService` follows common `configSource` and `file` patterns; exotic chains may need extension. |
| **Zero-disk secret values** | Implemented | Resolved values stay in **process memory** (app + agent); not written by this code. |
| **Local Agent: localhost REST** | Implemented | **`Akeyless.IIS.Agent`** — loopback-only Kestrel + middleware; **`Akeyless.Agent.Client`** HTTP client. |
| **Named pipe** | Not implemented | PRD allows REST **or** pipe; REST satisfies the local-channel requirement. Pipe can be added later without changing Gateway semantics. |
| **Memory cache + TTL (agent)** | Implemented | `IMemoryCache` per path in agent (`CacheTtlSeconds`). |
| **High concurrency / pooling** | Partial | Agent reuses **single `V2Api`** and **auth token** in-process; HTTP keep-alive via `HttpClient` defaults. |
| **Startup enrichment / unified configuration** | Implemented | Framework: `EnrichConfigurationAtStartup` patches `ConfigurationManager` + overlay; `AppConfiguration` for static helpers. .NET 8: `AddAkeylessResolvedSecrets` on `IConfiguration`. No custom `SettingsProvider` in app code. |
| **Auth: API Key** | Implemented | Agent + direct path use Access ID / Key. |
| **Auth: CSP IAM, Cert, UID** | Not implemented | Extend `GatewaySecretService` / SDK `Auth` overloads; configuration hooks reserved. |
| **Logging & auditing (no secret values)** | Implemented | Agent + app log **counts/phases**; optional **Windows Event Log** for agent. |
| **Windows Event Viewer / Syslog / ELK** | Partial | **Event Log** wired for agent on Windows; Syslog/ELK via host logging pipeline. |

## Architecture

1. **`Akeyless.IIS.Agent`** (Windows Service): listens on **127.0.0.1** only; holds Gateway credentials; caches; exposes `POST /api/v1/resolve` and `POST /api/v1/discover-and-resolve`.
2. **IIS worker processes**: set **`AKEYLESS_AGENT_URL`** (e.g. `http://127.0.0.1:17890`); no Gateway credentials required in the app pool when using the agent.
3. **Fallback**: omit `AKEYLESS_AGENT_URL` and set **`AKEYLESS_ACCESS_ID` / `AKEYLESS_ACCESS_KEY`** for direct Gateway access (development / air-gapped testing).

## Architect’s note (PRD)

Dynamic rotation: agent **TTL cache** + app **Framework TTL refresh** (`AKEYLESS_CACHE_TTL_SECONDS`) reduce Gateway load; agent centralizes identity and caching for many workers.
