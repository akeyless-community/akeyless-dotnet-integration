# Akeyless IIS Agent

Runs on a Windows IIS host as a **Windows Service** (or `dotnet run` for development). Binds **only to loopback**; holds **Gateway** credentials and **cached** secret values so IIS worker processes do not need outbound access or API keys.

## Configuration

See `appsettings.json` section **`AkeylessAgent`**. Override with environment variables (`AkeylessAgent__GatewayUrl`, `AkeylessAgent__AccessId`, …).

**`AllowedConfigurationRoots`** must list full directory prefixes (for example `C:\\inetpub\\wwwroot`) before **`POST /api/v1/discover-and-resolve`** is permitted.

## Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/health` | Liveness. |
| POST | `/api/v1/resolve` | Body: `{ "paths": ["/item/a"] }` → `{ "pathToValue": { ... } }`. |
| POST | `/api/v1/discover-and-resolve` | Body: `{ "configurationFilePath": "C:\\...\\web.config" }` → logical keys to values. |

Non-loopback clients receive **403**.
