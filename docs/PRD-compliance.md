# PRD coverage: Akeyless Secrets Provider for Legacy IIS (.NET)

This document maps the repository to **PRD: Akeyless Secrets Provider for Legacy IIS (.NET)** (Product PRD). The PRD describes a **service-resident Windows Agent** plus a **NuGet provider**; this repo ships a **provider-style library and sample** that can run **directly against the Akeyless Gateway** today, while leaving room to swap the transport for a **localhost agent** later.

## Compliance summary

| PRD area | Status | Notes |
|----------|--------|--------|
| **Target OS** (Windows Server 2016 / 2019 / 2022) | Documented | IIS + .NET Framework workloads assumed; validate in your images. |
| **Frameworks** (.NET Framework 4.5+, .NET 6/8+) | Partial / documented | Sample targets **4.7.2** and **.NET 8**. NuGet `akeyless` **2.20.1** requires **netstandard2.0** → practical minimum **.NET Framework 4.6.1+** (see README). |
| **Input: web.config / app.config** | Implemented | Discovery via `WebConfigurationManager` when ASP.NET-hosted; uses framework-merged sections (includes typical **`configSource`** merges). |
| **Input: AppSettings / ConnectionStrings** | Implemented | Scans both for values prefixed with **`akeyless://`**. |
| **Input: Environment variables** | Implemented | Fallback: any env var whose **value** is an `akeyless://` reference; plus **`AKEYLESS_SECRET_NAMES`** list. |
| **Recursive configSource / deep FS** | Partial | Relies on **ConfigurationManager / WebConfigurationManager** merge behavior; exotic multi-hop patterns may need extra work. |
| **Zero-disk secret values** | Implemented | Resolved values stay in **process memory** only (not written by this code). |
| **Local Agent: localhost REST / named pipe** | Not in repo | **Gap vs full PRD.** Current transport: **HTTPS to Gateway** from the app process. Same discovery and `AkeylessConfig` API can later call `http://127.0.0.1:...` when the Agent ships. |
| **`AkeylessConfig` / unified configuration reads** | Implemented | **Framework:** `AkeylessConfig` merges resolved secrets with **`ConfigurationManager`** (single read API). **.NET 8:** `AddAkeylessResolvedSecrets` enriches **`IConfiguration`** via in-memory overrides; normal `IConfiguration` / `IOptions` consumption. |
| **In-memory cache + configurable TTL** | Partial | Optional **`AKEYLESS_CACHE_TTL_SECONDS`** on **.NET Framework** bootstrapper only. **.NET 8** sample is one-shot enrich at startup (extend with a hosted service if you need periodic refresh). |
| **Auth: API Key** | Implemented | `AKEYLESS_ACCESS_ID` / `AKEYLESS_ACCESS_KEY`. |
| **Auth: CSP IAM, Cert, UID** | Not implemented | Extend with additional `Auth` overloads from the Akeyless SDK or Gateway-side identity; tracked as product follow-up. |
| **Connection pooling to Gateway** | SDK / runtime | Handled by the underlying HTTP stack / RestSharp in the official client; no extra pooling layer in this sample. |
| **Logging & auditing (no secret values)** | Implemented | Trace/source messages and structured logs emit **counts and phases only**, not values. |
| **Windows Event Viewer / Syslog / ELK** | Documented | Use **`Trace`** listeners or `ILogger` sinks in deployment; no dedicated Event Log writer in-sample. |
| **High concurrency** | Documented | Reads are in-memory after load; refresh runs on a **single timer** with a lock—tune TTL and consider Agent offload for extreme scale. |

## Architect’s note (PRD)

The PRD states that on Windows/IIS, **dynamic rotation** for legacy monoliths favors an **in-memory cache provider** because env injection often implies recycle. This repo’s **TTL refresh** on the **.NET Framework** path is a step in that direction; a **long-lived Agent** with a localhost API remains the **full** PRD end state for centralizing cache and policy.
