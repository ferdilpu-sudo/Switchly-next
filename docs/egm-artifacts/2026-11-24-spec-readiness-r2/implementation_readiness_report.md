# Implementation Readiness Audit — Switchly Next Canonical Suite

**Reviewer Persona:** Pragmatic Tech Lead (`egm-implementation-readiness`, fresh-context Goldfish subagent `2093fdd6-40d3-4e85-9082-57dfa09eecdc`)  
**Date:** 2026-11-24  
**Standard:** `.agents/plugins/egm/resources/document_standards/implementation-readiness-standard.md`

## Executive Verdict: **FAIL (Not Ready for Unambiguous Execution)**

Evaluated against `.agents/plugins/egm/resources/document_standards/implementation-readiness-standard.md` (Core Principle, Zero-Decision Rule, Phasing & Checkpoints, Target Scope & File Boundary, Actionable Steps, Test Plan).

The canonical specification suite is a solid **product-discovery and high-level architecture draft**, but it **fails the Implementation Readiness Standard** for M-002 and all subsequent milestones (`M-002`–`M-009`). A fresh engineer cannot execute `TASK-007` through `TASK-043` without inventing critical architectural boundaries, package versions, MVVM/logging frameworks, database columns, cryptographic constructions, process-locking semantics, and UI flows.

---

## Explicit Milestone Readiness Assessment (M-001 vs M-002)

### M-001 Status: **Effectively Executed, but Canonically Unreconciled / Partially Unverified**
- `plan.md` (lines 3, 150) and `decisions.md` (ADR-003 line 47, ADR-011 line 116) declare **M-001 Complete** and claim the M-002 manual sign-in gate passed for all four priority providers.
- **However, the evidence and canonical docs contradict this status:**
  1. `artifacts/engine-spike/task-006-go-nogo.md` (lines 12, 15, 22–24, 35–37) still records **Provider sign-in (tier 2) as `NOT TESTED`**, **Packaging as `NOT TESTED`**, an intermittent **OffScreen native crash under elevation** (line 30), and ends with `"Decision needed: Product owner confirms GO to begin M-002"` — with no recorded approval date or sign-in evidence artifact path.
  2. `plan.md` (line 22) requires `TEST-006` in M-001 exit criteria, and `architecture.md` (line 241, 246) + `testing.md` (line 73) require WPF window hosting and Windows 10 target-edition checks in M-001; `task-006-go-nogo.md` does not record `TEST-006`, WPF hosting (TASK-002 used `CefSharp.OffScreen.NETCore` console app per `decisions.md` line 133), or Windows 10 testing.
  3. `README.md` (lines 3, 43, 53, 125, 127), `testing.md` (lines 3, 119), and `architecture.md` (line 3) still state *"Specification only — no application code has been implemented or tested"* and *"Start with M-001"*.
  4. `plan.md` contradicts itself on adjacent rows (lines 151–152): row 2 says `M-002 | In progress | Sign-in gate PASS (all 4 providers, manual); TASK-007..010 remaining`, while row 3 immediately below says `M-002 to M-009 | Not started | No code or test execution has been performed`.

### M-002 Status & Readiness: **Partially Scaffolded in Workspace, BLOCKED for Clean Execution**
- **Workspace reality contradicts `plan.md` line 151 (`TASK-007..010 remaining`):** `Switchly.sln` already exists at the workspace root containing `src/Switchly.App`, `src/Switchly.Domain`, `src/Switchly.Application`, `src/Switchly.Infrastructure`, `src/Switchly.BrowserHost`, and `tests/Switchly.Infrastructure.Tests`.
- **Architecture & version drift already visible in the partial scaffold:**
  - `src/Switchly.App/Switchly.App.csproj` (line 17) directly references `CefSharp.Wpf.NETCore 156.0.300` and does **not** reference `Switchly.BrowserHost`, while `src/Switchly.BrowserHost/Switchly.BrowserHost.csproj` targets plain `net10.0` with no CefSharp reference — violating `architecture.md` (line 116) and `rules.md` (line 26).
  - `src/Switchly.App/Switchly.App.csproj` (lines 18–20) references `Microsoft.Extensions.DependencyInjection`/`Logging` `11.0.0` on `net10.0-windows`, while `src/Switchly.Infrastructure/Switchly.Infrastructure.csproj` (lines 8, 12) uses EF Core `10.0.11`.
  - `tests/Switchly.Infrastructure.Tests/Switchly.Infrastructure.Tests.csproj` (line 13) already chose `xunit 2.9.3`, while `README.md` (line 24) and `testing.md` (line 115) still say `"xUnit or NUnit (choose at scaffold)"`.
- **Conclusion:** **M-002 is NOT ready to continue executing cleanly** until (a) M-001/M-002 status and sign-in evidence are reconciled across docs, (b) an executable, file-level M-002 specification is written for `TASK-007`–`TASK-010`, and (c) the existing scaffold's boundary/package drift is explicitly accounted for.

---

## Findings

### Blockers

#### B-01 — Severe Canonical Status & Evidence Contradictions Across Docs and Workspace
- **Severity:** Blocker
- **Affected Scope:**
  - `README.md` (lines 3, 43, 53, 125, 127)
  - `plan.md` (lines 3, 22, 150–152)
  - `architecture.md` (line 3)
  - `testing.md` (lines 3–4, 73, 119, 150–155)
  - `decisions.md` (line 47)
  - `artifacts/engine-spike/task-006-go-nogo.md` (lines 12, 15, 22–37)
  - `Switchly.sln` (lines 6–21)
- **Finding & Guidance:**
  Per `AGENTS.md` (line 51), conflicting canonical documents halt execution; per `AGENTS.md` (lines 122–128, 153) and `testing.md` (line 4), no milestone/gate may be marked PASS/Complete without recorded evidence paths and verified exit criteria. Currently:
  - `plan.md` line 151 vs line 152 directly conflict (`M-002 In progress` vs `M-002 to M-009 Not started`).
  - `plan.md` line 3 / `decisions.md` line 47 claim all 4 provider sign-ins passed manually, but `task-006-go-nogo.md` says sign-in is `NOT TESTED` and no sign-in evidence file is linked.
  - `README.md` and `testing.md` claim zero code/tests exist, while `Switchly.sln` and 6 projects already exist.
  - **Guidance:** Reconcile all status banners/tables, link the exact redacted evidence artifact for the 4-provider manual sign-in gate, explicitly record product-owner GO approval and deferred M-001 items (Win10, packaging, WPF hosting check), and audit existing files under `src/` and `tests/` against `TASK-007`–`TASK-010`.

#### B-02 — `plan.md` Fails Zero-Decision Rule and Omits File Boundaries, Actionable Steps, and Task Traceability (`TASK-007`–`TASK-043`)
- **Severity:** Blocker
- **Affected Scope:** `plan.md` Section 4 (lines 70–133); `README.md` (line 24); `architecture.md` (lines 32, 66–108); `testing.md` (lines 115, 127)
- **Finding & Guidance:**
  Every task after M-001 (`TASK-007` through `TASK-043`) is a single-line summary with zero file paths, zero API/class definitions, and no per-task `REQ`/`AC`/`TEST` mapping (required by `AGENTS.md` line 120). Even for the immediately active milestone **M-002** (`TASK-007`–`TASK-010`), an implementer must independently decide:
  1. **MVVM library** (`CommunityToolkit.Mvvm` vs custom `ObservableObject`/`RelayCommand`).
  2. **Test runner** (`README.md` line 24 and `testing.md` line 115 say `"xUnit or NUnit (choose at scaffold)"`, contradicting `xunit 2.9.3` already in `Switchly.Infrastructure.Tests.csproj`).
  3. **Rolling file logging implementation** (`architecture.md` line 32 specifies `Microsoft.Extensions.Logging + rolling local files`, which has no built-in file sink in .NET; package, path, rolling interval, retention, and redaction sink are unspecified).
  4. **Exact `.csproj` TFMs, project references, and package versions** (e.g., whether `Switchly.BrowserHost` and `Switchly.Infrastructure` target `net10.0-windows` vs `net10.0`, and avoiding `11.0.0` vs `10.0.11` package skew).
  5. **M-002 SQLite baseline scope & health check contract** (`TASK-009`: which entities are in initial migration vs later milestones; exact `PRAGMA` settings like WAL / `foreign_keys=ON`; what "health check" verifies).
  6. **CI platform, workflow file path, `.editorconfig`, and CEF test filter** (`TASK-010`: GitHub Actions vs other, runner image, test filter excluding Windows-interactive CEF tests per `testing.md` line 127).
  - **Guidance:** Before executing any milestone, produce an implementation-ready milestone task spec with an explicit file write allowlist (exact repo-relative paths), pinned NuGet versions (ideally via `Directory.Packages.props`), exact DI/project dependency graph, and per-task REQ/AC/TEST mappings.

#### B-03 — Unspecified `BrowserHost` Interfaces, WPF Embedding Boundary, and CEF Lifecycle/Threading Contracts
- **Severity:** Blocker
- **Affected Scope:** `rules.md` (lines 17, 20, 26, 58–60, 65–66); `architecture.md` (lines 98–102, 116, 122–129, 133–141); `artifacts/engine-spike/engine-version-report.md` (lines 42–44, 104–109, 121, 145)
- **Finding & Guidance:**
  - `rules.md` (line 58) names `IBrowserRuntime`, `IBrowserProfileContext`, and `IBrowserWindowHost`, but **none of these interfaces, signatures, events, or lifecycle state transitions are defined** anywhere.
  - **WPF UI embedding paradox:** `rules.md` (lines 17, 26) prohibits `Switchly.App` from calling CefSharp directly and puts all CefSharp APIs inside `Switchly.BrowserHost`. Yet in WPF, `ChromiumWebBrowser` is a `FrameworkElement` that must be placed in the visual tree. The spec does not define how `Switchly.App` hosts a browser tab without referencing CefSharp (e.g., `Switchly.BrowserHost` exposing a WPF `Control`/`FrameworkElement` factory vs `IBrowserSurfaceHost` abstraction, and requiring `net10.0-windows` + `<UseWPF>true</UseWPF>` on `Switchly.BrowserHost`). Notably, the partial scaffold in `src/Switchly.App/Switchly.App.csproj` (line 17) already broke this rule by referencing `CefSharp.Wpf.NETCore` directly.
  - **CEF lifecycle & manifest details missing:** Exact `CefSettings` values, `app.manifest` (`asInvoker` per `rules.md` line 66), `Cef.UIThreadTaskFactory` marshaling boundary, `OnBeforeClose` + `Cef.Shutdown()` ordering, and decisions on known CEF 156 issues from `engine-version-report.md` (missing H.264/AAC codecs affecting SeaArt, WPF resize black-space bugs #5142/#5174, IME password leak bug #5220, and CVE-2026-134401 verification) are undocumented.
  - **Guidance:** Define exact C# port interfaces in `Switchly.Application`, define the WPF control-hosting boundary for `Switchly.BrowserHost`, specify the `app.manifest` and startup/shutdown sequence, and record explicit engineering decisions for codecs and known CefSharp 156 issues.

#### B-04 — Authoritative Data Schema (`schema.md`) Has Missing Columns, Unresolved Defaults, Missing FK Rules, and an Open Whole-DB Encryption Decision
- **Severity:** Blocker
- **Affected Scope:** `schema.md` (lines 34–50, 52–59, 61–165, 192–215, 241); `prd.md` (`REQ-015`/`AC-015` lines 66, 141); `plan.md` (`TASK-009`, `TASK-015A`)
- **Finding & Guidance:**
  Implementers building EF Core entities and initial migrations (`TASK-009`) hit multiple direct omissions:
  1. **Missing `IsFavorite` column on `ENT-001 BrowserProfile`:** Required by `REQ-015`/`AC-015`, `design.md` (line 20), `plan.md` (`TASK-015A`), and `testing.md` (`TEST-023`), but absent from `schema.md` (lines 34–50).
  2. **Missing `IsHidden` column on `ENT-002 ProfileGroup`:** `schema.md` (line 59) states users may hide system-derived groups, but `ENT-002` has no `IsHidden` field.
  3. **Unresolved default for `ENT-001.PersistSessionCookies`:** `schema.md` (line 48) says *"default selected by product privacy review"* — cannot be coded into a migration without choosing `true` or `false`.
  4. **Whole-DB encryption unresolved:** `schema.md` (line 241) leaves open whether SQLite itself is encrypted (SQLCipher vs plain SQLite), which changes the EF Core provider and startup key-unlock sequence starting in M-002.
  5. **Undefined allowlists & schemas:** Valid values for `ENT-001.IconKey` (line 40), `ENT-010.AppSetting.Key` + JSON schemas (lines 139–142), `ENT-012.SelectedCategoriesJson` (line 159), and `"supported internal URLs"` in `ENT-004.Url` (line 72) are never enumerated.
  6. **Incomplete physical DDL for `ENT-002`–`ENT-012`:** Column SQLite types, max lengths, nullability, defaults, and FK `ON DELETE` behaviors (e.g., `ImportJob.TargetProfileId`, self-referencing `Bookmark.ParentBookmarkId`, child entities on profile deletion) are omitted.
  - **Guidance:** Complete physical column tables (type, nullability, default, max length, FK `ON DELETE`, indexes) for all 12 entities, add `IsFavorite` and `IsHidden`, lock `PersistSessionCookies` default and plain-SQLite vs SQLCipher decision, and enumerate all allowlisted keys/enums.

#### B-05 — Unspecified Cryptography, Vault Key Hierarchy, Nonce-Reuse Hazard, Unlock Flow, and Portable Backup Wire Format
- **Severity:** Blocker
- **Affected Scope:** `schema.md` (lines 126–137, 232–234); `architecture.md` (lines 30–31, 169–175, 198–199); `decisions.md` (ADR-005 lines 68–75, ADR-006 lines 77–84); `README.md` (lines 22–23); `rules.md` (lines 46–49, 85); `plan.md` (`TASK-028`–`TASK-033A`)
- **Finding & Guidance:**
  Every security-critical design parameter for the vault and backup subsystems is unspecified:
  1. **Critical AEAD nonce-reuse hazard in `ENT-009 VaultCredential` (`schema.md` lines 130–133):** Lists two ciphertext fields (`UsernameCiphertext`, `PasswordCiphertext`) alongside a single `Nonce` and `AuthenticationTag`, and says *"where required by chosen cipher"*. Reusing one AES-GCM nonce across two ciphertexts breaks confidentiality and authenticity. The spec must pin the cipher (e.g., AES-256-GCM), encrypt a single canonical serialized payload (or store distinct nonces/tags per field), and define AAD (e.g., `CredentialId|ProfileId|Origin|CryptoVersion`) to prevent cross-profile ciphertext swapping.
  2. **Key hierarchy & storage location:** Undefined whether there is one app-wide Vault Master Key or per-profile keys (`architecture.md` line 198 vs `schema.md` line 232), where DPAPI-wrapped key files live, and exact DPAPI entropy/scope rules.
  3. **Master Password + Windows Hello unlock flow (`ADR-005`):** `README.md` (line 22) explicitly marks this as requiring an implementation spike. Unspecified whether master password cryptographically wraps the VMK or is a UI gate, which Windows API powers Hello (`UserConsentVerifier` vs `KeyCredentialManager`), lock scope (app-wide vs vault-only), auto-lock timeout, and key recovery policy (`schema.md` line 234).
  4. **Portable Backup Archive Format & KDF (`ADR-006`, `TASK-031`, `TASK-033`):** KDF algorithm (`Argon2id` vs `PBKDF2-HMAC-SHA256`) and parameters (memory, iterations, parallelism, salt size), archive file extension, binary header/magic/version, **streaming chunked AEAD layout** (essential so multi-GB CEF profile folders don't OOM single-shot AES-GCM), manifest JSON schema, vault re-encryption + target-machine DPAPI re-wrapping protocol, and Windows NTFS crash-safe multi-folder + SQLite staging/promotion/rollback journal protocol are all absent.
  5. **Scheduled backup vs active profiles + ADR-011 exclusion rules:** `decisions.md` (line 119) states backups must copy only `<profile-guid>` directories and never CEF global state (`Default`/cache), but exact exclusion globs and behavior when a scheduled backup fires while profiles are open (skip, prompt, or suspend?) are undefined.
  - **Guidance:** Author dedicated crypto/vault and backup-format technical specifications (or ADRs) locking cipher, AAD, key hierarchy, unlock mechanics, KDF parameters, chunked container format, manifest schema, and atomic restore journal before M-006/M-007.

---

### Majors

#### M-01 — Concurrency & Profile Locking Conflicts With CEF Chrome Runtime Process Singleton
- **Severity:** Major
- **Affected Scope:** `architecture.md` (lines 122, 140); `decisions.md` (ADR-011 lines 118–119); `rules.md` (line 37); `testing.md` (`TEST-017` line 49); `artifacts/engine-spike/engine-version-report.md` (line 83); `plan.md` (`TASK-012`)
- **Finding & Guidance:**
  `architecture.md` (line 140) specifies per-profile locking via named mutex or lock file with stale-lock recovery, and `TEST-017` tests opening the same profile from two app processes. However, under CEF Chrome Runtime with a shared `CefSettings.RootCachePath = %LOCALAPPDATA%\SwitchlyNext\Profiles`, Chromium enforces a **process singleton across the entire `RootCachePath`** (`engine-version-report.md` line 83). A second Switchly Next process cannot initialize CEF on that `RootCachePath` at all—even for a *different* profile.
  - **Guidance:** Explicitly specify an **app-wide single-instance mechanism** (named mutex + named pipe IPC to forward focus/open-profile requests to the running instance, matching `TEST-017`'s "directed to existing instance") combined with a documented per-profile lock file format (path, PID + process creation timestamp for stale-lock detection) for crash safety.

#### M-02 — Chrome/Edge Import Adapters Lack Concrete Source Formats, Deduplication Rules, and Limits (`M-006`)
- **Severity:** Major
- **Affected Scope:** `architecture.md` (lines 204–214); `schema.md` (lines 188, 190, 238, 240); `design.md` (lines 84–98); `plan.md` (`TASK-023`–`TASK-028`)
- **Finding & Guidance:**
  - `schema.md` (line 238) leaves exact Chrome/Edge SQLite history schemas open.
  - **Open-tab URL import (`TASK-026`)**: Chromium `Sessions/Session_*` and `Tabs_*` files use Chromium's binary SNSS pickle format, not JSON/SQLite. No parser specification, supported Chromium version range, or exact `"unsupported"` detection criteria are defined.
  - **Deduplication (`design.md` line 95)**: Preview must show duplicate counts, but duplicate keys and merge behavior (skip vs overwrite vs rename) are undefined for bookmarks, history, tabs, and CSV credentials.
  - **CSV parser & DoS bounds (`schema.md` lines 188, 190)**: Expected CSV column headers, max file size, max row count, max archive entry count, and behavior for rows with missing origin/username/password are unspecified.
  - **Discovery rules (`TASK-023`)**: Exact Chrome/Edge User Data paths, `Local State` profile-name parsing, and whether Beta/Dev/Canary/custom paths are supported are omitted.
  - **Guidance:** Define an Import Adapter specification covering discovery paths, SNSS/Bookmarks/History schema versions, duplicate-detection keys, CSV header mapping, and explicit size/row/entry bounds.

#### M-03 — Missing Vault UI, Ambiguous Window Topology, and Unspecified UX Mechanics (`design.md`)
- **Severity:** Major
- **Affected Scope:** `design.md` (lines 17–64, 67–83, 128, 133, 140–141, 149); `schema.md` (line 240)
- **Finding & Guidance:**
  1. **No Vault Management Screen:** `ENT-009 VaultCredential` stores imported CSV credentials and `schema.md` (line 240) defers browser autofill, yet `design.md` Section 3 (`UI-001`–`UI-013`) has **no screen to view, search, copy, or delete stored vault credentials**.
  2. **Main Workspace vs Browser Window Topology Ambiguity:** `design.md` (lines 18–31) nests `Browser Area` inside the root IA alongside `Workspace`, while lines 51/53 separate `UI-001 Main Workspace` and `UI-003 Browser Window`, and lines 70–71 say clicking a profile focuses the last profile window or opens the workspace. Implementers cannot tell whether tabs render inside the main shell window (with optional pop-out windows) or always spawn separate windows.
  3. **CEF Keyboard Shortcut Interception:** `design.md` (line 128) mandates `Ctrl+L`, `Ctrl+T`, `Ctrl+W`, `Ctrl+Tab`, `Ctrl+Shift+T`, but CEF native controls consume keyboard input unless intercepted via `IKeyboardHandler.OnPreKeyEvent`; routing rules vs page shortcuts are unspecified.
  4. **Localization & WPF Styling Baseline:** `design.md` (line 133) requires Bahasa Indonesia MVP with a localization framework, without choosing `.resx` vs XAML `ResourceDictionary`. Lines 140–141 defer visual tokens/theme without defining a minimal M-002 WPF styling baseline (e.g., standard WPF vs .NET 10 WPF Fluent theme).
  5. **Archive semantics:** Unspecified whether archived profiles (`ENT-001.IsArchived`) close active windows immediately, can be opened directly, or appear in a separate filter/section.
  - **Guidance:** Lock the window topology, add a Vault UI entry (or explicitly defer vault viewing with owner sign-off), choose the WPF localization and placeholder theme approach, and define archive and keyboard-interception rules before `TASK-007` and `TASK-016`.

#### M-04 — Unspecified Auto-Grouping Rules, Process-to-Profile Resource Attribution, Profile Duplication Scope, and Session Precedence
- **Severity:** Major
- **Affected Scope:** `prd.md` (`REQ-002`, `REQ-004`, `REQ-005`, `REQ-006`, `REQ-018`); `schema.md` (lines 47, 55–59, 64, 72, 77); `rules.md` (line 38); `architecture.md` (lines 145–148); `design.md` (line 149); `plan.md` (`TASK-013`, `TASK-015`, `TASK-018`, `TASK-019`, `TASK-034`, `TASK-038A`)
- **Finding & Guidance:**
  - **Auto-grouping (`TASK-013`)**: No rule defines *which* URLs populate `ProfileGroup (WebsiteDomain)` (startup URL only? all history? open tabs?), whether `DomainKey` is full host or eTLD+1 (Public Suffix List), or when auto-memberships expire/are pruned.
  - **Resource attribution & thresholds (`TASK-034`/`TASK-038A`)**: No specification for attributing CEF multi-process RAM/CPU to individual profiles, exact warning/critical thresholds (or configurable defaults), or polling/sampling cadence.
  - **Profile duplication (`REQ-002`, `rules.md` line 38)**: Does not specify which entities/fields copy on default "duplicate settings" (e.g., bookmarks? manual groups? name suffix?), nor whether optional "copy browser data" is in MVP.
  - **Startup/Session precedence (`TASK-019`)**: Precedence between app-level startup mode (`design.md` line 149), per-profile `RestoreLastSession`, `StartupUrl`, and `BrowserTabSession.RestoreState` (`Open | Closed | Discarded`) is undefined.
  - **Download manager (`TASK-018`)**: Default download folder (Windows Downloads vs per-profile folder), filename collision renaming, and dangerous extension handling are unspecified.
  - **Guidance:** Document concise, deterministic domain rules for each of these five behaviors in `architecture.md`/`schema.md` before M-003/M-004.

#### M-05 — Unaccepted ADRs, M-002 Installer PoC Omission, and Traceability/Test-Plan Gaps
- **Severity:** Major
- **Affected Scope:** `decisions.md` (lines 8–20, 93–100); `prd.md` (lines 57, 126–144); `testing.md` (lines 23–24, 31–58, 127); `plan.md` (lines 72–77, 129); `AGENTS.md` (line 43)
- **Finding & Guidance:**
  - `ADR-001`, `ADR-002`, `ADR-004`, `ADR-005`, `ADR-006`, and `ADR-007` are still `Proposed`, while `AGENTS.md` (line 43) confers authority only on **Accepted ADRs**.
  - `ADR-008` (line 100) schedules an installer/upgrade PoC in `"M-001 or M-002"`, but `plan.md` has no installer task in M-002 (it is pushed to `TASK-040` in M-009).
  - **`REQ-010` local unlock has no `AC-###` in `prd.md`:** `prd.md` `AC-010` (line 135) only covers Password CSV import, leaving DPAPI / Master Password / Windows Hello unlock without a formal acceptance criterion; `testing.md` `TEST-011` (line 43) consequently mis-maps to `AC-010`.
  - `testing.md` lacks concrete test-case assertions (happy/edge/negative), fixture specs, project-to-test-ID mapping (note `Switchly.sln` currently has only `Switchly.Infrastructure.Tests`, missing `Domain.Tests`, `Application.Tests`, and `E2E.Tests`), and baseline hardware specs (lines 23–24: `"Device specs open"`).
  - **Guidance:** Formally accept the foundational M-002 ADRs (`ADR-001`, `002`, `004`, `007`), resolve `ADR-008` timing, add a dedicated `AC` for local unlock in `prd.md` (and fix `TEST-011` mapping), and attach concrete test specifications/filters to each milestone task plan.

---

### Nits

#### N-01 — CEF Chrome Runtime Removal Version Typo in `README.md`
- **Severity:** Nit
- **Affected Scope:** `README.md` (line 116) vs `prd.md` (line 173), `decisions.md` (line 54), `artifacts/engine-spike/engine-version-report.md` (line 39)
- **Finding & Guidance:** `README.md` line 116 says `"Chrome Runtime is the only CEF mode from CEF 156+"`, whereas the other docs state Alloy was removed in **CEF 128** (so CEF 156+ runs Chrome Runtime exclusively). Align wording in `README.md`.

#### N-02 — Backup Retention Default Spread Across Three Docs With Different Status Labels
- **Severity:** Nit
- **Affected Scope:** `prd.md` (line 178), `schema.md` (line 233), `decisions.md` (OPEN-006 line 131)
- **Finding & Guidance:** Listed as `"Asumsi"` (daily, configurable) in `prd.md`, `"Terbuka; proposed initial default 7 daily snapshots"` in `schema.md`, and `"Daily/7-snapshot proposal"` in `decisions.md`. Consolidate into a single default constant before `TASK-032`/`TASK-033A`.

#### N-03 — Mixed NuGet Major Versions Already Present in Partial M-002 Scaffold
- **Severity:** Nit (escalates to Major if not cleaned before M-002 completion)
- **Affected Scope:** `src/Switchly.App/Switchly.App.csproj` (lines 18–20) vs `src/Switchly.Infrastructure/Switchly.Infrastructure.csproj` (lines 8, 12)
- **Finding & Guidance:** `Switchly.App` pulls `Microsoft.Extensions.*` `11.0.0` while `Switchly.Infrastructure` pulls EF Core `10.0.11` on `net10.0`. Introduce `Directory.Packages.props` and pin aligned `10.x` LTS packages as part of M-002 cleanup.

#### N-04 — Mixed Indonesian / English Terminology Across Canonical Specs
- **Severity:** Nit
- **Affected Scope:** `prd.md`, `design.md` (Indonesian); `architecture.md`, `schema.md` (mixed); `plan.md`, `rules.md`, `testing.md`, `decisions.md`, `README.md` (English)
- **Finding & Guidance:** Acceptable for product docs, but explicitly distinguish **English code/enum identifiers** (`schema.md`) from **Indonesian UI display strings** (`design.md`) to prevent implementers mixing localized labels into persisted keys/enums.

---

## Minimum Remediation Path Before Resuming M-002 Implementation

1. **Reconcile M-001/M-002 governance & evidence (B-01, M-05):** Update `README.md`, `plan.md`, `testing.md`, `architecture.md`, and `task-006-go-nogo.md`; link the 4-provider manual sign-in evidence; record Product Owner GO + acceptance of `ADR-001`, `ADR-002`, `ADR-004`, `ADR-007`.
2. **Write an Implementation-Ready M-002 Task Spec (B-02, B-03, N-03):** Lock MVVM package, `xunit`, rolling file logger + redaction sink, `Directory.Packages.props`, exact `.csproj` TFMs/references (moving `CefSharp.Wpf.NETCore` out of `Switchly.App` into `Switchly.BrowserHost` behind defined `IBrowser*` ports), exact file allowlist for `TASK-007`–`TASK-010`, CI workflow path + test filters, and M-002 baseline migration scope.
3. **Complete `schema.md` physical contracts before `TASK-009` (B-04):** Add `IsFavorite` and `IsHidden`, resolve `PersistSessionCookies` default and plain-SQLite vs SQLCipher, define FK delete rules and allowlisted keys.
4. **Before M-003 / M-006 / M-007 (B-05, M-01, M-02, M-03, M-04):** Close the single-instance vs per-profile locking design, window topology + vault UI, auto-grouping/duplication/session rules, import adapter specs, and the vault crypto + chunked backup archive format spec.
