# Getting started — Akeyless .NET / IIS integration

This guide walks through **what each part of the repository does** and **how to use it**, step by step. For a quick reference of environment variables and code snippets, see the root [README.md](../README.md).

## Table of contents

1. [Concepts](#1-concepts)
2. [Repository components](#2-repository-components)
3. [Prerequisites](#3-prerequisites)
4. [Step 1 — Clone and verify the build](#4-step-1--clone-and-verify-the-build)
5. [Step 2 — Prepare secrets in Akeyless](#5-step-2--prepare-secrets-in-akeyless)
6. [Step 3 — Run automated tests (no Gateway required)](#6-step-3--run-automated-tests-no-gateway-required)
7. [Step 4 — Install and configure the IIS Agent](#7-step-4--install-and-configure-the-iis-agent)
8. [Step 5 — Integrate a .NET Framework / IIS application](#8-step-5--integrate-a-net-framework--iis-application)
9. [Step 6 — Integrate an ASP.NET Core 8 application](#9-step-6--integrate-an-aspnet-core-8-application)
10. [Step 7 — Try the sample app locally](#10-step-7--try-the-sample-app-locally)
11. [Operating the agent day to day](#11-operating-the-agent-day-to-day)
12. [Troubleshooting](#12-troubleshooting)
13. [Further reading](#13-further-reading)
14. [What has been tested](#14-what-has-been-tested)

---

## 1. Concepts

### Secret references (`akeyless://`)

Configuration files store **pointers** to Akeyless items, not plaintext secrets:

```text
akeyless:///prod/myapp/api-key
```

At startup the integration resolves each pointer to the current secret value. Paths are normalized with a leading `/`.

### Startup enrichment (not a custom provider)

The design goal is **one configuration surface** for application code:

1. **Discover** — find every `akeyless://` value in config (and optional env fallbacks).
2. **Fetch** — call the **local agent** (recommended) or the **Gateway** directly.
3. **Enrich** — override placeholders in memory for the lifetime of the process.

After step 3, developers read settings with **`ConfigurationManager`** (.NET Framework) or **`IConfiguration`** (.NET 8). Feature code does not branch on “Akeyless vs file.”

### The IIS Agent (local sidecar)

On a Windows IIS server, **`Akeyless.IIS.Agent`** runs as a **Windows Service** on **loopback only** (default `http://127.0.0.1:17890`). It:

- Holds **Gateway credentials** and an **in-memory cache**
- Serves **many** IIS app pools on the same machine
- Lets worker processes use **`AKEYLESS_AGENT_URL`** instead of storing API keys on each pool

```text
  IIS worker process(es)
         │
         │  HTTP to 127.0.0.1 (AKEYLESS_AGENT_URL)
         ▼
  Akeyless IIS Agent  ──HTTPS──►  Akeyless Gateway
  (credentials + cache)
```

You do **not** need a separate application pool per app for Akeyless. One agent per server is typical; each pool that uses secrets sets the same agent URL.

---

## 2. Repository components

| Component | Path | Role |
|-----------|------|------|
| **IIS Agent** | `src/Akeyless.IIS.Agent` | Loopback REST service; Gateway auth; cache; optional `web.config` discovery |
| **Agent client** | `src/Akeyless.Agent.Client` | HTTP client used by app libraries to call the agent |
| **Framework bootstrap** | `src/Akeyless.Bootstrap.Net472` | .NET Framework 4.7.2 startup enrichment + `AppConfiguration` |
| **Core sample** | `src/Akeyless.WebApp.Net8` | ASP.NET Core 8 example with `AddAkeylessResolvedSecrets()` |
| **Examples** | `examples/net472/`, `examples/net8/` | `web.config`, `Global.asax`, `appsettings` snippets |
| **Service install notes** | `scripts/install-windows-service.example.md` | Register the agent as a Windows Service |
| **Tests** | `tests/Akeyless.Integration.Tests` | Automated coverage (agent API, discovery, client) |

---

## 3. Prerequisites

| Requirement | Notes |
|-------------|--------|
| **.NET 8 SDK** | Build agent, Core sample, and tests |
| **.NET Framework 4.7.2+** | For IIS / ASP.NET Framework integration |
| **Akeyless Gateway** | SaaS or customer-deployed; reachable from the agent (or from the app in direct mode) |
| **API key** | Access ID + Access Key with **read** on the secret paths you reference |
| **Windows Server + IIS** | Required for production agent-as-service; optional for local Core testing |

---

## 4. Step 1 — Clone and verify the build

```bash
git clone https://github.com/akeyless-community/akeyless-dotnet-integration.git
cd akeyless-dotnet-integration
dotnet build Akeyless.DotNet.Samples.sln -c Release
```

If this succeeds, the solution (agent, libraries, sample app, tests) compiles on your machine.

---

## 5. Step 2 — Prepare secrets in Akeyless

1. Create **static secrets** (or use existing items) whose paths match what you will put in config.

   The repository examples use paths such as:

   - `/prod/legacy-iis/api-shared-key` (see `examples/net8/appsettings.akeyless.example.json`)
   - `/prod/legacy-iis/sql-connection-string`

2. Create or use an **API key** with read permission on those items (or their folder).

3. Note your **Gateway URL** (e.g. `https://api.akeyless.io` or your on-prem gateway).

You will configure these on the **agent** (production) or on the **application** (direct / development mode).

---

## 6. Step 3 — Run automated tests (no Gateway required)

Tests use a **fake gateway** inside the test host. No live Akeyless calls or credentials are needed.

```bash
dotnet test Akeyless.DotNet.Samples.sln -c Release
```

**What this validates:**

- `akeyless://` parsing and path normalization
- Agent endpoints: `/health`, `/health/ready`, `/api/v1/resolve`, `/api/v1/discover-and-resolve`
- Configuration discovery from JSON, XML, and environment variables
- HTTP client serialization for the agent API

CI runs the same command on GitHub Actions (`.github/workflows/dotnet.yml`).

---

## 7. Step 4 — Install and configure the IIS Agent

Use this on a **Windows IIS server** (or run interactively for local testing with `dotnet run`).

### 7.1 What the agent does

| Setting / area | Purpose |
|----------------|---------|
| `AkeylessAgent:GatewayUrl` | Where the agent calls Akeyless |
| `AkeylessAgent:AccessId` / `AccessKey` | Agent’s Gateway credentials |
| `AkeylessAgent:ListenUrl` | Must be **loopback** only (e.g. `http://127.0.0.1:17890`) |
| `AkeylessAgent:CacheTtlSeconds` | How long resolved values stay in the agent cache |
| `AkeylessAgent:AllowedConfigurationRoots` | Directory prefixes allowed for `discover-and-resolve` on `web.config` paths |

Environment variable overrides use double underscores, e.g. `AkeylessAgent__AccessId`.

### 7.2 Publish the agent

```powershell
dotnet publish src/Akeyless.IIS.Agent/Akeyless.IIS.Agent.csproj -c Release -o "C:\Program Files\Akeyless\IIS-Agent"
```

Copy and edit `appsettings.json` next to the executable, or set configuration on the Windows Service.

### 7.3 Install as a Windows Service

Follow [scripts/install-windows-service.example.md](../scripts/install-windows-service.example.md).

After start, verify:

```text
GET http://127.0.0.1:17890/health
```

Expected: JSON with `"status":"ok"` and `"role":"akeyless-iis-agent"` (process liveness only).

Also verify Gateway connectivity and credentials:

```text
GET http://127.0.0.1:17890/health/ready
```

Expected when configured correctly: HTTP **200** with `"status":"healthy"` and `"gateway":"reachable"`.  
If `GatewayUrl` is wrong or credentials fail: HTTP **503** with `"status":"unhealthy"` and `gateway` set to `unreachable`, `auth_failed`, or `missing_credentials` (no secret values in the body).

### 7.4 Agent HTTP API

| Method | Path | Body | Response |
|--------|------|------|----------|
| GET | `/health` | — | **Liveness** (process up; does not check Gateway) |
| GET | `/health/ready` | — | **Readiness** (Auth to Gateway with AccessId/AccessKey; HTTP 200 / 503) |
| POST | `/api/v1/resolve` | `{ "paths": ["/path/a"] }` | `{ "pathToValue": { "/path/a": "..." } }` |
| POST | `/api/v1/discover-and-resolve` | `{ "configurationFilePath": "C:\\...\\web.config" }` | Map of logical config keys to resolved values |

**Security:** only **loopback** clients are accepted; others receive HTTP **403**. Do not log response bodies in production.

More detail: [src/Akeyless.IIS.Agent/README.md](../src/Akeyless.IIS.Agent/README.md).

### 7.5 Configure IIS application pools

For **each application pool** that runs an app with `akeyless://` references:

1. Open **IIS Manager** → **Application Pools** → your pool → **Advanced Settings**.
2. Under **Environment Variables**, add:

   ```text
   AKEYLESS_AGENT_URL = http://127.0.0.1:17890
   ```

3. **Do not** put `AKEYLESS_ACCESS_ID` / `AKEYLESS_ACCESS_KEY` on the pool when using the agent.

4. Recycle the pool after changes.

---

## 8. Step 5 — Integrate a .NET Framework / IIS application

### 8.1 Add the library

Reference project **`Akeyless.Bootstrap.Net472`** from your web application (or replicate its source and the `akeyless` **2.20.1** package reference).

### 8.2 Declare references in `web.config`

Store references, not secret values:

```xml
<appSettings>
  <add key="MyApiKey" value="akeyless:///prod/myapp/api-key" />
</appSettings>
<connectionStrings>
  <add name="DefaultConnection"
       connectionString="akeyless:///prod/myapp/sql-connection-string"
       providerName="System.Data.SqlClient" />
</connectionStrings>
```

See [examples/net472/web.config.snippet.xml](../examples/net472/web.config.snippet.xml).

### 8.3 Enrich at startup (one call)

In `Global.asax.cs`, **before** other startup logic:

```csharp
protected void Application_Start()
{
    Akeyless.Bootstrap.AkeylessFrameworkBootstrapper.EnrichConfigurationAtStartup();
    // routes, DI, etc.
}
```

Template: [examples/net472/Global.asax.cs.example](../examples/net472/Global.asax.cs.example).

**What happens internally:**

1. Scan `web.config` / `ConfigurationManager` / environment for `akeyless://` values.
2. Batch-fetch paths via **`AKEYLESS_AGENT_URL`** (or direct Gateway if unset).
3. Apply resolved values to **`ConfigurationManager`** where supported, plus an in-memory overlay for any keys that cannot be patched in place.

### 8.4 Read configuration in application code

**Option A — `ConfigurationManager` (after enrichment):**

```csharp
var apiKey = ConfigurationManager.AppSettings["MyApiKey"];
var sql = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
```

**Option B — central helper (recommended if you already use one):**

Point your static helper at **`AppConfiguration.Get` / `TryGet`** once. Callers stay unchanged; no Akeyless-specific code in features.

Optional periodic refresh: set **`AKEYLESS_CACHE_TTL_SECONDS`** on the app pool to re-fetch on an interval (.NET Framework only in this sample).

### 8.5 App pool recycle

When IIS recycles the pool, `Application_Start` runs again and secrets are reloaded from Akeyless.

---

## 9. Step 6 — Integrate an ASP.NET Core 8 application

### 9.1 Reference configuration shape

Use `akeyless://` in `appsettings.json` or environment variables:

```json
{
  "Secrets": {
    "ApiKey": "akeyless:///prod/myapp/api-key"
  }
}
```

Example: [examples/net8/appsettings.akeyless.example.json](../examples/net8/appsettings.akeyless.example.json).

### 9.2 Enrich at startup (one call)

Immediately after `WebApplication.CreateBuilder`:

```csharp
using Akeyless.WebApp.Net8;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddAkeylessResolvedSecrets();
var app = builder.Build();
```

**What happens internally:**

1. Build interim configuration from existing sources.
2. Discover all `akeyless://` bindings.
3. Fetch via agent or direct Gateway.
4. Add an **in-memory configuration layer last**, so resolved values override placeholders.

### 9.3 Read configuration in application code

Use **`IConfiguration`**, **`IOptions<T>`**, or injected configuration as you already do:

```csharp
var key = configuration["Secrets:ApiKey"];
```

No separate secret service is required in business logic.

### 9.4 Hosting on IIS

Host the Core app in-process or out-of-process like any ASP.NET Core 8 site. Set **`AKEYLESS_AGENT_URL`** on the application pool (same as Framework). Gateway credentials belong on the **agent**, not the pool.

**Note:** this sample enriches configuration **once at startup**. For periodic refresh on Core, add a hosted service that re-runs resolution (Framework supports `AKEYLESS_CACHE_TTL_SECONDS` out of the box).

---

## 10. Step 7 — Try the sample app locally

This exercises the **agent + Core sample** pattern without IIS.

### Terminal 1 — start the agent

Set agent credentials (use your values; never commit them):

**PowerShell:**

```powershell
$env:AkeylessAgent__GatewayUrl = "https://api.akeyless.io"
$env:AkeylessAgent__AccessId = "<your-access-id>"
$env:AkeylessAgent__AccessKey = "<your-access-key>"
$env:AkeylessAgent__ListenUrl = "http://127.0.0.1:17890"
dotnet run --project src/Akeyless.IIS.Agent/Akeyless.IIS.Agent.csproj
```

Confirm `http://127.0.0.1:17890/health`.

### Terminal 2 — start the sample web app

```powershell
$env:AKEYLESS_AGENT_URL = "http://127.0.0.1:17890"
```

Copy secret paths from `examples/net8/appsettings.akeyless.example.json` into `src/Akeyless.WebApp.Net8/appsettings.Development.json` (paths must exist in your tenant).

```powershell
dotnet run --project src/Akeyless.WebApp.Net8/Akeyless.WebApp.Net8.csproj
```

Open the URL shown in the console (e.g. `http://localhost:5000/health`). The app should start without errors if secrets resolve.

To verify enrichment, inspect configuration in the debugger or add temporary logging of **key names and lengths only**—never log secret values.

### Direct mode (no agent)

For local development without the agent, omit `AKEYLESS_AGENT_URL` and set:

```text
AKEYLESS_ACCESS_ID
AKEYLESS_ACCESS_KEY
AKEYLESS_GW_URL   (optional; defaults to https://api.akeyless.io)
```

on the application process instead.

---

## 11. Operating the agent day to day

| Task | Action |
|------|--------|
| **Rotate Gateway API key** | Update agent service config / env; restart the service |
| **Add a new site** | Add `akeyless://` in config; ensure pool has `AKEYLESS_AGENT_URL`; call startup enrichment |
| **New `web.config` path for discover-and-resolve** | Add directory prefix to `AllowedConfigurationRoots` |
| **Verify agent health** | `GET /health` (liveness) and `GET /health/ready` (Gateway Auth) on loopback |
| **Verify app can resolve** | Recycle pool; confirm app starts; check logs (counts only, no secret values) |

---

## 12. Troubleshooting

| Symptom | Likely cause | What to check |
|---------|--------------|---------------|
| Startup exception about `AKEYLESS_AGENT_URL` or Access ID/Key | App has `akeyless://` but no way to fetch | Set agent URL on pool, or direct Gateway credentials on process |
| HTTP 403 from agent | Client not on loopback | App must call `127.0.0.1` / `localhost`, not server hostname |
| `/health` ok but `/health/ready` unhealthy | Bad `GatewayUrl`, network, or AccessId/AccessKey | Fix agent `AkeylessAgent` settings; readiness reports `unreachable`, `auth_failed`, or `missing_credentials` |
| Secret path not found | Wrong path or ACL | Path in config matches Akeyless item; API key can read it |
| `discover-and-resolve` fails | Path outside allowlist | Add site root to `AllowedConfigurationRoots` |
| Framework app still shows `akeyless://` at runtime | Enrichment did not patch that key | Use `AppConfiguration` in your central helper; verify `EnrichConfigurationAtStartup()` runs first in `Application_Start` |
| Tests fail locally | Missing .NET 8 SDK | Install SDK; run `dotnet test` from repo root |

---

## 13. Further reading

| Document | Contents |
|----------|----------|
| [README.md](../README.md) | Overview, env var table, code snippets |
| [docs/PRD-compliance.md](PRD-compliance.md) | Mapping to the IIS .NET PRD |
| [src/Akeyless.Bootstrap.Net472/README.md](../src/Akeyless.Bootstrap.Net472/README.md) | Framework library details |
| [src/Akeyless.WebApp.Net8/README.md](../src/Akeyless.WebApp.Net8/README.md) | Core sample details |
| [src/Akeyless.IIS.Agent/README.md](../src/Akeyless.IIS.Agent/README.md) | Agent endpoints and configuration |

---

## 14. What has been tested

Automated tests live in **`tests/Akeyless.Integration.Tests`** (xUnit, **23 tests**). They run locally with:

```bash
dotnet test Akeyless.DotNet.Samples.sln -c Release
```

The same command runs on **GitHub Actions** for every push and pull request to `main` / `master` (`.github/workflows/dotnet.yml`, **Ubuntu**, .NET 8 SDK). Tests do **not** call a live Akeyless Gateway; the agent uses an in-memory **fake gateway** during HTTP API tests.

### Covered by automated tests

| Area | What is verified |
|------|------------------|
| **`akeyless://` parsing** | Valid references normalize to paths with a leading `/`; invalid or non-Akeyless URIs are rejected (`SecretReferenceParserTests`). |
| **Agent HTTP API** | `GET /health` (liveness); `GET /health/ready` (healthy / 503 unhealthy for unreachable or auth failure); `POST /api/v1/resolve` (path normalization, batch resolve, empty body); `POST /api/v1/discover-and-resolve` on an allowlisted `web.config` (`AgentApiTests`). |
| **Agent allowlist** | `discover-and-resolve` returns **403** when the config file path is outside `AllowedConfigurationRoots` (`AgentApiTests`, `AllowedPathValidatorTests`). |
| **XML config discovery (agent)** | `appSettings` and `connectionStrings` in a single `web.config`; following **`appSettings configSource`** to an external file (`ConfigurationDiscoveryServiceTests`). |
| **ASP.NET Core config discovery** | Nested `IConfiguration` keys; env vars whose **value** is `akeyless://`; parsing `AKEYLESS_SECRET_NAMES` (`ConfigurationSecretDiscoveryNet8Tests`). |
| **Agent HTTP client** | `AkeylessLocalAgentClient` POST URLs and JSON mapping for `/api/v1/resolve` and `/api/v1/discover-and-resolve` (`AgentClientTests`). |

### Test environment notes

- Agent API tests host the real **`Akeyless.IIS.Agent`** application via **`WebApplicationFactory`**, with **`ASPNETCORE_ENVIRONMENT=Testing`** (loopback middleware is skipped so the test host can call the app).
- Gateway responses are **stubbed** (`FakeGatewaySecretService` returns deterministic values such as `resolved-value-for:/path`).
- Tests run on **Linux/macOS/Windows** wherever .NET 8 is installed; CI currently uses **Ubuntu only**.

### Not covered by automated tests (manual / customer validation)

These paths are implemented in the repository but require **manual** or **environment-specific** verification:

| Area | Suggested manual check |
|------|-------------------------|
| **Live Akeyless Gateway** | Create secrets + API key; run agent and sample app against your Gateway (Steps 7 and 10 above). |
| **Windows Service install** | Publish agent; install with `scripts/install-windows-service.example.md`; confirm service starts and `/health` responds. |
| **IIS application pools** | Set `AKEYLESS_AGENT_URL` on a pool; recycle; confirm Framework/Core app starts and reads resolved config. |
| **.NET Framework enrichment** | `EnrichConfigurationAtStartup()` patching `ConfigurationManager` / `AppConfiguration` on IIS or full Framework runtime. |
| **Loopback-only enforcement** | Confirm non-loopback callers receive **403** from the agent (production middleware; disabled in test environment). |
| **Agent memory cache / TTL** | Observe cache behavior under load or after `CacheTtlSeconds` expires (not asserted in unit tests). |
| **Framework TTL refresh** | `AKEYLESS_CACHE_TTL_SECONDS` periodic re-fetch on .NET Framework. |
| **Exotic `web.config` chains** | Deep or unusual `configSource` / `file` merge patterns beyond the tested `appSettings configSource` case. |
| **Auth methods beyond API key** | CSP IAM, certificate, UID (not implemented in this sample). |

If you extend the integration, add tests under `tests/Akeyless.Integration.Tests` and keep **`dotnet test Akeyless.DotNet.Samples.sln -c Release`** green before merging.

---

## Security reminders

- Never commit Access Keys or production secrets to source control.
- Resolved values live in **process memory** only in this sample; do not write them to disk.
- Do not log secret values, connection strings, or full agent resolve responses in production.
