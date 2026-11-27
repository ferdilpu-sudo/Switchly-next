# Switchly Next

> **Project phase:** M-009 Release Readiness (`TASK-039`..`042` + Browser-First UI D14/D15, OCI/Midtrans Hybrid Billing D16, Microsoft Store MSIX packaging, & Full Security/Engine Audit complete; **145 desktop + 4 server tests PASS**) — awaiting CEF proprietary-codec artifact (`D13`) & final `TASK-043` release sign-off.  
> **Product name:** Switchly (official name per D8; https://www.switchly.site/).  
> **Purpose:** Windows desktop workspace for managing many isolated web browser profiles, tabs and windows.

## 1. At a glance

Switchly Next is designed to reduce repeated logout/login when working with many website accounts. Each profile uses its own browser storage context. The product is local-first and supports manual encrypted backup/restore.

**MVP:** profile management, isolated browser sessions, groups, Hybrid Integrated Workspace with multi-window/tabs (D7), browser basics, Chrome/Edge data import (bookmarks, history, open-tab URLs), encrypted password vault with CSV import & explicit-click autofill (D6), local security, and manual encrypted backup/restore. Extensions (`REQ-007`, M-005) and scheduled automatic backups (`REQ-017`, D2) remain deferred post-MVP.

## 2. Current technology stack

| Component | Selection | Notes |
|---|---|---|
| Desktop UI | C# / WPF / MVVM | Windows desktop shell (`ADR-001`, Accepted) |
| Runtime | .NET 10 LTS | Windows 11 primary; Windows 10 compatibility requires policy validation (`ADR-002`, Accepted) |
| Embedded browser | `CefSharp.Wpf.NETCore 156.0.300` + CEF `156.0.30` (Chrome Runtime) | Validated in M-001 (`ADR-003`, `ADR-011`, Accepted); isolated strictly in `Switchly.BrowserHost` (D5) |
| Metadata | Plain SQLite + EF Core `10.x` | Non-secret app metadata + AEAD-encrypted `VaultCredential` blobs (D6); no plaintext secrets (`ADR-004`, Accepted) |
| Local security | Windows DPAPI (`CurrentUser`) + optional master password / Windows Hello + AES-256-GCM Vault | Encrypted password vault (`ENT-009`) & CSV import implemented per D6 (`ADR-005`) |
| Portable backup | Manual chunked streaming AES-256-GCM with passphrase KDF | Cross-device restore excludes device-bound Chromium cookies (D2, D3, `ADR-006`) |
| Tests | `xUnit` (`xunit 2.9.3`) | Unit, integration, browser and E2E tests |

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

`Switchly.sln` contains the full desktop solution (`Switchly.App`, `Switchly.Domain`, `Switchly.Application`, `Switchly.Infrastructure`, `Switchly.BrowserHost`) and automated test suites. Build and test from the repository root:

```powershell
dotnet restore .\Switchly.sln
dotnet build .\Switchly.sln -c Release
dotnet test .\Switchly.sln -c Release
dotnet test .\server\Switchly.LicenseApi.Tests\Switchly.LicenseApi.Tests.csproj -c Release
```

M-001 engine spike artifacts live under `artifacts/engine-spike/`; M-008 compatibility and QA evidence lives in [docs/compatibility-and-qa-matrix.md](./docs/compatibility-and-qa-matrix.md).

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
server/
  Switchly.LicenseApi/
  Switchly.LicenseApi.Tests/
packaging/
artifacts/
docs/
```

This is a proposed layout. Keep each source file focused on one primary responsibility; see [rules.md](./rules.md).

## 6. Configuration

No environment variables or service credentials are required for local Free / Beta usage.

Optional GA commercial licensing overrides (`D16`, read by `HttpLicenseApiClient` or `%LOCALAPPDATA%\SwitchlyNext\Data\license-config.json`):
- `SWITCHLY_LICENSE_API_URL` (e.g., `https://api.switchly.site`)
- `SWITCHLY_ED25519_PUBLIC_KEY` (32-byte Base64 Ed25519 public key)
- `SWITCHLY_ENFORCE_LICENSING` (`true` | `false`)

Never put real secrets or private keys in documentation or source control.

## 7. Quality checks

Quality checks (run from the repository root):
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

- Extension support is not a core requirement. Chrome Runtime is the only CEF mode since CEF 128 (Alloy removed); do not promise Chrome Web Store extension parity.
- Profile storage is isolated per direct-child `<profile-guid>` context (`ADR-011`); validated in M-001 on Windows 11.
- Chrome/Edge cookies and login sessions are intentionally not imported (`ADR-010`), and cross-device backup restore does not carry device-bound Chromium cookies (D3).
- Unattended scheduled backups (D2, `REQ-017`) and optional extensions (`REQ-007`, M-005) are deferred post-MVP. (Encrypted password vault & CSV import were reopened and implemented per D6.)
- Commercial billing direction is locked per `D16` (Free max 5 profiles without login, 7-day Pro Trial with email OTP, Midtrans Snap IDR hybrid subscription/perpetual, offline-tolerant Ed25519 tokens); Beta builds remain unlocked by default.
- Official supported OS is Windows 11 x64 (`D8` / `OPEN-002`); Windows 10 is best-effort/unsupported.

## 11. Current next step

Verify proprietary-codec CEF build (`D13`), then build the Microsoft Store MSIX update (`.\packaging\package-msix.ps1`) for Store ID `9PH5FF24TKZR` and/or Portable ZIP & Inno Setup EXE (`.\packaging\package-beta.ps1`) upon Product Owner release sign-off (`M-009` `TASK-043`).
