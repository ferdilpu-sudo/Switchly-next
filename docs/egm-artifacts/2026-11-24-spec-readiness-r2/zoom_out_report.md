# EGM Zoom-Out Review — Switchly Next Canonical Specification Suite

**Reviewer Persona:** Skeptical Architect (`egm-zoom-out`, fresh-context Goldfish subagent `d4de298a-2490-4b19-af7d-ed6acc80217c`)  
**Date:** 2026-11-24  
**Standard:** `.agents/plugins/egm/resources/document_standards/zoom-out-standard.md`  
**Overall Verdict:** **FAIL (Blockers Present — M-001/M-002 readiness claims contradict both canonical docs and workspace artifacts)**

## 0. Context Sufficiency Check (`zoom-out-standard.md` §0)
**PASS.** The product objective (Windows desktop multi-account isolated browser workspace built cleanly on C#/WPF/CEF without WebView2), target personas, non-goals, and document authority order are clear across `README.md`, `prd.md`, `architecture.md`, and `AGENTS.md`.

---

## Executive Summary of M-001 / M-002 Readiness vs. Reality
The canonical suite is **internally split between three conflicting timelines**:
1. **"Pre-code specification only"** (`README.md`, `prd.md`, `architecture.md`, `testing.md`, and `plan.md` L152).
2. **"M-001 complete, M-002 sign-in gate passed, TASK-007..010 remaining"** (`plan.md` L3, L150–151; `decisions.md` `ADR-003` L12, L47).
3. **Actual workspace reality**: `artifacts/engine-spike/task-006-go-nogo.md` marks provider sign-in as **NOT TESTED** and awaits product-owner **GO**, M-001 used `CefSharp.OffScreen.NETCore` (never validating WPF hosting or Windows 10), yet `Switchly.sln` **already exists** with 5 `src/` projects and `tests/Switchly.Infrastructure.Tests`.

Proceeding with M-002/M-003 on top of unvalidated WPF hosting, unrecorded sign-in evidence, unresolved core ADRs (`ADR-001`, `ADR-002`, `ADR-004`–`ADR-008`), and conflicting security/schema rules violates `AGENTS.md` §2 (L51), §6 (L130), and §8 (L153).

---

## Findings

### [Blocker] B-1: Severe Contradictions on Project Phase, M-001 Completion, and M-002 Readiness
- **Affected Scope:**
  - `README.md` L3, L43, L53, L125–127
  - `plan.md` L3, L22, L41, L150–152
  - `decisions.md` L10–17, L47 (`ADR-003`), L133 (`OPEN-008`)
  - `testing.md` L3, L69–73, L148–156
  - `architecture.md` L3, L238–248
  - `prd.md` L3
  - `artifacts/engine-spike/task-006-go-nogo.md` L10–15, L22–24, L30–37
  - Workspace root `Switchly.sln` L8–20
- **Simplicity & Coherence Rationale:**
  - **Self-contradiction inside `plan.md`:** L3 & L150–151 claim M-001 is `Complete` and M-002 is `In progress` with `"Sign-in gate PASS (all 4 providers, manual); TASK-007..010 remaining"`, while **L152 immediately underneath** states `M-002 to M-009 | Not started | No code or test execution has been performed`. Meanwhile, `Switchly.sln` already contains `Switchly.App`, `Domain`, `Application`, `Infrastructure`, `BrowserHost`, and `Switchly.Infrastructure.Tests` (contradicting both `README.md` L3/L53 and `plan.md` L151–152).
  - **Unverified claims vs. M-001 evidence:** `decisions.md` L47 (`ADR-003`) and `plan.md` L3/L151 claim all four providers' sign-in passed, but `artifacts/engine-spike/task-006-go-nogo.md` L12 & L22–23 explicitly states `Provider sign-in | NOT TESTED (tier 2) | do not claim login compatibility` and L37 leaves product-owner GO open. `testing.md` L3 still says `all tests planned; none have been executed`, with zero evidence paths or version logs per `testing.md` §9 and `AGENTS.md` §6.
  - **Premature M-001 sign-off on core engine risks:** `plan.md` L22, `architecture.md` L241/L246, and `testing.md` §4 (L73) require M-001 to prove **WPF window hosting**, `TEST-006`, and **Windows 10** compatibility (`REQ-014`). Instead, M-001 ran a console app using `CefSharp.OffScreen.NETCore` (`plan.md` L41), experienced an intermittent native `CreateBrowser` crash (`task-006-go-nogo.md` L30), and reported **no Windows 10 or WPF hosting tests** — yet `ADR-003` is marked `Validated` for `"C# + WPF + CEF"`.
  - **Unapproved foundational ADRs during active scaffolding:** `ADR-001` (WPF), `ADR-002` (.NET 10), `ADR-004` (SQLite), `ADR-005` (DPAPI/Hello), `ADR-006` (AES-GCM), and `ADR-007` (Logging) are all still `Proposed` in `decisions.md` L10–16 while `Switchly.sln` is already scaffolded on those choices.
- **Concrete Recommendation:**
  1. Reconcile all status banners (`README.md`, `prd.md`, `architecture.md`, `testing.md`, `plan.md` §6, `decisions.md`) to one single truthful state matching actual git/workspace state.
  2. Attach a concrete redacted evidence artifact for the claimed M-002 manual sign-in pass (with OS/CEF versions and test dates per `testing.md` §9) OR revert `plan.md` L3/L151 and `ADR-003` L47 back to `NOT TESTED / Partial`.
  3. Explicitly re-scope WPF hosting validation (`CefSharp.Wpf.NETCore` multi-window/focus/DPI/non-elevated stability) and Windows 10 decision (`ADR-002`/`OPEN-002`) as an explicit M-002 exit gate before starting M-003, and record explicit Product Owner sign-off on `ADR-001`, `ADR-002`, and `ADR-004`.

---

### [Blocker] B-2: Direct Canonical Conflict & Root-Problem Violation on Credential Vault / SQLite Password Storage
- **Affected Scope:**
  - `AGENTS.md` §5 L114
  - `schema.md` §1 L8–9, §2 L27, §3 (`ENT-009 VaultCredential`) L126–136, §9 L240
  - `prd.md` §4.1 `REQ-008` L55, `REQ-010` L57, §7 L154, §8 L179
  - `architecture.md` §7 L181
  - `rules.md` §4 L46–47
  - `design.md` §3 (`UI-001`..`UI-013`) L49–63
- **Simplicity & Coherence Rationale:**
  - **Hard rule conflict:** `AGENTS.md` L114 states: *"Never store website passwords, cookies, OAuth tokens, or browser-session material in the application SQLite database or logs."* Conversely, `schema.md` §1–3 (`ENT-009 VaultCredential`) stores website usernames and passwords (encrypted) inside `switchly-next.db`, while `prd.md` L154 and `architecture.md` L181 only prohibit *plaintext* passwords in SQLite.
  - **Severe YAGNI / Root-Problem-Focus violation (`zoom-out-standard.md` §1–2):** `schema.md` §9 (L240) admits password autofill is **not promised** pending PoC, and `design.md` §2–3 defines **no vault viewer/copy/autofill UI at all** (`UI-010` is only app/vault unlock). Building a custom encrypted password manager (`ENT-009`, DPAPI wrapping, CSV parser, master password + Windows Hello unlock, cross-device vault re-encryption, and an unspecified key-recovery flow per `schema.md` L234) when users have no defined way to autofill or even view those passwords adds massive security surface and implementation drag without serving the root problem (isolated multi-account browsing).
- **Concrete Recommendation:**
  1. Resolve the `AGENTS.md` L114 vs. `schema.md` `ENT-009` conflict via explicit Product Owner decision.
  2. **Architectural simplification (strongly recommended):** Defer custom CSV password import, `ENT-009 VaultCredential`, master password, and Windows Hello unlock (`REQ-008` CSV part, `REQ-010` custom vault/unlock) out of MVP. Let Chromium's isolated per-profile storage handle normal web sign-in (or let users keep using dedicated password managers). If CSV password import MUST stay in MVP, either move vault storage to a separate encrypted store outside `switchly-next.db` (or amend `AGENTS.md` L114 explicitly) AND define the missing UX screen + key-recovery contract before M-006/M-007.

---

### [Blocker] B-3: Architectural Dead-End — Unattended Scheduled Encrypted Backups vs. "No Stored Passphrase" Rule
- **Affected Scope:**
  - `prd.md` §4.1 `REQ-011` L58, `FLOW-004` L109–113, §8 L178
  - `schema.md` §3 `ENT-011 BackupJob` L144–153, §8 L233
  - `rules.md` §4 L47
  - `architecture.md` §6 Flow C L169–175
  - `decisions.md` `ADR-006` L77–84, `ADR-011` L119
  - `plan.md` `M-007` (`TASK-032`) L114
- **Simplicity & Coherence Rationale:**
  - **Logical impossibility:** `REQ-011` and `TASK-032` require **scheduled automatic encrypted backups**, yet `rules.md` L47 and `schema.md` L153 strictly forbid storing the backup passphrase or derived key. An unattended scheduled job cannot derive an AES-256-GCM key from an unstored passphrase (`ADR-006`).
  - **Operational dead-end on live profiles & disk bloat:** `architecture.md` L170 requires blocking writes or shutting down profiles for a consistent snapshot, which conflicts with `design.md` L148 / `rules.md` L113 (never disrupt or discard active tabs without consent). Furthermore, `architecture.md` L171 fails to propagate `ADR-011` L119 (exclude CEF global state in `Profiles/`, back up only `<profile-guid>` dirs) and omits cache-directory exclusion policy — copying raw Chromium HTTP/GPU/Code caches across 7 daily snapshots (`schema.md` L233) will bloat storage by gigabytes.
- **Concrete Recommendation:**
  1. **Simplify MVP (`REQ-011`):** Make MVP backup **manual-only** (user enters passphrase interactively, chooses profiles or closes active ones cleanly). Defer scheduled background backups (`TASK-032`, `REQ-017`) until an explicit cryptographic design (e.g., asymmetric public-key encryption where only the public key is stored locally for unattended encryption while the private key is passphrase-protected, OR DPAPI-wrapped local key with explicit portability trade-off) is approved in `ADR-006`.
  2. Propagate `ADR-011` L119 into `architecture.md` §6 Flow C and `rules.md` §7: explicitly specify backing up only `<profile-guid>` directories (excluding CEF global root state and regenerable cache subdirectories).

---

### [Major] M-1: Dual-Ownership Complexity for Bookmarks, History, Downloads, and Unspecified Auto-Grouping Source
- **Affected Scope:**
  - `architecture.md` §7 L179–180, §6 Flow A L158
  - `schema.md` §1 L4, L13; §2–3 `ENT-002` L52–59, `ENT-005`–`ENT-007` L81–111; §8 L230
  - `decisions.md` `ADR-004` L59–66
  - `prd.md` `REQ-004` L52, `REQ-006` L54
- **Simplicity & Coherence Rationale:**
  - Mirroring Chromium's `HistoryEntry`, `DownloadRecord`, and `Bookmark` into application SQLite while CEF Chrome Runtime maintains its own authoritative Chromium databases inside `<profile-guid>` creates a **split-brain state model** (`zoom-out-standard.md` §2 Minimalist Data Flow, §4 Leverage Existing). Every clear-history, download cancellation, redirect, or restore operation now has to synchronize two storage engines.
  - Worse, `REQ-004` / `ENT-002` (`WebsiteDomain` auto-grouping) never specifies *which signal* generates automatic groups (startup URL? bookmarks? top visited history domains? every visited domain?). Deriving it from `HistoryEntry` without a clear rule would flood the sidebar with OAuth/CDN/redirect domains.
- **Concrete Recommendation:**
  1. Define a single, minimal source of truth for `WebsiteDomain` auto-grouping (e.g., profile's `StartupUrl` host + explicitly pinned/bookmarked origins or top-level user-navigated domains, rather than raw history).
  2. Re-evaluate whether `HistoryEntry` and `DownloadRecord` need full custom SQLite persistence + custom UIs (`UI-006`, `UI-007`) in MVP, or whether lightweight in-session tracking / minimal app-owned bookmarks + native CEF behavior suffices to cut significant M-004 complexity.

---

### [Major] M-2: Per-Profile Cross-Process Locking Conflicts with CEF Chrome Runtime's `RootCachePath` Singleton Model
- **Affected Scope:**
  - `architecture.md` §5.2 L140
  - `rules.md` §3 L37
  - `testing.md` `TEST-017` L49
  - `decisions.md` `ADR-011` L114–120
- **Simplicity & Coherence Rationale:**
  - Under `ADR-011`, all profiles live as direct children of a single `CefSettings.RootCachePath` (`%LOCALAPPDATA%\SwitchlyNext\Profiles`). In CEF Chrome Runtime, Chromium enforces a process-level singleton lock (`SingletonLock`) on `RootCachePath` — two separate Switchly Next app processes **cannot** concurrently host CEF against the same `RootCachePath`.
  - Designing per-profile cross-process lock files with stale-lock recovery (`architecture.md` L140) solves the wrong layer and creates a dead-end when a second Switchly Next process tries to initialize CEF on the same `RootCachePath`.
- **Concrete Recommendation:**
  - Replace per-profile cross-process mutex/lock-file machinery with a **single-instance application guard** (one app-level named mutex + IPC handoff to focus the running Switchly Next instance), paired with simple in-process `ProfileId` state guards inside the single browser host. Update `architecture.md` L140, `rules.md` L37, and `TEST-017` accordingly.

---

### [Major] M-3: Undefined Shell Window Topology (Single-Workspace Pane vs. Detached Per-Profile Windows)
- **Affected Scope:**
  - `prd.md` `REQ-001` L49, `FLOW-002` L94–98
  - `design.md` §2 L18–31, §3 (`UI-001`, `UI-003`) L51–53, §4.1–4.2 L67–83, §6 L131
  - `architecture.md` §5–6 L121–160
  - `schema.md` `ENT-004` L68–79
- **Simplicity & Coherence Rationale:**
  - The specs never decide whether a profile's tabs live *embedded inside* the Main Workspace window (`design.md` §2 shows `Workspace` + `Browser Area` as siblings) or open in *separate* `UI-003 Browser Window` OS windows (`design.md` §3, §4.1 L70), or support detaching/reattaching between them.
  - In WPF + CefSharp, switching/reparenting `ChromiumWebBrowser` controls across WPF windows has major native-HWND lifecycle, focus, and DPI consequences. Additionally, `ENT-004` has a `WindowId` GUID with no `BrowserWindow` entity to persist window placement/monitor state (`design.md` L131).
- **Concrete Recommendation:**
  - Lock **one simple window model** before finishing `M-002` (`TASK-007`): e.g., Main Workspace window hosts the sidebar + active profile's tabs, plus optional secondary detached `BrowserWindow` instances (each bound permanently to one `ProfileId`, no cross-window HWND reparenting in MVP). Document this explicitly in `architecture.md` and `design.md`.

---

### [Major] M-4: YAGNI Violations — Deferred Extensions, Speculative Freemium Plumbing, and Redundant Module Boundaries
- **Affected Scope:**
  - **Extensions baked into MVP structure:** `architecture.md` §4 L79 (`Switchly.Domain/Extensions/`), L102 (`Switchly.BrowserHost/OptionalExtensions/`), §6 Flow B L161–167; `schema.md` §2 L26, §3 `ENT-008` L113–124, §6 L209 (`ExtensionCount`); `design.md` L35, L58 (`UI-008`); `prd.md` `FLOW-005` L117–122; `plan.md` `M-005` L26/L95–99, `TASK-027` L106.
  - **Speculative Freemium in Must-MVP:** `prd.md` §4.1 `REQ-013` L60 (listed under `Must — MVP inti`, contradicting §4.3 L81); `design.md` L44, L63 (`UI-013 License / Pro`); `testing.md` `TEST-015` L47.
  - **Duplicate Browser Layer:** `architecture.md` §4 L92 (`Switchly.Infrastructure/BrowserEngine/`) vs. L98 (`Switchly.BrowserHost/`).
- **Simplicity & Coherence Rationale:**
  - `AGENTS.md` L116 forbids adding extension complexity unless `REQ-007` is reopened, yet extension namespaces, DB entities, DTO fields, and UI catalog entries are wired into the canonical baseline.
  - `REQ-013` / `UI-013` / `TEST-015` ask to build entitlement gates and an upgrade screen when `OPEN-001` & `OPEN-005` confirm zero Pro limits, benefits, or licensing providers exist.
  - Having both `Switchly.Infrastructure/BrowserEngine` and a dedicated `Switchly.BrowserHost` project duplicates responsibilities with no documented distinction (`architecture.md` L115–116).
- **Concrete Recommendation:**
  1. Remove `Extensions/` folders, `ENT-008`, `ExtensionCount`, and `UI-008` from the MVP schema/architecture baseline (keep only a single `REQ-007` deferred note in `prd.md` and `OPEN-003` in `decisions.md`).
  2. Move `REQ-013`, `UI-013`, and `TEST-015` out of MVP (`Must`) into post-MVP (`M-009`).
  3. Delete `Switchly.Infrastructure/BrowserEngine/` from `architecture.md` §4 so `Switchly.BrowserHost` is the sole home of browser-engine code.

---

### [Major] M-5: Schema & Traceability Gaps Blocking M-002 / M-003 Implementation
- **Affected Scope:**
  - `schema.md` `ENT-001` L34–49, `ENT-002` L52–59
  - `prd.md` `REQ-015` L66, `AC-015` L141
  - `design.md` L20
  - `plan.md` §2 L22–28, §3 L52/L57, §4 L85
  - `testing.md` `TEST-001`, `TEST-006`, `TEST-014`, `TEST-015`, `TEST-021`, `TEST-023`
- **Simplicity & Coherence Rationale:**
  - **Missing schema fields:** `REQ-015` / `TEST-023` / `TASK-015A` require profile favorites, and `schema.md` L59 says users may hide auto-generated `WebsiteDomain` groups — yet `ENT-001 BrowserProfile` has **no `IsFavorite` field**, `ENT-002 ProfileGroup` has **no `IsHidden` field**, and `ENT-001.PersistSessionCookies` (L48) has **no chosen default** despite `TASK-009` needing initial EF Core migrations now.
  - **Broken milestone traceability in `plan.md` §2:**
    - `TEST-001` (`REQ-001`/`REQ-014`), `TEST-014` (`REQ-012`), and `TEST-015` (`REQ-013`) are **never mapped to any milestone exit criterion** in `plan.md` §2 (M-002 L23 lists zero `TEST-###` IDs).
    - `TEST-006` (multi-window E2E) is wrongly listed as an M-001 PoC exit criterion (L22), while `TEST-021` (tested in TASK-003/004 and cited in `ADR-003`) is missing from M-001's exit criteria.
    - `M-007` (Security & Backup, L28) unnecessarily depends on `M-006` (Browser Import), blocking backup delivery on Chrome/Edge import parsers.
- **Concrete Recommendation:**
  - Add `IsFavorite` (default `false`), `IsHidden` (on `ENT-002`, default `false`), and a concrete default for `PersistSessionCookies` to `schema.md` before `TASK-009` locks migrations.
  - Fix the `plan.md` §2 traceability table (`TEST-001`, `TEST-014` in M-002/M-008; remove `TEST-006` from M-001; decouple `M-007` from `M-006`).

---

### [Major] M-6: MVP Scope Bloat Relative to Team Size & Root Problem
- **Affected Scope:** `prd.md` §4.1 (`REQ-001`..`REQ-014`), `plan.md` L11, `design.md` §3 (`UI-001`..`UI-013`)
- **Simplicity & Coherence Rationale:**
  - For *"one developer or small AI-assisted team"* (`plan.md` L11), marking **all** of: multi-window isolated CEF workspace, custom Bookmark/History/Download managers, full Chrome/Edge 5-category Import Wizard (parsing proprietary Chromium SQLite + session formats + CSV), custom encrypted password vault + DPAPI + master password + Windows Hello, manual + scheduled portable encrypted backup/restore with atomic staging, AND Freemium entitlement scaffolding as **`Must — MVP inti`** creates extreme delivery risk before validating whether real users even adopt the core workspace.
- **Concrete Recommendation:**
  - Split MVP into **v0.1 Core Daily Driver** (`M-001`–`M-004` + minimal manual encrypted backup) and **v0.2 Migration & Hardening** (Chrome/Edge bookmark+URL import, scheduled backups, optional local lock). Cut custom password vault and Freemium plumbing out of MVP entirely.

---

### [Nit] N-1: Altitude Drops (Premature Code Details) in High-Level Specs
- **Affected Scope:** `schema.md` §6 L196–215; `rules.md` §1 L11, §5 L58
- **Simplicity Rationale (`zoom-out-standard.md` §3 Anti-Weeds):**
  - `schema.md` §6 embeds concrete C# `public sealed record` DTOs (including `ExtensionCount` for a deferred feature) while admitting they are unimplemented illustrations.
  - `rules.md` L58 hardcodes concrete C# interface names (`IBrowserRuntime`, `IBrowserProfileContext`, `IBrowserWindowHost`) that are never defined or referenced in `architecture.md`, creating premature interface lock-in.
- **Concrete Recommendation:**
  - Replace C# record snippets in `schema.md` §6 with high-level contract descriptions (inputs/outputs/invariants), and either define the browser-host port boundaries in `architecture.md` or replace specific type names in `rules.md` L58 with a boundary-level rule.

---

### [Nit] N-2: Version / Toolchain / Language Drift Across Docs
- **Affected Scope:**
  - `README.md` L24, L32, L43, L116
  - `prd.md` L173
  - `architecture.md` L12–35, L28
  - `decisions.md` L54, L133 (`OPEN-008`)
  - `design.md` L133
  - `testing.md` L113, L115, L119
  - `rules.md` L126
- **Simplicity Rationale:**
  - **CEF mode history drift:** `README.md` L116 & `architecture.md` L28 say Chrome Runtime is the only mode *"from CEF 156+"*, whereas `prd.md` L173 & `decisions.md` L54 accurately note Alloy was removed in **CEF 128**.
  - **Stale open items & choices:** `OPEN-008` (`decisions.md` L133) is already confirmed and M-001 is past that step; `xUnit or NUnit (choose at scaffold)` (`README.md` L24, `testing.md` L115) is still listed as open even though `tests/Switchly.Infrastructure.Tests` already exists in `Switchly.sln`.
  - **UI localization mismatch:** `design.md` L133 declares Bahasa Indonesia as MVP UI language (`English UI: Terbuka`), yet nearly all UI labels in `prd.md` and `design.md` (`New Profile`, `Favorites`, `All Profiles`, `Import Wizard`, `Open URL in profile...`) are in English, and `architecture.md` mixes Indonesian and English mid-document.
- **Concrete Recommendation:**
  - Standardize the CEF 128 vs. 156+ phrasing, close `OPEN-008`, pin the actual test framework already chosen in `Switchly.Infrastructure.Tests`, and clarify MVP UI copy language (Indonesian vs. English) consistently in `design.md`.

---

## Priority Remediation Sequence Before Continuing M-002
1. **Fix `B-1` (Truth & Gate Alignment):** Reconcile all doc statuses with `Switchly.sln` and `task-006-go-nogo.md`; verify/log real WPF hosting, provider sign-in evidence, and Win10 policy (`ADR-002`/`OPEN-002`); approve `ADR-001`, `ADR-002`, `ADR-004`.
2. **Resolve `B-2` & `B-3` (Security & Backup Architecture):** Eliminate the `AGENTS.md` L114 vs. `ENT-009` conflict (ideally defer custom password vault from MVP) and make MVP backup manual-only with `ADR-011` directory rules propagated.
3. **Lock `M-2`, `M-3`, `M-5` (Core Shell & Schema):** Adopt single-instance app locking instead of per-profile cross-process locks, lock the WPF window topology, and patch missing schema fields (`IsFavorite`, `IsHidden`, `PersistSessionCookies` default) before `TASK-009` migrations solidify.
4. **Trim `M-1`, `M-4`, `M-6` (YAGNI):** Strip deferred extension and speculative Freemium artifacts from the MVP baseline and remove `Infrastructure/BrowserEngine` duplication.
