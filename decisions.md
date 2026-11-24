# Decisions / ADR Log — Switchly Next

**Status:** DRAFT  
**Tanggal dokumen:** 23 November 2026

## 1. Decision index

| ADR | Status | Decision | Related requirement | Revisit trigger |
|---|---|---|---|---|
| ADR-001 | Proposed | WPF + MVVM desktop shell | REQ-001, REQ-005 | Accessibility or shell limitations discovered |
| ADR-002 | Proposed | .NET 10 LTS | REQ-001, REQ-014 | Win10 Home/Pro support requirement conflicts |
| ADR-003 | Validated | C# + WPF + CEF (Chrome Runtime) through CefSharp | REQ-001, REQ-003, REQ-005 | Priority-site sign-in fails in M-002 |
| ADR-004 | Proposed | SQLite for app metadata; CEF owns browser storage | REQ-002, REQ-006 | Local data volume/performance demands |
| ADR-005 | Proposed | DPAPI local key wrapping + optional master password/Hello unlock | REQ-010 | Threat model or account recovery issues |
| ADR-006 | Proposed | AES-GCM encrypted portable backup | REQ-011 | KDF performance/interoperability |
| ADR-007 | Proposed | Local structured logs, secrets redacted | NFR security | Privacy review requests telemetry |
| ADR-008 | Open | MSIX vs signed setup installer | REQ-014 | Installation/update PoC |
| ADR-009 | Confirmed scope | No registration for basic use; Pro login only for upgrade/activation | REQ-012, REQ-013 | Product business model evolves |
| ADR-010 | Confirmed scope | No source cookies/session import | REQ-009 | Product owner explicitly expands scope |
| ADR-011 | Validated | Profile dirs are direct children of RootCachePath | REQ-003 | CEF profile-path rules change |

## 2. ADR-001 — WPF desktop shell

- **Status:** Proposed.
- **Context:** Windows-only desktop app, many profiles, multi-window, keyboard-centric management.
- **Options:**
  - WPF + MVVM: mature Windows desktop patterns, flexible control and window management; native Windows-only.
  - WinUI 3: modern Windows UI, but browser control integration and ecosystem PoC required.
  - Electron: mature web stack, but user wants lightweight app and native Windows integration; memory footprint is a concern.
  - Tauri: lighter shell candidate, but adding CEF as a separate browser runtime and managing native interop may erase simplicity.
- **Decision:** Start with WPF + MVVM as the candidate shell.
- **Consequences:** Use presentation ViewModels and app services; no business logic in code-behind.
- **Revisit when:** Browser embedding or DPI/accessibility tests demonstrate material blockers.

## 3. ADR-002 — .NET runtime

- **Status:** Proposed.
- **Context:** Need modern supported C# runtime and CefSharp WPF integration.
- **Options:** .NET 10 LTS; legacy .NET Framework 4.8.1; native C++ shell.
- **Decision:** Target .NET 10 LTS for the candidate implementation, subject to OS compatibility gate.
- **Consequences:** .NET 10 is listed as active LTS through 14 November 2028. Microsoft currently lists Windows 10 support for modern .NET only on Enterprise/LTSC editions; Windows 10 Home/Pro needs best-effort validation or a changed support policy.
- **Revisit when:** Product owner requires a vendor-supported target on Windows 10 Home/Pro, or CefSharp support changes.
- **Reference:** [Microsoft .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy); [Windows install support matrix](https://learn.microsoft.com/en-us/dotnet/core/install/windows).

## 4. ADR-003 — Embedded browser engine

- **Status:** Validated by M-001 (isolation TEST-003/004/017, browser behavior TEST-021, resource baseline, provider public pages). Provider sign-in remains unvalidated — first M-002 gate.
- **Context:** Switchly Next needs one integrated desktop UI with embedded Chromium browsing, per-profile storage isolation, tabs, and managed windows. The product owner explicitly excludes WebView2. Extension support is not a priority.
- **Options:**
  - CefSharp + CEF (Chrome Runtime): direct WPF integration and Chromium embedding; CEF lifecycle, native packaging, security patching, and website compatibility remain engineering responsibilities.
  - Rust + CEF bindings: native memory-safety advantages in application code, but a different Windows UI integration path and a higher implementation burden for the current WPF concept.
  - Launch installed Chrome: best Chrome fidelity but violates the requirement for an integrated browser surface.
  - WebView2: excluded by explicit product-owner decision; do not add it as a fallback.
- **Decision:** Use C# + WPF + CefSharp/CEF as the current implementation proposal. CEF 156+ ships **Chrome Runtime exclusively** (the Alloy bootstrap was removed in CEF 128), so Chrome Runtime is the baseline runtime rather than an option to avoid. The earlier "standard CEF vs Chrome Runtime" distinction is obsolete. What remains deferred and optional is the use of Chrome Runtime's **extension APIs** and any Chrome Web Store parity — neither is an MVP requirement. Validate the chosen CefSharp/CEF version and four priority websites in M-001.
- **Consequences:** The first technical gate tests browser embedding, persistent profile isolation, popups, downloads, video/WebGL, provider sign-in behavior, and resource use. Extensions remain optional. A website that rejects embedded user agents must not be bypassed; document the limitation and request a product decision if it blocks a priority workflow.
- **Revisit when:** CefSharp/CEF cannot meet basic integrated browser requirements, supported target Windows versions, or the required website workflows.
- **Related:** `REQ-001`, `REQ-003`, `REQ-005`, `REQ-007`, `AC-003`, `TEST-003`, `M-001`.

## 5. ADR-004 — Data ownership

- **Status:** Proposed.
- **Context:** Need app metadata and browser web storage without corrupting Chromium internal state.
- **Options:** One large SQLite DB for everything; use CEF profile directories plus a separate app DB; external DB server.
- **Decision:** Separate application metadata in SQLite from browser data in unique CEF profile storage directories.
- **Consequences:** Browser engine owns cookie/localStorage/cache/indexedDB; application DB owns profile names/groups/bookmarks/history projection/tab restore and workflow metadata.
- **Revisit when:** Data migration or backup needs prove this separation insufficient.

## 6. ADR-005 — Local security and unlock

- **Status:** Proposed.
- **Context:** User requested Windows security, master password, and Windows Hello where available.
- **Options:** Windows account protection only; application master password only; combined local DPAPI key wrapping plus optional passphrase and Windows Hello unlock.
- **Decision:** Use DPAPI CurrentUser to wrap the local vault key. Add optional master-password protection and Windows Hello as local unlock experience if the API PoC is successful.
- **Consequences:** DPAPI local key does not make a portable backup; portable backup gets a separate passphrase-derived key. Windows Hello is not itself the backup encryption.
- **Revisit when:** Account recovery, Hello API, or multi-user Windows scenario changes.

## 7. ADR-006 — Portable backup cryptography

- **Status:** Proposed.
- **Context:** Backup must move to another computer.
- **Options:** Plain ZIP; Windows DPAPI-only; authenticated encryption with passphrase KDF.
- **Decision:** Encrypt the archive using AES-256-GCM with a password-derived key and versioned KDF parameters. Local secrets remain DPAPI wrapped.
- **Consequences:** A lost passphrase means the backup cannot be decrypted. KDF implementation and parameters require independent security review and performance test.
- **Revisit when:** FIPS requirement, recovery requirements, or audited cryptography package changes.

## 8. ADR-007 — Diagnostics

- **Status:** Proposed.
- **Context:** Browser errors and memory use need diagnostics, but history and credentials are private.
- **Decision:** Structured local logs by default; redact secrets, cookies, credentials, URL query strings, and personal file paths. No cloud telemetry until explicit opt-in policy is approved.
- **Revisit when:** Product owner approves privacy notice and telemetry model.

## 9. ADR-008 — Installer and update channel

- **Status:** Open.
- **Options:** MSIX; signed EXE/MSI installer; portable ZIP.
- **Proposed default:** Signed installer with per-user install option, subject to testing with CEF runtime files, VC++ prerequisites, auto-update, and code-signing budget.
- **Owner:** Product owner + engineering.
- **Impact:** Affects update mechanism, installer size, SmartScreen reputation and support.
- **Next validation:** Build install/uninstall/upgrade PoC in M-001 or M-002.

## 10. ADR-009 — Basic use without registration

- **Status:** Confirmed by user.
- **Decision:** App can create and use local profiles without registration. Sign-in/account is required only for purchase or activation of Pro features.
- **Consequences:** No remote auth dependency for core Free workflow; exact Pro benefits, provider, payment and entitlement server remain open.

## 11. ADR-010 — Do not import source cookies/session

- **Status:** Confirmed by user selection.
- **Decision:** Import list includes bookmarks, history, open tabs as URLs, and passwords from a user-exported CSV; an extension inventory may be informative only. Source cookies/session are not imported.
- **Consequences:** Imported profiles must sign in to websites again. This lowers risk of silently copying device-bound or sensitive sessions.

## 12. ADR-011 — Profile storage layout under Chrome Runtime

- **Status:** Validated (verified by M-001 TASK-002).
- **Context:** CEF 156 runs Chrome Runtime exclusively. In Chrome Runtime, a request context's `CachePath` is accepted only as a **direct child** of `CefSettings.RootCachePath`; a deeper path is rejected (`Cannot create profile at path`) and CEF silently falls back to an in-memory (off-the-record) profile — persistence fails with no obvious error. Verified by the M-001 TASK-002 spike.
- **Decision:** Set `CefSettings.RootCachePath = %LOCALAPPDATA%\SwitchlyNext\Profiles`; each profile's `RequestContextSettings.CachePath = %LOCALAPPDATA%\SwitchlyNext\Profiles\<profile-guid>` (a direct child). This preserves the documented per-profile path.
- **Consequences:** `Profiles` doubles as CEF's user-data dir, so it also holds CEF global state (an unused "Default" profile + cache). Backups must copy only `<profile-guid>` directories, never CEF global state. Empty `CachePath` = in-memory only, never for account browsing.
- **Revisit when:** CEF changes its profile-path rules, or the product prefers a differently-named root (e.g. `BrowserData`).

## 13. Open decisions with impact

| ID | Missing decision | Assumption for draft | Impact | Owner / next step |
|---|---|---|---|---|
| OPEN-001 | Free vs Pro limits/benefits | No artificial quotas in prototype | Subscription model and UX | Product owner defines after product prototype |
| OPEN-002 | Windows 10 Home/Pro support guarantee | Best-effort only | Public minimum OS and support workload | Product owner decides before public beta |
| OPEN-003 | Optional extension scope | Extensions are not an MVP priority | Post-MVP feature scope | Reopen only if product owner reprioritizes |
| OPEN-004 | Product branding | Switchly Next working name | Package identity, domain, icon | Product owner |
| OPEN-005 | License/account provider | Not integrated in MVP | Pro activation flow | Product owner after package definition |
| OPEN-006 | Backup schedule/retention | Daily/7-snapshot proposal | Disk usage and user expectation | Product owner |
| OPEN-007 | Installer technology/signing | Signed per-user installer proposal | Distribution and cost | Product owner before beta |
| OPEN-008 | M-001 test harness type | Console app + CefSharp.OffScreen.NETCore at `artifacts/engine-spike/` | PoC project structure | Confirmed 2026-11-24 |

### Provider identity clarification (2026-11-24)

Priority provider compatibility targets confirmed with exact URLs:
- **Google Flow**: https://flow.google
- **ChatGPT**: https://chatgpt.com
- **Dola**: https://dola.com
- **SeaArt**: https://seaart.ai
