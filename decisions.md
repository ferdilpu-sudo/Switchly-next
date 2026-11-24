# Decisions / ADR Log — Switchly Next

**Status:** ACTIVE — core M-001/M-002 ADRs accepted; remaining decisions tracked below  
**Date:** 2026-11-24

## 1. Decision index

| ADR | Status | Decision | Related requirement | Revisit trigger |
|---|---|---|---|---|
| ADR-001 | Accepted | WPF + MVVM desktop shell | REQ-001, REQ-005 | Accessibility or shell limitations discovered |
| ADR-002 | Accepted | .NET 10 LTS | REQ-001, REQ-014 | Win10 Home/Pro support requirement conflicts |
| ADR-003 | Accepted | C# + WPF + CEF (Chrome Runtime) through CefSharp 156.0.300 | REQ-001, REQ-003, REQ-005, REQ-007 | Provider compatibility or isolation regresses |
| ADR-004 | Accepted | Plain SQLite for non-secret app metadata; CEF owns browser storage | REQ-002, REQ-006 | Local data volume/performance or whole-DB encryption demands |
| ADR-005 | Proposed | DPAPI local protection + optional master password/Hello unlock (custom password vault deferred post-MVP) | REQ-010 | Threat model, Hello API PoC, or vault reprioritization |
| ADR-006 | Proposed | Manual AES-256-GCM chunked portable backup; device-bound cookies excluded; scheduled backup deferred post-MVP | REQ-011 | KDF performance, restore PoC, or scheduled-backup reprioritization |
| ADR-007 | Accepted | Local structured logs, secrets redacted; no telemetry without opt-in | NFR-SEC | Privacy review requests telemetry |
| ADR-008 | Open | MSIX vs signed setup installer | REQ-014 | Installation/update PoC |
| ADR-009 | Accepted | No registration for basic use; Pro login only for upgrade/activation | REQ-012, REQ-013 | Product business model evolves |
| ADR-010 | Accepted | No source cookies/session import; CSV password vault deferred post-MVP | REQ-008, REQ-009 | Product owner explicitly expands scope |
| ADR-011 | Accepted | Profile dirs are direct children of RootCachePath; backups exclude CEF global state | REQ-003, REQ-011 | CEF profile-path rules change |

## 2. ADR-001 — WPF desktop shell

- **Status:** Accepted (2026-11-24).
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

- **Status:** Accepted (2026-11-24).
- **Context:** Need modern supported C# runtime and CefSharp WPF integration.
- **Options:** .NET 10 LTS; legacy .NET Framework 4.8.1; native C++ shell.
- **Decision:** Target .NET 10 LTS for the candidate implementation, subject to OS compatibility gate.
- **Consequences:** .NET 10 is listed as active LTS through 14 November 2028. Microsoft currently lists Windows 10 support for modern .NET only on Enterprise/LTSC editions; Windows 10 Home/Pro needs best-effort validation or a changed support policy.
- **Revisit when:** Product owner requires a vendor-supported target on Windows 10 Home/Pro, or CefSharp support changes.
- **Reference:** [Microsoft .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy); [Windows install support matrix](https://learn.microsoft.com/en-us/dotnet/core/install/windows).

## 4. ADR-003 — Embedded browser engine

- **Status:** Accepted (2026-11-24 — validated by M-001 + M-002 manual sign-in gate). Isolation (`TEST-003`/`004`/`017`), browser behavior (`TEST-021`), resource baseline (`TEST-016`), and provider public pages + manual sign-in (all four providers; evidence in `artifacts/engine-spike/task-002..005-results.md`) pass on Windows 11. Non-elevated WPF hosting stress checks and Windows 10 checks remain tracked in M-002/M-008.
- **Context:** Switchly Next needs one integrated desktop UI with embedded Chromium browsing, per-profile storage isolation, tabs, and managed windows. The product owner explicitly excludes WebView2. Extension support is not a priority.
- **Options:**
  - CefSharp + CEF (Chrome Runtime): direct WPF integration and Chromium embedding; CEF lifecycle, native packaging, security patching, and website compatibility remain engineering responsibilities.
  - Rust + CEF bindings: native memory-safety advantages in application code, but a different Windows UI integration path and a higher implementation burden for the current WPF concept.
  - Launch installed Chrome: best Chrome fidelity but violates the requirement for an integrated browser surface.
  - WebView2: excluded by explicit product-owner decision; do not add it as a fallback.
- **Decision:** Use C# + WPF + `CefSharp.Wpf.NETCore 156.0.300` (CEF 156.0.30 + Chromium 156.0.8078.96), isolated strictly behind `Switchly.BrowserHost`. CEF 156+ ships **Chrome Runtime exclusively** (the Alloy bootstrap was removed in CEF 128), so Chrome Runtime is the baseline runtime. What remains deferred and optional is the use of Chrome Runtime's **extension APIs** and any Chrome Web Store parity — neither is an MVP requirement.
- **Consequences:** All CefSharp/CEF calls stay in `Switchly.BrowserHost` (never referenced directly by `Switchly.App`). Extensions remain optional. A website that rejects embedded user agents must not be bypassed; document the limitation and request a product decision if it blocks a priority workflow.
- **Revisit when:** CefSharp/CEF cannot meet basic integrated browser requirements, supported target Windows versions, or the required website workflows.
- **Related:** `REQ-001`, `REQ-003`, `REQ-005`, `REQ-007`, `AC-003`, `TEST-003`, `M-001`.

## 5. ADR-004 — Data ownership

- **Status:** Accepted (2026-11-24).
- **Context:** Need app metadata and browser web storage without corrupting Chromium internal state.
- **Options:** One large SQLite DB for everything; plain SQLite for non-secret app metadata + CEF profile directories; SQLCipher whole-DB encryption; external DB server.
- **Decision:** Use plain SQLite (EF Core 10.x) for non-secret application metadata (`switchly-next.db`, protected by Windows user ACLs) and separate unique CEF profile storage directories for browser runtime data. No website passwords, cookies, or OAuth tokens are stored in `switchly-next.db` in MVP.
- **Consequences:** Browser engine owns cookie/localStorage/cache/IndexedDB; application DB owns profile names/groups/bookmarks/history projection/tab restore and workflow metadata. Avoids SQLCipher startup/key-management complexity in MVP.
- **Revisit when:** Data migration, threat model, or backup needs require whole-DB encryption.

## 6. ADR-005 — Local security and unlock

- **Status:** Proposed (scope updated 2026-11-24 per Product Owner decision D1).
- **Context:** User requested Windows security, optional master password, and Windows Hello where available. Custom CSV password vault (`ENT-009`) is deferred to post-MVP so MVP avoids maintaining a separate password manager.
- **Options:** Windows account protection only; application master password only; Windows DPAPI protection plus optional master-password and Windows Hello local app unlock.
- **Decision:** Protect local app secrets/unlock state with Windows DPAPI (`CurrentUser`), and provide optional master-password and Windows Hello local unlock if the API PoC succeeds in M-007. Custom CSV password import and `ENT-009 VaultCredential` are deferred to post-MVP (if later enabled, must use unique-nonce AEAD single-payload encryption with AAD binding `(CryptoVersion, CredentialId, ProfileId, Origin)` and a dedicated Vault UI).
- **Consequences:** DPAPI local protection is device/user-bound and does not make a portable backup; portable backup uses a separate passphrase-derived key. Windows Hello is an unlock UX, not backup encryption.
- **Revisit when:** Account recovery, Hello API, multi-user Windows scenario, or post-MVP password vault scope is reopened.

## 7. ADR-006 — Portable backup cryptography

- **Status:** Proposed (scope updated 2026-11-24 per Product Owner decisions D2 & D3).
- **Context:** Backup must move profiles and metadata to another Windows computer safely. Chromium encrypts profile `Cookies` with a Windows DPAPI-wrapped `os_crypt` key in `RootCachePath\Local State`, which is not portable across machines.
- **Options:** Plain ZIP; Windows DPAPI-only; manual streaming chunked AES-256-GCM archive with passphrase KDF; hybrid public-key envelope encryption for unattended scheduled backups.
- **Decision:** MVP supports **manual** portable backups encrypted with a **chunked streaming AES-256-GCM** archive format (header magic/version, KDF parameters + salt, deterministic per-chunk nonces with final-chunk flag and header digest in AAD to prevent OOM and truncation attacks). Backups copy only `<profile-guid>` directories (excluding CEF global state and regenerable caches: `Cache`, `Code Cache`, `GPUCache`, `GrShaderCache`, `DawnCache`, `Crashpad`). Browser login cookies/sessions are device-bound and excluded on cross-device restore (consistent with `REQ-009`). Restore uses a **restart-to-restore staged directory swap** executed before `Cef.Initialize()` and EF Core open file handles. Unattended scheduled encrypted backups (`REQ-017`, `TASK-032`) are deferred to post-MVP pending hybrid public-key envelope design.
- **Consequences:** A lost passphrase means the backup cannot be decrypted; restored profiles on a new PC require signing in to websites again. KDF algorithm/parameters (`Argon2id` or `PBKDF2-HMAC-SHA256`) require security review and performance testing before M-007.
- **Revisit when:** FIPS requirement, recovery requirements, same-machine session backup, or scheduled backup is reprioritized.

## 8. ADR-007 — Diagnostics

- **Status:** Accepted (2026-11-24).
- **Context:** Browser errors and memory use need diagnostics, but history and credentials are private.
- **Options:** Structured local rolling logs with default redaction; remote crash/telemetry ingestion; no file logging.
- **Decision:** Structured local logs by default (`Microsoft.Extensions.Logging`); redact secrets, cookies, credentials, URL query strings, and personal file paths. No cloud telemetry until explicit opt-in policy is approved.
- **Consequences:** Keeps diagnostics private and offline; users share redacted logs manually when reporting issues.
- **Revisit when:** Product owner approves privacy notice and telemetry model.

## 9. ADR-008 — Installer and update channel

- **Status:** Open.
- **Options:** MSIX; signed EXE/MSI installer; portable ZIP.
- **Proposed default:** Signed installer with per-user install option, subject to testing with CEF runtime files, VC++ prerequisites, auto-update, and code-signing budget.
- **Owner:** Product owner + engineering.
- **Impact:** Affects update mechanism, installer size, SmartScreen reputation and support.
- **Next validation:** Build install/uninstall/upgrade PoC before beta (`M-009` `TASK-040`, or earlier if packaging risk increases).

## 10. ADR-009 — Basic use without registration

- **Status:** Accepted (confirmed by Product Owner).
- **Context:** Users expect a local-first desktop workspace without forced account creation.
- **Options:** Mandatory account login; offline Free MVP with optional Pro login only for paid upgrades.
- **Decision:** App can create and use local profiles without registration. Sign-in/account is required only for purchase or activation of Pro features.
- **Consequences:** No remote auth dependency for core Free workflow; exact Pro benefits, provider, payment and entitlement server remain open.
- **Revisit when:** Product business model evolves.

## 11. ADR-010 — Do not import source cookies/session

- **Status:** Accepted (confirmed by Product Owner; updated 2026-11-24 per D1).
- **Context:** Importing device-bound cookies or plaintext password dumps introduces security and compatibility risks.
- **Options:** Attempt DPAPI decryption of Chrome/Edge cookies; import non-secret browsing data only (bookmarks, history, open-tab URLs).
- **Decision:** MVP import includes bookmarks, history, and open tabs as URLs (plus optional read-only extension inventory). Source cookies/session are not imported. CSV password import and custom credential vault are deferred to post-MVP (D1).
- **Consequences:** Imported profiles must sign in to websites again. Eliminates plaintext CSV credential handling and custom password-manager surface in MVP.
- **Revisit when:** Product owner explicitly reopens CSV password vault or session import scope.

## 12. ADR-011 — Profile storage layout under Chrome Runtime

- **Status:** Accepted (2026-11-24 — verified by M-001 `TASK-002`).
- **Context:** CEF 156 runs Chrome Runtime exclusively. In Chrome Runtime, a request context's `CachePath` is accepted only as a **direct child** of `CefSettings.RootCachePath`; a deeper path is rejected (`Cannot create profile at path`) and CEF silently falls back to an in-memory (off-the-record) profile — persistence fails with no obvious error. Verified by the M-001 `TASK-002` spike (`artifacts/engine-spike/task-002-results.md`).
- **Options:** Nested `Profiles\<group>\<guid>` paths (rejected by Chrome Runtime); flat direct-child `<RootCachePath>\<profile-guid>`.
- **Decision:** Set `CefSettings.RootCachePath = %LOCALAPPDATA%\SwitchlyNext\Profiles`; each profile's `RequestContextSettings.CachePath = %LOCALAPPDATA%\SwitchlyNext\Profiles\<profile-guid>` (a single-segment direct child).
- **Consequences:** `Profiles` doubles as CEF's user-data dir, so it also holds CEF global state (unused `Default` profile, `Local State`, caches) and enforces Chromium's process-wide `ProcessSingleton` lock across `RootCachePath` (requiring an app-level single-instance guard). Backups must copy only registered `<profile-guid>` directories, never CEF global state. Empty `CachePath` = in-memory only, never for account browsing.
- **Revisit when:** CEF changes its profile-path rules, or the product prefers a differently-named root (e.g. `BrowserData`).

## 13. Confirmed clarifications (2026-11-24)

- **Priority provider compatibility targets:**
  - **Google Flow**: https://flow.google
  - **ChatGPT**: https://chatgpt.com
  - **Dola**: https://dola.com
  - **SeaArt**: https://seaart.ai
- **M-001 test harness (formerly `OPEN-008`, closed 2026-11-24):** Console app + `CefSharp.OffScreen.NETCore` at `artifacts/engine-spike/Switchly.EngineSpike/`.
- **Test framework (closed 2026-11-24):** `xUnit` (`xunit 2.9.3` in `tests/Switchly.Infrastructure.Tests`).
- **Product Owner Decisions D1–D5 (2026-11-24):**
  - **D1:** Password Vault (`ENT-009`) & CSV password import deferred to post-MVP.
  - **D2:** MVP backup is manual-only; scheduled automatic backup (`REQ-017`, `TASK-032`) deferred to post-MVP.
  - **D3:** Cross-device restore excludes device-bound Chromium cookies, uses chunked streaming AEAD + cache exclusions + restart-to-restore staged swap.
  - **D4:** Schema adds `IsFavorite` & `IsHidden`, `PersistSessionCookies` default = `true`, plain SQLite for non-secret metadata, ADR-001..004/007/009..011 promoted to `Accepted`.
  - **D5:** Strict `Switchly.BrowserHost` isolation enforced in M-002; `Infrastructure/BrowserEngine` removed; `Microsoft.Extensions.*` aligned to `10.x` LTS via `Directory.Packages.props`.
  - **D6 (2026-11-24):** Product Owner explicitly reopened **D1** after M-007 DPAPI verification to implement **`ENT-009 VaultCredential`** (single-payload AES-256-GCM with unique 96-bit CSPRNG nonce + AAD `(CryptoVersion, CredentialId, ProfileId, Origin)` wrapped by Windows DPAPI `CurrentUser`), **Chrome/Edge CSV Password Import (`TASK-028`, `AC-010`, `TEST-010`)**, and **explicit-click origin-matched Autofill** (no unrestricted JS bridge).
  - **D7 (2026-11-24):** Product Owner approved **Hybrid Integrated Workspace UX**: clicking a profile in the sidebar opens/switches its isolated `BrowserWorkspaceView` directly inside `MainWindow`'s right pane (1-click switching while keeping other active profiles alive), while retaining **Pop-out (`↗ Jendela Baru`)** for side-by-side / multi-monitor workflows, plus interactive per-profile Bookmark (`UI-005`), History (`UI-006`), and Downloads (`UI-007`) dialogs.

## 14. Open decisions with impact

| ID | Missing decision | Assumption for draft | Impact | Owner / next step |
|---|---|---|---|---|
| OPEN-001 | Free vs Pro limits/benefits | No artificial quotas in prototype | Subscription model and UX | Product owner defines after product prototype |
| OPEN-002 | Windows 10 Home/Pro support guarantee | Best-effort only | Public minimum OS and support workload | Product owner decides before public beta |
| OPEN-003 | Optional extension scope | Extensions are not an MVP priority | Post-MVP feature scope | Reopen only if product owner reprioritizes |
| OPEN-004 | Product branding | Switchly Next working name | Package identity, domain, icon | Product owner |
| OPEN-005 | License/account provider | Not integrated in MVP | Pro activation flow | Product owner after package definition |
| OPEN-006 | Post-MVP scheduled backup schedule/retention | Deferred post-MVP (daily/7-snapshot proposal) | Disk usage and crypto design (hybrid public-key) | Product owner when reopening `REQ-017` |
| OPEN-007 | Installer technology/signing | Signed per-user installer proposal | Distribution and cost | Product owner before beta |
| OPEN-009 | Post-MVP password vault & CSV import | Deferred post-MVP (D1) | Security surface and Vault UI | Reopen only if product owner reprioritizes |
| OPEN-010 | Telemetry opt-in & English UI / theme tokens | Telemetry OFF; Indonesian MVP UI; system theme | Privacy notice & UX polish | Product owner before beta |
