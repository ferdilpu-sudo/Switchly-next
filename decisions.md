# Decisions / ADR Log — Switchly Next

**Status:** ACTIVE — core M-001/M-002 ADRs accepted; remaining decisions tracked below  
**Date:** 2026-11-25

## 1. Decision index

| ADR | Status | Decision | Related requirement | Revisit trigger |
|---|---|---|---|---|
| ADR-001 | Accepted | WPF + MVVM desktop shell | REQ-001, REQ-005 | Accessibility or shell limitations discovered |
| ADR-002 | Accepted | .NET 10 LTS | REQ-001, REQ-014 | Win10 Home/Pro support requirement conflicts |
| ADR-003 | Accepted | C# + WPF + CEF (Chrome Runtime) through CefSharp 156.0.300 | REQ-001, REQ-003, REQ-005, REQ-007 | Provider compatibility or isolation regresses |
| ADR-004 | Accepted | Plain SQLite for non-secret app metadata; CEF owns browser storage | REQ-002, REQ-006 | Local data volume/performance or whole-DB encryption demands |
| ADR-005 | Proposed (Implemented M-007/D6) | DPAPI local protection + optional master password/Hello unlock + encrypted Password Vault (`ENT-009`, reopened per D6) | REQ-010 | Threat model or Hello API PoC |
| ADR-006 | Proposed (Implemented M-007) | Manual AES-256-GCM chunked portable backup; device-bound cookies excluded; scheduled backup deferred post-MVP | REQ-011 | KDF performance, restore PoC, or scheduled-backup reprioritization |
| ADR-007 | Accepted | Local structured logs, secrets redacted; no telemetry without opt-in | NFR-SEC | Privacy review requests telemetry |
| ADR-008 | Accepted | Per-user Inno Setup EXE + Portable ZIP for Beta; code-signing before public release | REQ-014 | Public release signing or Store distribution |
| ADR-009 | Accepted | No registration for basic use; Pro login only for upgrade/activation | REQ-012, REQ-013 | Product business model evolves |
| ADR-010 | Accepted | No source cookies/session import; CSV password vault reopened & implemented per D6 | REQ-008, REQ-009 | Product owner explicitly expands scope |
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

- **Status:** Proposed — implemented in M-007 & D6 (scope originally updated 2026-11-24 per D1, then Password Vault reopened per D6).
- **Context:** User requested Windows security, optional master password, Windows Hello where available, and (via D6) an encrypted Password Vault for Chrome/Edge CSV import and explicit-click autofill.
- **Options:** Windows account protection only; application master password only; Windows DPAPI protection plus optional master-password, Windows Hello local app unlock, and DPAPI-wrapped AEAD credential vault.
- **Decision:** Protect local app secrets/unlock state with Windows DPAPI (`CurrentUser`), provide optional master-password and Windows Hello local unlock, and implement `ENT-009 VaultCredential` (reopened per D6) using single-payload AES-256-GCM with unique 96-bit CSPRNG nonces and AAD binding `(CryptoVersion, CredentialId, ProfileId, Origin)` plus explicit-click origin-matched autofill (no unrestricted JS bridge).
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

- **Status:** Accepted (2026-11-25 — Product Owner Decision D8).
- **Options:** MSIX; signed EXE/MSI installer; Inno Setup per-user EXE + portable ZIP.
- **Decision:** Ship Beta as a **per-user Setup Installer (Inno Setup EXE)** alongside a **Portable ZIP** archive for Windows 11 x64 (`win-x64`). Authenticode code-signing is deferred until public GA release (documented clearly in Beta release notes / SmartScreen guidance).
- **Consequences:** Works cleanly with CEF native binaries (`libcef.dll`, `locales/*.pak`, `CefSharp.BrowserSubprocess.exe`) without MSIX virtualization quirks; per-user install avoids UAC elevation (keeping CEF non-elevated policy intact).
- **Revisit when:** Preparing signed public GA release or Microsoft Store distribution.

## 10. ADR-009 — Basic use without registration

- **Status:** Accepted (confirmed by Product Owner).
- **Context:** Users expect a local-first desktop workspace without forced account creation.
- **Options:** Mandatory account login; offline Free MVP with optional Pro login only for paid upgrades.
- **Decision:** App can create and use local profiles without registration. Sign-in/account is required only for purchase or activation of Pro features.
- **Consequences:** No remote auth dependency for core Free workflow; exact Pro benefits, provider, payment and entitlement server remain open.
- **Revisit when:** Product business model evolves.

## 11. ADR-010 — Do not import source cookies/session

- **Status:** Accepted (confirmed by Product Owner; updated 2026-11-24 per D1 and D6).
- **Context:** Importing device-bound cookies introduces security and compatibility risks; CSV password import was initially deferred under D1 and subsequently reopened under D6 once M-007 DPAPI & AEAD primitives were verified.
- **Options:** Attempt DPAPI decryption of Chrome/Edge cookies; import non-secret browsing data (bookmarks, history, open-tab URLs) plus explicit CSV import into the encrypted per-profile vault (`ENT-009`).
- **Decision:** Import includes bookmarks, history, and open tabs as URLs (plus optional read-only extension inventory), and explicit CSV password import into the encrypted per-profile vault (`ENT-009`, per D6). Source cookies/sessions are never imported.
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
  - **D8 (2026-11-25):** Product Owner finalized M-009 Beta release decisions: (1) **Beta Free-only** — no artificial quotas and no remote license/payment backend in Beta; expose a clean local entitlement interface (`IFeatureEntitlementService`) for future Pro tiers (`OPEN-001`, `OPEN-005`); (2) **Windows 11 x64 only** as officially supported OS (Windows 10 best-effort/unsupported, `OPEN-002`); (3) **Per-user Inno Setup EXE + Portable ZIP** for Beta with code-signing before public GA (`OPEN-007`, `ADR-008`); (4) Official product name **`Switchly`**, **UI Bahasa Indonesia**, **Telemetry OFF (local-only)** (`OPEN-004`, `OPEN-010`).
  - **D9 (2026-11-25):** Product Owner simplified workspace grouping UX: remove automatic Website Domain Categories from the Main Workspace UI (since a single profile browses many websites) and make **Manual Folder Groups** first-class (assignable directly in `ProfileEditorDialog`, filterable via 1-click chips in Sidebar & Dashboard, and deletable with confirmation).
  - **D10 (2026-11-25):** Product Owner approved **Web Workspace Groups**: manual groups can carry an optional `DefaultUrl` (`AddGroupDefaultUrl` migration) with 1-click presets in `GroupEditorDialog` (ChatGPT, Google Flow, Dola, SeaArt). Selecting a Web Workspace Group and clicking any profile opens that profile's isolated session directly to the group's `DefaultUrl`. Under Opsi A, a Web Workspace Group with no manually assigned profiles displays all active profiles for instant switching.
  - **D11 (2026-11-25):** Product Owner selected **Model 1 — Web-Space Mode**: docked `BrowserWorkspaceView` instances are keyed by `(ProfileId, GroupId)` so each Web Workspace Group has its own clean, isolated tab strip starting strictly at the group's `DefaultUrl` (labeled `Group • Profile`), while still sharing the underlying `ProfileId`'s single isolated CEF storage/login context. Open tasks appear grouped by workspace (`Google Flow (N)`, `ChatGPT (N)`, `Browser Umum (N)`) in the Sidebar's `SEDANG AKTIF (TASK)` switcher for 1-click switching and per-task closing. Closing a profile releases all its workspaces cleanly.
  - **D12 (2026-11-25):** Product Owner approved 3-layer session shutdown UX: (1) 1-click `×` close-session button on active profiles in the Sidebar; (2) `Tutup Sesi Lain` & `Tutup Semua` controls in the Footer Status Bar plus 1-click `Tutup Sesi Latar Belakang` inside the Memory Warning Banner; (3) configurable **Auto-Sleep (30m)** for idle background profiles (skipping active docked & popped-out windows, persisted in `ui-autosleep.txt`).
  - **D13 (2026-11-25):** Product Owner approved **Option B** for HTML5 video playback compatibility (Google Flow, SeaArt, X, Instagram MP4/H.264/AAC): (1) implement native HTML5 fullscreen video transitions (`IDisplayHandler.OnFullscreenModeChange` → `BrowserWorkspaceView` → `MainWindow`/`BrowserWindow` borderless maximized fullscreen); (2) support an opt-in MSBuild binary overlay (`third_party/cef-proprietary/win-x64/`) backed by a reproducible GitHub Actions workflow (`.github/workflows/build-cef-proprietary.yml`) pinned to the exact CefSharp 156.0.300 ABI commit (`156.0.30+g6028375+chromium-156.0.8078.96`, branch `8078`, `proprietary_codecs=true`, `ffmpeg_branding=Chrome`). Never download unverified third-party `libcef.dll` drops; verify MPEG-LA/H.264 patent licensing requirements before wide commercial public distribution.
  - **D14 (2026-11-25):** Product Owner approved **UI/UX Redesign Direction v1 (`Browser-First · Multi-Profile · Scale-Ready Workspace`)** documented in `docs/UI-UX-REDESIGN-DIRECTION.md` with four conflict resolutions: (1) Automatic Website Groups remain excluded from the primary UI per `D9` (focus strictly on Manual & Web Workspace Groups); (2) `D10` (`DefaultUrl` Web Workspace Groups) and `D11` (Model 1 Web-Space Mode keyed by `(ProfileId, GroupId)`) are explicitly preserved in the Top Profile Strip and Feature Preservation Matrix; (3) global Profile Switcher / Command Palette uses `Ctrl+Shift+K` to avoid conflicting with ChatGPT/Notion/GitHub `Ctrl+K`; (4) UI status labels remain consistent Bahasa Indonesia (`Tampil`, `Berjalan`, `Dijeda`, `Tertutup`) with **Light theme** as default (Dark/Light toggle preserved). Execute in phased slices (Phase 0 audit → Collapsible Sidebar + Top Profile Strip → Virtualized Profile Library → Activity & Settings views) while preserving all architectural/security guardrails.
  - **D15 (2026-11-26):** Product Owner resolved usability audit findings by adopting **Strict Group Filter (Option 1)** — superseding the `D10` Opsi A empty-group fallback: selecting a group always filters strictly to profiles assigned to that group (showing an empty state with an explicit "Buat / Tambahkan Profil" action when 0 profiles are assigned), unifying terminology to **Grup** across Sidebar/Header/Table. Default table sort is **Name (A–Z)** to keep row positions stable when sessions open; memory advisory triggers early at **≥ 80% RAM** (when ≥ 1 profile is active) as a top non-overlay strip so web page controls (e.g. SeaArt Generate) are never obscured.

## 14. Open decisions with impact

| ID | Missing decision | Assumption for draft | Impact | Owner / next step |
|---|---|---|---|---|
| OPEN-001 | Free vs Pro limits/benefits | **Closed for Beta by D8 (2026-11-25):** Beta Free-only (no artificial quotas; local entitlement interface only) | Subscription model deferred to post-Beta GA | Reopen when launching paid Pro tier |
| OPEN-002 | Windows 10 Home/Pro support guarantee | **Closed by D8 (2026-11-25):** Official support is Windows 11 x64 only; Win10 best-effort/unsupported | Clear minimum OS policy without false claims | Closed |
| OPEN-003 | Optional extension scope | Extensions are not an MVP priority | Post-MVP feature scope | Reopen only if product owner reprioritizes |
| OPEN-004 | Product branding | **Closed by D8 (2026-11-25):** Official product name is **Switchly** | Installer, app title, docs use `Switchly` | Closed |
| OPEN-005 | License/account provider | **Closed for Beta by D8 (2026-11-25):** No remote license/payment backend in Beta | Free offline experience preserved (`ADR-009`) | Reopen when launching paid Pro tier |
| OPEN-006 | Post-MVP scheduled backup schedule/retention | Deferred post-MVP (daily/7-snapshot proposal) | Disk usage and crypto design (hybrid public-key) | Product owner when reopening `REQ-017` |
| OPEN-007 | Installer technology/signing | **Closed by D8 (2026-11-25):** Per-user Inno Setup EXE + Portable ZIP for Beta; code-signing before GA (`ADR-008` Accepted) | Beta distribution ready in `TASK-040` | Acquire signing cert before public GA |
| OPEN-009 | Password vault & CSV import | **Closed by D6 (2026-11-24):** reopened & implemented (`ENT-009`, `TASK-028`, explicit-click autofill) | Security surface and Vault UI covered by `PasswordVaultIntegrationTests` (`TEST-010`) | Closed |
| OPEN-010 | Telemetry opt-in & English UI / theme tokens | **Closed by D8 (2026-11-25):** Telemetry OFF (local-only); UI Bahasa Indonesia; system theme | Privacy notice & Beta UX finalized | Closed |
