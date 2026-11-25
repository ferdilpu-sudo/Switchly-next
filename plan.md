# Implementation Plan — Switchly Next

**Status:** M-009 In Progress (D8 decisions locked: Beta Free-only, Win11 x64 official, Inno Setup EXE + Portable ZIP, branding `Switchly`, UI Bahasa Indonesia, Telemetry OFF)  
**Strategy:** prove the browser engine risk before implementing the full product.

## 1. Delivery strategy

Build small vertical slices. First prove CEF storage isolation, integrated browser behavior, priority-provider compatibility, and measured resource use. Do not start all profile/import/backup features before the engine selection gate passes.

Key assumptions:
- One developer or small AI-assisted team.
- Windows x64 first.
- Windows 11 primary target; Windows 10 compatibility is validated and its policy resolved before public beta.
- Local-first architecture; no cloud sync.
- Freemium packaging is planned but exact entitlement/pricing is undecided.
- Product name is provisional.

## 2. Milestone index

| ID | Goal | Dependencies | Deliverables | Requirements | Exit criteria |
|---|---|---|---|---|---|
| M-001 | CEF engine, provider and OS proof-of-concept | None | CefSharp harness, profile isolation demo, priority-provider compatibility matrix, performance baseline | REQ-003, REQ-005, REQ-014 | TEST-003, TEST-004, TEST-016, TEST-017, TEST-021 + provider checks pass on Win11 (Win10 & non-elevated WPF stress carried to M-002/M-008) |
| M-002 | Desktop solution foundation | M-001 pass | WPF shell, MVVM, DI, strict `Switchly.BrowserHost` isolation (D5), `Directory.Packages.props` (`10.x` LTS), SQLite migration baseline, CI | REQ-001, REQ-012 | Clean build/test green; TEST-001 (Win11 launch), TEST-014 pass; non-elevated WPF launch verified |
| M-003 | Profile manager and isolation | M-002 | CRUD, duplicate config, group model, favorites, search/filter (profiles/groups), per-profile request context, multiple windows | REQ-002, REQ-003, REQ-004, REQ-005, REQ-015, REQ-016 | TEST-002, TEST-003, TEST-004, TEST-005, TEST-017, TEST-023, TEST-024 (profiles/groups) pass |
| M-004 | Browser essentials | M-003 | Tabs, navigation, bookmarks, history, downloads, session restore, tab/bookmark search | REQ-005, REQ-006, REQ-016 | TEST-006, TEST-007, TEST-021, TEST-024 (tabs/bookmarks) pass |
| M-005 | Optional extension support (deferred) | M-001, M-003 | Only a limited extension capability if later approved | REQ-007 | Informative optional gate (`TEST-008`, `TEST-019`); not required for MVP release |
| M-006 | Browser import | M-004 | Chrome/Edge discovery, preview, bookmark/history/open-tab URL import adapters *(CSV password vault reopened & implemented per D6)* | REQ-008, REQ-009 | TEST-009, TEST-018 pass (`TEST-010` PASS per D6) |
| M-007 | Security and manual backup/restore | M-003, M-004 | DPAPI/Hello local unlock, manual chunked AES-256-GCM backup, restart-to-restore staging swap *(scheduled backup deferred per D2)* | REQ-010, REQ-011 | TEST-011, TEST-012, TEST-013, TEST-020, TEST-022 pass (`TEST-025` deferred post-MVP per D2) |
| M-008 | Performance, stability, accessibility | M-004, M-006, M-007 | Resource manager/indicator, UX hardening, recovery, high-DPI/keyboard QA, Win10/11 matrix | REQ-005, REQ-006, REQ-014, REQ-018 | TEST-001 (Win11+Win10), TEST-016, TEST-026 and full MVP acceptance suite pass |
| M-009 | Freemium and release readiness | M-008 + product decisions | License boundary, installer (`ADR-008`), signing, notices, support docs | REQ-012, REQ-013, REQ-014 | TEST-001, TEST-014, TEST-015 pass; product owner approves pricing/Free-Pro/OS policy and beta release gates |

## 3. M-001 — executable work breakdown

### TASK-001 — Verify current CEF/CefSharp compatibility
- Inspect current CEF/CefSharp release notes, supported .NET targets, lifecycle/threading APIs, security advisories, known issues, and redistribution requirements.
- Pin a test package/runtime combination.
- Output: `artifacts/engine-spike/engine-version-report.md`.
- Tests: Windows/CEF runtime prerequisite; detailed provider and isolation evidence is recorded by TASK-002 through TASK-005.

### TASK-002 — Create isolated profile contexts
- Test harness: .NET 10 console application using `CefSharp.OffScreen.NETCore`. Project name: `Switchly.EngineSpike`. Location: `artifacts/engine-spike/Switchly.EngineSpike/`. This is a throwaway PoC, not part of the final solution structure.
- Create two request contexts with distinct absolute persistent cache paths.
- Verify cookie/localStorage separation and persistence.
- Output: minimal test harness and results.
- Tests: TEST-003, TEST-004, TEST-017.

### TASK-003 — Validate priority website workflows
- Validate normal navigation and supported authentication behavior for Google Flow (https://flow.google), ChatGPT (https://chatgpt.com), Dola (https://dola.com), and SeaArt (https://seaart.ai).
- Test uploads, downloads, popups, clipboard, media, WebGL where relevant, and browser recovery.
- Respect site policies; do not bypass embedded-browser restrictions, age requirements, or authentication safeguards.
- Output: provider compatibility matrix with evidence and limitations.
- Tests: TEST-006, TEST-021 and provider-specific manual cases.

### TASK-004 — Validate browser behavior
- Test downloads, popups, new windows, renderer crash/shutdown and navigation callbacks.
- Output: browser integration note.
- Tests: TEST-021.

### TASK-005 — Measure resource behavior
- Measure startup, idle, one active context and multiple contexts on named hardware.
- Test closed profiles do not keep renderer processes alive.
- Output: baseline report; do not invent memory goals before measuring.
- Tests: TEST-016.

### TASK-006 — Decide go/no-go
- Summarize pass/fail against profile isolation, required site workflows, basic browser behavior, packaging, and resource expectations. Extension capability is informative only.
- If any must-have fails, stop and present alternatives to product owner.
- Update ADR-003 and affected architecture/docs before M-002.

## 4. Later milestone breakdown

### M-002 — desktop foundation
- TASK-007: create solution/projects and WPF shell *(committed `0047a0c`)*.
- TASK-008: establish MVVM, DI/composition root and logging *(committed `713ac92`)*.
- TASK-009: create SQLite context/migrations and health check *(committed `5111268`, `9e5680b`; add `IsFavorite`/`IsHidden` & `PersistSessionCookies=true` per D4)*.
- TASK-010: create CI build/test baseline (`xUnit`), enforce strict `Switchly.BrowserHost` isolation (move `CefSharp.Wpf.NETCore` out of `Switchly.App`), and align `Microsoft.Extensions.*` to `10.x` LTS via `Directory.Packages.props` (D5).
- Exit: clean checkout builds, `dotnet test` green, non-elevated WPF shell launches cleanly (`TEST-001` Win11, `TEST-014`).

### M-003 — profile manager
- TASK-011: profile CRUD use cases and validation.
- TASK-012: storage path and profile locking.
- TASK-013: manual and website-domain grouping.
- TASK-014: connect BrowserHost to profile lifecycle.
- TASK-015: multiple windows and resource state.
- TASK-015A: favorites, search and filter interactions.
- Exit: cross-profile isolation tests green; TEST-023 and TEST-024 pass.

### M-004 — browser essentials
- TASK-016: tab and window coordination.
- TASK-017: bookmarks and history.
- TASK-018: download manager.
- TASK-019: session restore and startup preference.
- Exit: all state scoped to correct profile.

### M-005 — optional extensions (deferred)
- TASK-020: review supported extension APIs of the pinned CEF runtime only if the product owner reprioritizes this feature.
- TASK-021: implement explicit permission and compatibility UX only for capabilities verified in the selected runtime.
- TASK-022: maintain extension lifecycle/error state only if the optional feature is approved.
- Exit: not required for MVP; no extension-related claim without targeted evidence.

### M-006 — import
- TASK-023: Chrome/Edge source discovery and profile picker.
- TASK-024: bookmark importer.
- TASK-025: history importer with schema-version handling.
- TASK-026: open-tab URL importer.
- TASK-027: optional extension inventory report only if supported and approved.
- TASK-028: *(Reopened & implemented per D6 — commit `c0bf4df`)* password CSV parser, encrypted vault writer (`ENT-009`), and explicit-click autofill.
- Exit: preview/partial failure/report behavior validated (`TEST-009`, `TEST-010`, `TEST-018`).

### M-007 — security and manual backups
- TASK-029: local DPAPI protection and unlock key handling.
- TASK-030: optional master password and Windows Hello unlock UX.
- TASK-031: manual backup manifest and chunked streaming AES-256-GCM encryption (excluding CEF global state and disposable caches per D3).
- TASK-032: *(Deferred post-MVP — D2)* automatic backup scheduler.
- TASK-033: restart-to-restore staging swap, cross-device cookie exclusion (D3), and rollback handling.
- TASK-033A: *(Deferred post-MVP — D2)* backup schedule/retention settings.
- Exit: cross-device restore and negative security tests pass (`TEST-011`, `TEST-012`, `TEST-013`, `TEST-020`, `TEST-022`).

### M-008 — hardening
- TASK-034: resource manager and user warnings.
- TASK-035: crash/renderer failure recovery.
- TASK-036: accessibility, keyboard, high-DPI and multi-monitor QA.
- TASK-037: Windows compatibility matrix.
- TASK-038: full regression/security/performance pass.
- TASK-038A: resource indicators and memory-pressure warning UI; TEST-026.

### M-009 — release
- TASK-039: finalize Free/Pro boundaries and account/license provider.
- TASK-040: installer/update/signing pipeline.
- TASK-041: third-party notices and security update policy.
- TASK-042: onboarding, support and privacy documentation.
- TASK-043: product owner beta approval.

## 5. Risks and mitigation

| Risk | Severity | Mitigation |
|---|---|---|
| Priority website rejects embedded CEF user agent or sign-in flow | High | Validate in M-001; respect provider policy and document limits |
| Active profile RAM grows quickly | High | Measure early, lazy-open, no persistent renderer for closed profiles |
| Win10 consumer support promise conflicts with vendor lifecycle | High | Public OS policy decision before beta |
| Password CSV plaintext exposure | High | Warnings, memory-first parsing, encrypted vault, no logs |
| Chrome/Edge data format drift | Medium | Fixture-based, versioned importer and partial result |
| Backup restore corrupts existing data | Critical | Staging, integrity validation, atomic promotion |
| Freemium limits undecided | Medium | Do not add artificial hard limits or payments before product decision |

## 6. Progress tracker

| Milestone | Status | Evidence |
|---|---|---|
| M-001 | Complete (Win11; Win10 & non-elevated WPF stress carried to M-002/M-008) | `artifacts/engine-spike/engine-version-report.md`, `task-002-results.md` (`TEST-003`/`004`/`017` PASS), `task-003-results.md` (4 providers public + manual sign-in PASS), `task-004-results.md` (`TEST-021` PASS), `task-005-results.md` (`TEST-016` baseline), `task-006-go-nogo.md` |
| M-002 | Complete | Tier-2 manual sign-in PASS (`task-003-results.md`); `TASK-007`..`010` + D4 migration (`AddProfileFavoritesGroupHiddenAndConstraints`) + D5 `Switchly.BrowserHost` isolation & `Directory.Packages.props` (`10.0.11`) + `app.manifest` (`asInvoker`, `longPathAware`) + CEF `locales\*.pak` (220 files) done; `dotnet format`, `dotnet build -c Release` (0 warnings/0 errors), `dotnet test -c Release` (2/2 PASS), and manual WPF launch (`TEST-001` Win11 / `TEST-014`) confirmed PASS by product owner on 2026-11-24 |
| M-003 | Complete | `TASK-011`..`015A` done (`ProfileValidation`, `ProfileService`, `ProfileFilter`, `GroupValidation`, `GroupService`, `ProfileSessionManager`, `BrowserWindowCoordinator`, `WorkspaceViewModel`, `MainWindow` UI-001/004, `ProfileEditorDialog` UI-002, `BrowserWindow` UI-003; `Domain.Tests` 39/39, `Application.Tests` 8/8, `Infrastructure.Tests` 11/11 = 58/58 PASS, `dotnet format` PASS, manual multi-profile workspace & browser window check confirmed PASS by product owner on 2026-11-24) |
| M-004 | Complete | `TASK-016`..`019` done (multi-tab `BrowserWindow`, `ProfileLifeSpanHandler`, `ProfileDownloadHandler`, `Bookmark`/`HistoryEntry`/`DownloadRecord`/`BrowserTabSession` + migrations `AddBookmarksAndHistory`, `AddDownloads`, `AddTabSessions` + session restore; `Domain.Tests` 58/58, `Application.Tests` 8/8, `Infrastructure.Tests` 14/14 = 80/80 PASS, `dotnet format` PASS; manual GUI verification confirmed PASS by product owner on 2026-11-24) |
| M-005 | Deferred (optional) | Optional extension support (`REQ-007`) deferred beyond MVP per product-owner decision |
| M-006 | Complete | `TASK-023`..`026` done (commit `ce03122`): Chrome/Edge read-only discovery, preview, bookmark/history/open-tab URL import, `ImportWizardDialog`, `AddImportJobs` migration; `BrowserImportIntegrationTests` (`TEST-009`, `TEST-018`) PASS; no cookies/sessions imported |
| M-007 | Complete | `TASK-029`..`031`, `TASK-033` done (commit `90c3c6a`): Windows DPAPI protection, optional master password unlock (`UnlockDialog`), manual chunked AES-256-GCM backup, restart-to-restore staged swap (`PendingRestoreApplier`), `AddBackupJobs` migration; `SecurityAndBackupIntegrationTests` (`TEST-011`, `TEST-012`, `TEST-013`, `TEST-020`, `TEST-022`) PASS; scheduled backup (`TASK-032`/`033A`) deferred per D2 |
| D6 (Password Vault) | Complete | `TASK-028` reopened & implemented per D6 (commit `c0bf4df`): `ENT-009 VaultCredential` (unique-nonce AES-256-GCM + AAD wrapped by DPAPI `CurrentUser`), Chrome/Edge CSV import, explicit-click origin-matched autofill (`CredentialPickerDialog`); `PasswordVaultIntegrationTests` (`TEST-010`) PASS |
| M-008 | Complete (Win11; Win10 physical test pending `OPEN-002`) | `TASK-034`..`038A` done (commit `e5c22a2`): `WindowsResourceMonitor`, `ResourcePressureEvaluator`, memory-pressure warning banner, renderer crash recovery (`ProfileRequestHandler`), PerMonitorV2 DPI, `Ctrl+Shift+T`, `docs/compatibility-and-qa-matrix.md`; `ResourcePressureEvaluatorTests` (`TEST-026`) PASS |
| D7 (Hybrid Workspace UX) | Complete | Hybrid Integrated Workspace implemented per D7 (commit `b1d7936`): 1-click sidebar switching in `MainWindow` (`BrowserWorkspaceView`), pop-out `BrowserWindow`, interactive per-profile Bookmark/History/Downloads dialog (`ProfileLibraryDialog`) |
| M-009 | In Progress (`TASK-039`..`042` done; `TASK-043` awaiting PO Beta approval) | D8 locked (`decisions.md`); `TASK-039` (`IFeatureEntitlementService`, `FreeBetaEntitlementService`, `FreeBetaEntitlementServiceTests` `TEST-015` PASS, `Switchly` branding `0.9.0-beta.1`); `TASK-040` (`packaging/Switchly.iss`, `packaging/package-beta.ps1`); `TASK-041` (`THIRD-PARTY-NOTICES.md`, `SECURITY.md`); `TASK-042` (`docs/PRIVACY.md`, `docs/USER-GUIDE-BETA.md`, `docs/BETA-RELEASE-NOTES.md`); Release build 0 warnings/0 errors, **108/108 tests PASS** (`Domain` 62, `Application` 20, `Infrastructure` 26) on 2026-11-25 |

## 7. Milestone definition of done

- All linked REQ/AC/TEST obligations for milestone are implemented or explicitly deferred with owner approval.
- `dotnet build`, relevant test suites, and platform smoke tests are actually run.
- Security-sensitive workflows have negative tests.
- No unhandled critical data-loss or profile-isolation defects.
- Docs are updated to match actual code.
- Report PASS/FAIL/BLOCKED/NOT RUN honestly.
- Product owner explicitly approves any change to scope, browser engine, OS support or business model.
