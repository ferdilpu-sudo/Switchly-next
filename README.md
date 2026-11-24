# Switchly Next

> **Project phase:** M-007 & D6 complete — M-001..M-004, M-006, M-007 (DPAPI, master-password unlock, chunked AES-256-GCM backup/restore), and D6 (Encrypted Password Vault `ENT-009`, Chrome/Edge CSV import `TASK-028`, explicit-click autofill; 92/92 tests PASS) verified; ready for M-008 (Hardening & QA).  
> **Product name:** Switchly Next (working name).  
> **Purpose:** Windows desktop workspace for managing many isolated web browser profiles, tabs and windows.

## 1. At a glance

Switchly Next is designed to reduce repeated logout/login when working with many website accounts. Each profile uses its own browser storage context. The product is local-first and supports manual encrypted backup/restore.

**MVP:** profile management, isolated browser sessions, groups, integrated multi-window/tabs, browser basics, Chrome/Edge data import (bookmarks, history, open-tab URLs), local security, and manual encrypted backup/restore. Extensions (`REQ-007`), CSV password vault (D1), and scheduled automatic backups (D2) are deferred post-MVP.

## 2. Current technology stack

| Component | Selection | Notes |
|---|---|---|
| Desktop UI | C# / WPF / MVVM | Windows desktop shell (`ADR-001`, Accepted) |
| Runtime | .NET 10 LTS | Windows 11 primary; Windows 10 compatibility requires policy validation (`ADR-002`, Accepted) |
| Embedded browser | `CefSharp.Wpf.NETCore 156.0.300` + CEF `156.0.30` (Chrome Runtime) | Validated in M-001 (`ADR-003`, `ADR-011`, Accepted); isolated strictly in `Switchly.BrowserHost` (D5) |
| Metadata | Plain SQLite + EF Core `10.x` | Non-secret application metadata only (`ADR-004`, Accepted) |
| Local security | Windows DPAPI (`CurrentUser`) + optional master password / Windows Hello | Custom password vault deferred post-MVP (D1, `ADR-005`) |
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

`Switchly.sln` is scaffolded under M-002 (`TASK-007`–`009`). Verify the commands below as part of completing `TASK-010` and M-002 cleanup:

```powershell
dotnet restore .\Switchly.sln
dotnet build .\Switchly.sln -c Release
dotnet test .\Switchly.sln -c Release
```

M-001 engine spike artifacts live under `artifacts/engine-spike/`. Do not start `M-003` until `M-002` cleanup (`Switchly.BrowserHost` isolation, `Directory.Packages.props`, and `TASK-010` build/test verification) passes.

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

- Extension support is not a core requirement. Chrome Runtime is the only CEF mode since CEF 128 (Alloy removed); do not promise Chrome Web Store extension parity.
- Profile storage is isolated per direct-child `<profile-guid>` context (`ADR-011`); validated in M-001 on Windows 11.
- Chrome/Edge cookies and login sessions are intentionally not imported (`ADR-010`), and cross-device backup restore does not carry device-bound Chromium cookies (D3).
- Password CSV import & custom credential vault (D1) and unattended scheduled backups (D2) are deferred post-MVP.
- Free/Pro feature limits, pricing, payment and license provider are unresolved (`OPEN-001`, `OPEN-005`).
- Windows 10 Home/Pro support status needs a product policy decision (`OPEN-002`).

## 11. Current next step

Start **M-006: Browser import** (`TASK-023`–`TASK-026`: Chrome/Edge read-only discovery, preview, bookmark/history/open-tab URL import; no cookies/sessions, CSV password vault deferred per D1).
