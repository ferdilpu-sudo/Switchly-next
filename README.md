# Switchly Next

> **Project phase:** Specification only — no application code has been implemented or tested.  
> **Product name:** Switchly Next (working name).  
> **Purpose:** Windows desktop workspace for managing many isolated web browser profiles, tabs and windows.

## 1. At a glance

Switchly Next is designed to reduce repeated logout/login when working with many website accounts. Each profile uses its own browser storage context. The product is local-first and supports manual/automatic encrypted backup.

**MVP:** profile management, isolated browser sessions, groups, integrated multi-window/tabs, browser basics, Chrome/Edge data import, local security, and encrypted backup/restore. Extensions are optional and deferred.

## 2. Current technology proposal

| Component | Candidate | Notes |
|---|---|---|
| Desktop UI | C# / WPF / MVVM | Windows desktop shell |
| Runtime | .NET 10 LTS | Windows 11 primary; Windows 10 compatibility requires policy validation |
| Embedded browser | CefSharp + standard CEF runtime | Candidate; pin exact compatible versions after M-001 |
| Metadata | SQLite + EF Core | Application metadata only |
| Local vault key | Windows DPAPI | Current Windows user |
| Optional unlock | Master password / Windows Hello | Exact unlock flow requires implementation spike |
| Portable backup | AES-256-GCM with passphrase-derived key | Key derivation parameters must be reviewed |
| Tests | xUnit or NUnit (choose at scaffold) | Unit, integration, browser and E2E tests |

Technology rationale and risks: [architecture.md](./architecture.md), [decisions.md](./decisions.md).

## 3. Prerequisites

Planned development environment:
- Windows 11 x64 (preferred).
- Visual Studio 2026 or a current supported Visual Studio with .NET desktop/WPF workload.
- .NET 10 SDK.
- Git.
- Access to the selected CefSharp/CEF release and its required native runtime/VC++ redistributables.

The Windows 10 target needs explicit qualification. Microsoft currently lists .NET 10 support on Windows 10 Enterprise/LTSC editions, not consumer Home/Pro; Windows 10 Home/Pro also passed regular support on 14 October 2025. See:
- [Microsoft .NET Windows install support matrix](https://learn.microsoft.com/en-us/dotnet/core/install/windows)
- [Windows 10 lifecycle](https://learn.microsoft.com/en-us/lifecycle/products/windows-10-home-and-pro)

## 4. Getting started

This handoff contains specifications, not a runnable repository. The following commands are **Direncanakan — verifikasi setelah scaffolding**.

```powershell
git clone <repository-url>
cd SwitchlyNext
dotnet restore
dotnet build .\Switchly.sln -c Release
dotnet test .\Switchly.sln -c Release
```

Do not assume a solution file, NuGet package lock, installer or command exists until the agent creates and verifies it. M-001 must be completed before the full app scaffolding proceeds.

## 5. Repository orientation (target structure)

```text
src/
  Switchly.App/
  Switchly.Domain/
  Switchly.Application/
  Switchly.Infrastructure/
  Switchly.BrowserHost/
tests/
  Switchly.Domain.Tests/
  Switchly.Application.Tests/
  Switchly.Infrastructure.Tests/
  Switchly.E2E.Tests/
artifacts/
docs/
```

This is a proposed layout. Keep each source file focused on one primary responsibility; see [rules.md](./rules.md).

## 6. Configuration

No environment variables or service credentials are required for the local Free MVP.

Potential future Pro configuration names only (not yet selected/implemented):
- `SWITCHLY_NEXT_LICENSE_API_BASE_URL`
- `SWITCHLY_NEXT_AUTH_CLIENT_ID`

Do not add these settings until the account/licensing provider is selected. Never put real secrets in documentation or source control.

## 7. Quality checks

Planned — verify once scaffolding exists:
```powershell
dotnet format .\Switchly.sln --verify-no-changes
dotnet build .\Switchly.sln -c Release
dotnet test .\Switchly.sln -c Release
```

CEF-specific tests require Windows and a valid runtime build. Refer to [testing.md](./testing.md).

## 8. AI development workflow

This repository is configured for Antigravity and includes a workspace-scoped EGM plugin snapshot at `.agents/plugins/egm/`. Read [AGENTS.md](./AGENTS.md) first and follow [EGM Antigravity Setup](./docs/EGM-ANTIGRAVITY-SETUP.md) and [EGM Workflow Map](./docs/EGM-WORKFLOW-MAP.md). EGM is a methodology layer; the project documents remain the source of truth.

The original Switchly repository is reference-only. This generation starts from a clean solution and a newly designed WPF UI; WebView2 is excluded.

## 9. Documentation map

- [PRD](./prd.md) — product scope and acceptance requirements.
- [Design](./design.md) — screens, flows, UI states and interaction.
- [Architecture](./architecture.md) — runtime, modules and trust boundaries.
- [Schema](./schema.md) — SQLite entities, validation and contracts.
- [Decisions](./decisions.md) — ADRs and open decisions.
- [Rules](./rules.md) — enforceable coding, security and quality rules.
- [Testing](./testing.md) — test cases and release gates.
- [Plan](./plan.md) — milestones and task order.
- [Agent instructions](./AGENTS.md) — required workflow for AI coding agents.

## 10. Important known limitations

- Extension support is not a core requirement. Do not require CEF Chrome Runtime or promise Chrome Web Store parity.
- Profile storage is isolated per context; test this before any product work.
- Chrome/Edge cookies and login sessions are intentionally not imported.
- Passwords are only imported from a CSV file explicitly exported and selected by the user; plaintext CSV handling needs strong warnings.
- Free/Pro feature limits, pricing, payment and license provider are unresolved.
- Windows 10 Home/Pro support status needs a product policy decision.

## 11. Current next step

Start with **M-001: CEF browser and provider compatibility proof-of-concept**. Do not build the full profile manager until the engine demonstrates isolated persistent contexts, integrated browser behavior, compatibility evidence for priority websites, and measured resource behavior.

No tests have run. No production readiness or compatibility claim is made.
