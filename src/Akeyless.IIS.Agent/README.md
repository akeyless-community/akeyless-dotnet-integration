# Akeyless IIS Agent

Runs on a Windows IIS host as a **Windows Service** (or `dotnet run` for development). Binds **only to loopback**; holds **Gateway** credentials and **cached** secret values so IIS worker processes do not need outbound access or API keys.

## Configuration

See `appsettings.json` section **`AkeylessAgent`**. Override with environment variables (`AkeylessAgent__GatewayUrl`, `AkeylessAgent__AccessId`, …).

**`AllowedConfigurationRoots`** must list full directory prefixes (for example `C:\\inetpub\\wwwroot`) before **`POST /api/v1/discover-and-resolve`** is permitted.

## Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/health` | **Liveness** — process is up (does not check Gateway). |
| GET | `/health/ready` | **Readiness** — authenticates to the Gateway with configured AccessId/AccessKey. Returns HTTP **200** when healthy, **503** when unhealthy. Response includes `gateway` (`reachable`, `unreachable`, `auth_failed`, `missing_credentials`) and a safe `detail` string (no secrets). Result is cached briefly (~15s) to avoid Auth spam. |
| POST | `/api/v1/resolve` | Body: `{ "paths": ["/item/a"] }` → `{ "pathToValue": { ... } }`. |
| POST | `/api/v1/discover-and-resolve` | Body: `{ "configurationFilePath": "C:\\...\\web.config" }` → logical keys to values. |

Non-loopback clients receive **403**.

Example readiness responses:

```json
{ "status": "healthy", "role": "akeyless-iis-agent", "gateway": "reachable", "detail": "Gateway authentication succeeded." }
```

```json
{ "status": "unhealthy", "role": "akeyless-iis-agent", "gateway": "unreachable", "detail": "Gateway host could not be resolved (check GatewayUrl)." }
```
