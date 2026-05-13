# Install Akeyless IIS Agent as a Windows Service (example)

Run **elevated** PowerShell on the IIS server after publishing `Akeyless.IIS.Agent`:

```powershell
$bin = "C:\Program Files\Akeyless\IIS-Agent\Akeyless.IIS.Agent.exe"
sc.exe create AkeylessIISAgent binPath= "`"$bin`"" start= auto DisplayName= "Akeyless IIS Agent"
sc.exe description AkeylessIISAgent "Local loopback proxy for Akeyless secret resolution on IIS hosts."
sc.exe start AkeylessIISAgent
```

- Copy `appsettings.json` next to the executable and set **`AkeylessAgent:AccessId`**, **`AccessKey`**, **`GatewayUrl`**, **`AllowedConfigurationRoots`**, and **`ListenUrl`** (must remain loopback, e.g. `http://127.0.0.1:17890`).
- Alternatively, set environment variables on the service (for example `AkeylessAgent__AccessId`) so secrets are not stored in plain files.

Each IIS application pool should set **`AKEYLESS_AGENT_URL`** to the agent base URL (for example `http://127.0.0.1:17890`). Gateway credentials are **not** required on the app pool when using the agent.

Remove the service:

```powershell
sc.exe stop AkeylessIISAgent
sc.exe delete AkeylessIISAgent
```
