# Structural & Traceability Audit Report — Switchly Next Canonical Specification Suite

**Auditor:** Structural Goldfish (`egm-structural`, fresh-context Goldfish subagent `a2c6957f-31fe-4d9a-8baf-1ad48b2e1c33`)  
**Date:** 2026-11-24  
**Standard Evaluated Against:** `.agents/plugins/egm/resources/document_standards/feature-spec-standard.md` + cross-document canonical rules (`AGENTS.md`, `GEMINI.md`, `README.md`, `prd.md`, `plan.md`, `architecture.md`, `schema.md`, `design.md`, `rules.md`, `testing.md`, `decisions.md`).  
**Summary Counts:** 8 Blockers | 11 Majors | 6 Nits

---

## Blockers

### B-01 — Internal Status Contradiction & Duplicate `M-002` Row in Progress Tracker
1. **Severity:** Blocker
2. **Affected Scope:** `plan.md` line 3 and Section 6 (`Progress tracker`, lines 148–152).
3. **Discrepancy Description & Rationale:** `plan.md:151` states `| M-002 | In progress | Sign-in gate PASS (all 4 providers, manual); TASK-007..010 remaining |`, immediately followed on `plan.md:152` by `| M-002 to M-009 | Not started | No code or test execution has been performed |`. `M-002` appears twice with mutually exclusive statuses (`In progress` vs `Not started`), and line 152's evidence ("No code or test execution has been performed") directly contradicts lines 3, 150, and 151.

### B-02 — Suite-Wide Project Phase, Version-Pinning, and Test-Execution Contradiction
1. **Severity:** Blocker
2. **Affected Scope:**
   - Claims M-001 complete / tests passed / version pinned: `plan.md:3, 150–151`; `decisions.md:12, 20, 47, 116`; `architecture.md:35`.
   - Claims specification-only / M-001 not started / zero tests executed / versions unpinned: `README.md:3, 19, 43, 53, 125, 127`; `prd.md:3, 173`; `architecture.md:3, 28, 238–248`; `testing.md:3, 114`; `rules.md:132`; `AGENTS.md:23`; `decisions.md:54, 100, 133`.
3. **Discrepancy Description & Rationale:** Half the canonical suite reports M-001 complete, M-002 in progress, `CefSharp.Wpf.NETCore 156.0.300` pinned, and `TEST-003/004/017/021` + all 4 provider sign-ins passing. The other half asserts "Specification only — no application code has been implemented or tested" (`README.md:3`), "all tests planned; none have been executed" (`testing.md:3`), and "Start with M-001" (`README.md:125`). Both `architecture.md` (line 35 vs lines 3, 28, 238) and `decisions.md` (`ADR-003` line 47 "Validated" vs line 54 "Validate... in M-001") contradict themselves internally. This violates `feature-spec-standard.md` Core Principle (a fresh reader cannot determine actual project state).

### B-03 — `M-001` Marked Complete Despite Unverified / Omitted M-001 Gate Obligations
1. **Severity:** Blocker
2. **Affected Scope:** `plan.md:22, 41, 52, 57, 150, 156`; `architecture.md:241, 246`; `testing.md:69, 73`; `decisions.md:47, 133`; `AGENTS.md:153`.
3. **Discrepancy Description & Rationale:** `AGENTS.md:153` forbids marking a milestone complete while any required acceptance criterion is unverified, and `plan.md:156` requires all linked REQ/AC/TEST obligations to be verified or explicitly deferred with owner approval. However, `plan.md:150` marks `M-001` Complete while:
   - `architecture.md:241` requires M-001 to test "WPF window hosting", whereas `plan.md:41` (`TASK-002`) and `decisions.md:133` (`OPEN-008`) used a `.NET 10` console app with `CefSharp.OffScreen.NETCore`.
   - `plan.md:22` maps `REQ-014` to M-001 and `architecture.md:246` / `testing.md:73` require testing target Windows 10 editions in M-001, yet `TEST-001` is omitted from `plan.md:22` Exit criteria and neither `plan.md:150` nor `decisions.md:47` records Windows 10 verification.
   - `plan.md:150` notes `TASK-003` provider sign-in was deferred out of M-001 to M-002.
   - `TASK-003` (`plan.md:52`) and `TASK-004` (`plan.md:57`) require `TEST-021` (`REQ-006`/`AC-007`), which is omitted from M-001's Milestone index row (`plan.md:22`).

### B-04 — Missing Acceptance Criterion (`AC-###`) for Must-Have `REQ-010` & Mismatched Test Mappings
1. **Severity:** Blocker
2. **Affected Scope:** `prd.md:57` (`REQ-010`), `prd.md:124–145` (Section 6 `AC-001`..`AC-018`), `testing.md:42–43, 54` (`TEST-010`, `TEST-011`, `TEST-022`).
3. **Discrepancy Description & Rationale:** Must-have requirement `REQ-010` ("perlindungan lokal melalui akun Windows/DPAPI, opsi master password, dan Windows Hello jika tersedia") has **no** corresponding `AC-###` in `prd.md` Section 6 covering DPAPI key wrapping, master password, or Windows Hello unlock (nor general secret redaction in logs/crash dumps). `AC-010` (`prd.md:135`) solely specifies Password CSV import (`REQ-008`). As a result, `testing.md:43` (`TEST-011` — app lock/unlock via Windows account/master password/Hello) and `testing.md:54` (`TEST-022` — secret redaction in logs/crash reports) are forced to map to `AC-010`, creating broken AC-to-TEST traceability for a core security requirement.

### B-05 — Authoritative Schema Missing `IsFavorite` Field Required by `REQ-015` / `AC-015` / `TEST-023`
1. **Severity:** Blocker
2. **Affected Scope:** `schema.md:19, 34–49` (`ENT-001 BrowserProfile`) vs `prd.md:66, 141` (`REQ-015`, `AC-015`), `design.md:20` (`Workspace -> Favorites`), `testing.md:55` (`TEST-023`), `plan.md:24, 85` (`M-003`, `TASK-015A`).
3. **Discrepancy Description & Rationale:** `ENT-001 BrowserProfile` defines `ColorHex`, `IconKey`, and `StartupUrl`, but has **no `IsFavorite` column** (and no separate Favorites table exists in `schema.md`). Consequently, `REQ-015` / `AC-015` / `TEST-023` ("Favorite and appearance persist") cannot be persisted under `schema.md`. Additionally, `schema.md:19` only maps `ENT-001` to `REQ-002, REQ-003`, omitting `REQ-015`.

### B-06 — Five Orphaned Test Cases in `plan.md` (Including Three MVP Must-Have Tests)
1. **Severity:** Blocker
2. **Affected Scope:** `testing.md:33, 40, 46, 47, 51` vs `plan.md:20–30, 70–133`.
3. **Discrepancy Description & Rationale:** Five of the 26 traceable test cases are completely unreferenced anywhere in `plan.md` (neither in Section 2 Milestone index Exit criteria nor in any `TASK-###`):
   - `TEST-001` (`REQ-001, REQ-014` / `AC-001, AC-014` — **Must-have** OS install/launch; omitted from M-001, M-002, M-008, M-009, and `TASK-037`).
   - `TEST-014` (`REQ-012` / `AC-012` — **Must-have** offline/no-registration basic use; omitted from M-002 and M-009).
   - `TEST-015` (`REQ-013` / `AC-013` — **Must-have** Free/Pro entitlement boundary; omitted from M-009).
   - `TEST-008` and `TEST-019` (`REQ-007` / `AC-008` — Optional extension tests; omitted from M-005).

### B-07 — Architectural Contradiction Between Unattended Scheduled Backups and Forbidden Passphrase Storage
1. **Severity:** Blocker
2. **Affected Scope:** `prd.md:58, 110–112` (`REQ-011`, `FLOW-004`), `design.md:106–107`, `plan.md:114` (`TASK-032`) vs `schema.md:153` (`ENT-011`), `rules.md:47`, `decisions.md:82` (`ADR-006`).
3. **Discrepancy Description & Rationale:** `REQ-011`, `FLOW-004`, and `TASK-032` require scheduled automatic encrypted backups, yet `FLOW-004` step 3 (`prd.md:112`) and `design.md:107` require the user to enter a backup passphrase, while `schema.md:153` ("No backup passphrase or derived key stored") and `rules.md:47` ("Do not store master password, backup passphrase, encryption key or recovery key in SQLite") forbid persisting the passphrase/key, and `ADR-006` specifies symmetric passphrase-derived AES-256-GCM with no mechanism (e.g., DPAPI-wrapped backup key or public-key wrapping) for unattended scheduled runs.

### B-08 — Governance Authority Mismatch: `AGENTS.md:43` Requires "Accepted ADRs", Zero ADRs Use `Accepted`
1. **Severity:** Blocker
2. **Affected Scope:** `AGENTS.md:43` vs `decisions.md:3, 8–20, 47, 104, 110, 116`.
3. **Discrepancy Description & Rationale:** `AGENTS.md:43` sets Authority Rank 3 to `Accepted ADRs in decisions.md`. However, `decisions.md` is marked `Status: DRAFT` (`line 3`) and individual ADRs use `Proposed`, `Validated`, `Open`, `Confirmed scope` (index) / `Confirmed by user` (`ADR-009` line 104) / `Confirmed by user selection` (`ADR-010` line 110). Not a single ADR carries status `Accepted`, creating ambiguity over which ADRs hold canonical Rank-3 authority.

---

## Majors

### M-01 — Severe Bilingual / Language-Mixing Across and Within Canonical Documents
1. **Severity:** Major
2. **Affected Scope:** Suite-wide (`prd.md`, `design.md` mostly Indonesian; `AGENTS.md`, `GEMINI.md`, `README.md`, `plan.md`, `schema.md`, `rules.md`, `testing.md`, `decisions.md` mostly English; `architecture.md` split mid-file).
3. **Discrepancy Description & Rationale:** No canonical documentation language is declared, and 8 of 11 files mix Indonesian and English internally:
   - `architecture.md`: English intro (`line 9`), Indonesian bullets (`lines 12–20`), Indonesian heading/table headers (`lines 22–25`), Indonesian rows (`lines 26–27`), English rows (`lines 28–33`), then switches entirely to English (`lines 37–248`).
   - `prd.md`: English/hybrid headings (`lines 45, 63, 146`) and mid-sentence switch to English (`line 149`: `...dan extension data when an extension is installed; extensions remain optional.`).
   - `design.md`: English heading (`line 15`), English cell in `UI-008` (`line 58`), and entire subsection `4.4` in English (`lines 99–103`) while `4.1–4.3` and `4.5` are Indonesian.
   - `README.md:43`, `rules.md:126`, `testing.md:119`: Indonesian phrase `Direncanakan — verifikasi setelah scaffolding` inside English sentences.
   - `schema.md:233, 236`: Indonesian keyword `` `Terbuka` `` and heading `## 9. Terbuka` in an otherwise English document.
   - `decisions.md:4`: Indonesian metadata label `**Tanggal dokumen:** 23 November 2026` in an otherwise English document.

### M-02 — Incomplete `REQ-002` Coverage in `AC-002` and `TEST-002` (Missing Duplicate-Config Session-Isolation Check)
1. **Severity:** Major
2. **Affected Scope:** `prd.md:50` (`REQ-002`), `prd.md:127` (`AC-002`), `testing.md:34` (`TEST-002`), `rules.md:38`.
3. **Discrepancy Description & Rationale:** `REQ-002` mandates profile create, edit, **duplicate configuration (without silently copying secret sessions)**, open, close, archive, and delete. `AC-002` only verifies that a new profile receives a unique storage directory and local metadata entry (omitting edit, duplicate, archive, delete). `TEST-002` tests create/edit/archive/delete, still **omitting duplicate configuration** and its critical security assertion that secret sessions/cookies are not copied.

### M-03 — `REQ-016` (Search & Filter) Scope Shrinkage, Missing `UI-###` Traceability, and Milestone Order Conflict
1. **Severity:** Major
2. **Affected Scope:** `prd.md:67, 142` (`REQ-016`, `AC-016`), `testing.md:56` (`TEST-024`), `design.md:49–63`, `plan.md:24–25, 85` (`M-003`, `M-004`, `TASK-015A`).
3. **Discrepancy Description & Rationale:**
   - `REQ-016` (`prd.md:67`) requires searching and filtering **profiles, tabs, bookmarks, and groups**, but `AC-016` (`prd.md:142`) and `TEST-024` (`testing.md:56`) drop **tabs and bookmarks**.
   - `REQ-016` does **not** appear in the `Requirement` column of any screen in `design.md` Section 3 (`lines 49–63`), even though `UI-001` lists `search`.
   - `plan.md:24, 85` assigns `REQ-016` (`TASK-015A`) exclusively to `M-003`, which precedes `M-004` (where tabs and bookmarks are introduced), making full `REQ-016` completion impossible in `M-003`.

### M-04 — Cross-Document Schema-to-UI and Architecture-to-UI Mismatches
1. **Severity:** Major
2. **Affected Scope:** `schema.md:47–48, 52–59, 126–136, 168–179, 232, 240`; `design.md:32–37, 49–63, 68, 73, 149`; `architecture.md:128, 134–139`.
3. **Discrepancy Description & Rationale:**
   - `schema.md:59` (`ENT-002 ProfileGroup`) states users may hide system-derived groups, but `ENT-002` (`lines 52–58`) defines **no `IsHidden` field**.
   - `schema.md:27, 126–136` (`ENT-009 VaultCredential`), `schema.md:232`, and `schema.md:240` assume local vault management/deletion, but `design.md` has **no IA entry or `UI-###` screen** for viewing/managing/deleting imported vault credentials (`UI-010` is unlock only).
   - `schema.md:47–48` (`RestoreLastSession`, `PersistSessionCookies`) and `architecture.md:128` ("user-visible privacy/session setting") have no corresponding controls in `design.md` Section 3 (`Profile Details` from IA `lines 32–37` has no `UI-###` entry; `UI-002` omits `RestoreLastSession`, `PersistSessionCookies`, `Storage`, and `Favorite`).
   - `design.md:61–62`: `UI-011` (Backup & Restore) lists `Schedule` in elements but omits `REQ-017` and `retention`; `UI-012` (Settings) maps `REQ-017, REQ-018` despite its elements (`Startup, appearance, resource, privacy`) omitting backup/retention; `UI-001` omits `REQ-018` despite Section 8 (`lines 145–147`) placing active profile counts and memory warnings in the workspace.
   - `design.md:54` (`UI-004` context menu) lists `Open, close, duplicate config, archive, delete` (missing `Edit` and `Favorite`), contradicting `design.md:73` (`open, close, edit, duplicate settings, archive, delete` — still missing `Favorite`).
   - `design.md:68` defines 4 profile status indicators (`closed, active, suspended, error`), omitting transitional states `Starting` and `Suspending` defined in `architecture.md:134–139`.
   - `REQ-003` is not mapped to any `UI-###` in `design.md:49–63`.
   - `schema.md:168–179` (`erDiagram`) omits `ENT-010 AppSetting` and `ENT-011 BackupJob`.

### M-05 — Missing Task-Level `REQ/AC/TEST` Traceability & Invalid `M-008` Dependency on Deferred `M-005`
1. **Severity:** Major
2. **Affected Scope:** `plan.md:29, 34–133`; `AGENTS.md:120`.
3. **Discrepancy Description & Rationale:**
   - `AGENTS.md:120` mandates that every task map to at least one `REQ-###`, `AC-###`, and `TEST-###` (or explain why it is internal-only). In `plan.md` Section 4 (`lines 70–133`), 39 of 40 tasks (`TASK-007`..`TASK-043`, except `TASK-038A` mentioning `TEST-026`) lack `REQ/AC/TEST` mappings; Section 3 (`TASK-001..006`) omits `REQ/AC` mappings and leaves `TASK-006` unmapped.
   - `plan.md:29` specifies `M-008` depends on `M-004 to M-007`, which literally includes **optional/deferred `M-005`**, blocking MVP hardening on a deferred post-MVP milestone.
   - `plan.md:119–132`: `M-008` and `M-009` omit the `- Exit:` line present on `M-002` through `M-007`.

### M-06 — Module Ownership Contradiction: `Switchly.Infrastructure/BrowserEngine/` vs `Switchly.BrowserHost/`
1. **Severity:** Major
2. **Affected Scope:** `architecture.md:74–102, 115–116`; `rules.md:19–20, 26`.
3. **Discrepancy Description & Rationale:** `architecture.md:92` includes a `BrowserEngine/` folder under `Switchly.Infrastructure/` alongside the dedicated `Switchly.BrowserHost/` project (`line 98`). This contradicts `architecture.md:115–116` and `rules.md:20, 26`, which assign all CefSharp/CEF calls and lifecycle exclusively to `Switchly.BrowserHost` and exclude browser engine ownership from `Switchly.Infrastructure`. Additionally, `Switchly.Domain/` (`architecture.md:74–82`) omits `Downloads` (`ENT-007`) and `Tabs`/`Sessions` (`ENT-004`).

### M-07 — `ADR-011` Consequences Not Propagated to Backup Flow, Architecture References, or Schema Path Constraint
1. **Severity:** Major
2. **Affected Scope:** `decisions.md:114–120` (`ADR-011`), `architecture.md:24–33, 127, 171`, `schema.md:42`.
3. **Discrepancy Description & Rationale:**
   - `ADR-011` (`decisions.md:119`) states `Profiles\` doubles as CEF's user-data dir (containing CEF global state + unused `Default` profile) and **backups must copy only `<profile-guid>` directories, never CEF global state**. `architecture.md:171` (`Flow C — Backup`) fails to exclude CEF global state, and `architecture.md:127` fails to reflect that global CEF state resides inside `Profiles\`.
   - `schema.md:42` (`ENT-001.StorageRelativePath`) does not constrain the value to a single path segment (`<profile-guid>`) despite `ADR-011`'s direct-child requirement.
   - `architecture.md` never references `ADR-009`, `ADR-010`, or `ADR-011` (Section 2 only references `ADR-001`..`ADR-008`).

### M-08 — `feature-spec-standard.md` Alternatives Rule Violations & Inconsistent ADR Structure in `decisions.md`
1. **Severity:** Major
2. **Affected Scope:** `decisions.md:12, 16, 57, 86–120`; `feature-spec-standard.md:13–21`.
3. **Discrepancy Description & Rationale:**
   - `ADR-007` (`lines 86–91`), `ADR-009` (`lines 102–106`), `ADR-010` (`lines 108–112`), and `ADR-011` (`lines 114–120`) omit an `Options` / alternatives evaluation, violating `feature-spec-standard.md` ("The Alternatives Rule"). `ADR-007` also omits `Consequences`; `ADR-009`/`010` omit `Context` and `Revisit when`; `ADR-008` (`lines 93–100`) replaces `Context`/`Decision`/`Consequences`/`Revisit when` with `Proposed default`/`Owner`/`Impact`/`Next validation`.
   - Only `ADR-003` has a `Related:` line in its body (`line 57`), and that line lists `REQ-007` which is missing from `ADR-003`'s Section 1 index row (`line 12`).
   - `ADR-007` (`line 16`) cites `NFR security`, which is not a defined ID anywhere in `prd.md`.

### M-09 — Fragmented Open Decisions, Unregistered Open Items, Closed `OPEN-008`, and Misnested Section
1. **Severity:** Major
2. **Affected Scope:** `decisions.md:122–141`; `prd.md:152, 170–181`; `schema.md:48, 233, 236–242`; `design.md:133, 141, 151–156`; `testing.md:23–24, 115`; `README.md:24`.
3. **Discrepancy Description & Rationale:**
   - `OPEN-001`..`OPEN-008` are **orphaned** outside `decisions.md` (zero cross-references in `prd.md`, `design.md`, `schema.md`, `plan.md`, or `README.md`).
   - `OPEN-008` (`decisions.md:133`) is marked `Confirmed 2026-11-24` yet remains inside `## 13. Open decisions with impact`.
   - `### Provider identity clarification (2026-11-24)` (`decisions.md:135–141`) is a confirmed decision erroneously nested as an H3 under `## 13. Open decisions with impact`.
   - At least 8 explicit open decisions across other canonical docs have **no `OPEN-###` ID** in `decisions.md`: telemetry opt-in policy (`prd.md:152`, `architecture.md:32, 202`), `PersistSessionCookies` default (`schema.md:48`), whole-DB SQLite encryption (`schema.md:241`), password autofill vs vault management (`schema.md:240`), English UI localization (`design.md:133`), dark/light theme validation (`design.md:141`), baseline test device specs (`testing.md:23–24`), and xUnit vs NUnit test framework choice (`README.md:24`, `testing.md:115`).

### M-10 — Contradictory CEF Chrome-Runtime Version Milestone (`CEF 156+` vs `CEF 128`)
1. **Severity:** Major
2. **Affected Scope:** `README.md:116`, `architecture.md:28` vs `prd.md:173`, `decisions.md:54`.
3. **Discrepancy Description & Rationale:** `README.md:116` claims `Chrome Runtime is the only CEF mode from CEF 156+`, whereas `prd.md:173` (`satu-satunya mode sejak CEF 128`) and `decisions.md:54` (`the Alloy bootstrap was removed in CEF 128`) place the transition at CEF 128.

### M-11 — Four ID Families Unlinked Across Documents (`FLOW-###`, `UI-###`, `ENT-###`, `NFR-PERF-###`)
1. **Severity:** Major
2. **Affected Scope:** `prd.md:86–123, 157–160`; `design.md:49–63`; `schema.md:17–30`; `architecture.md:20, 150–176`; `testing.md:29–58, 80–95`.
3. **Discrepancy Description & Rationale:**
   - `FLOW-001`..`FLOW-005` (`prd.md`) are never referenced in `design.md`, `architecture.md`, `testing.md`, or `plan.md`; `architecture.md:150–176` uses a parallel scheme (`Flow A`, `Flow B`, `Flow C`).
   - `UI-001`..`UI-013` (`design.md`) are never referenced outside Section 3's table (not even in `design.md` Section 4 or `plan.md`/`testing.md`).
   - `ENT-001`..`ENT-012` (`schema.md`) are never referenced by ID outside `schema.md`.
   - `NFR-PERF-001`..`NFR-PERF-004` (`prd.md:157–160`) are never explicitly mapped in `testing.md` (Section 3 or Section 5), and Security/Compatibility NFRs (`prd.md:148–155, 163–166`) lack IDs altogether.

---

## Nits

### N-01 — Non-Sequential `REQ-007` Gap & Table/List Asymmetry in `prd.md`
1. **Severity:** Nit
2. **Affected Scope:** `prd.md:45–70` (Sections `4.1` and `4.2`).
3. **Discrepancy Description & Rationale:** Section `4.1` (`Must`) jumps from `REQ-006` (`line 54`) to `REQ-008` (`line 55`) because `REQ-007` was moved to Section `4.2` (`Should`, `line 65`). Section `4.1` uses a Markdown table while Section `4.2` uses a bullet list.

### N-02 — Schema Section 3 Format Asymmetry, Missing Nullability, and Underspecified Bounds
1. **Severity:** Nit
2. **Affected Scope:** `schema.md:34–165, 190`.
3. **Discrepancy Description & Rationale:** `ENT-001` (`lines 34–49`) is a structured table (`Field | Type | Required | Constraints`), while `ENT-002`..`ENT-012` (`lines 52–165`) are bullet lists that frequently omit SQLite types/nullability. Specifically, `CompletedAtUtc` is not marked nullable on `ENT-007` (`line 110`, states include `InProgress`) or `ENT-012` (`line 163`, states include `Preview`/`Running`), unlike `ENT-011` (`line 149`). `ENT-009` (`line 133`) does not specify whether `Nonce`/`AuthenticationTag` are per ciphertext column or shared. `schema.md:190` requires row-size and archive-entry limits without defining concrete thresholds.

### N-03 — Inconsistent Document Status/Metadata Headers & Stale Date
1. **Severity:** Nit
2. **Affected Scope:** Header blocks of all 11 documents; specifically `decisions.md:4` vs `lines 133, 135`.
3. **Discrepancy Description & Rationale:** Header metadata is non-uniform across the suite (`Version` appears separately in `prd.md`, is embedded in `Status` in `schema.md`/`design.md`/`rules.md`/`testing.md`, and is absent in `plan.md`/`architecture.md`/`decisions.md`/`README.md`/`AGENTS.md`; `GEMINI.md` has no header metadata). `decisions.md:4` retains `23 November 2026` despite `2026-11-24` entries on lines 133 and 135.

### N-04 — Non-Uniform Task IDs & Shorthand Test IDs in `plan.md`
1. **Severity:** Nit
2. **Affected Scope:** `plan.md:22–28, 85, 116, 125`; `AGENTS.md:104`.
3. **Discrepancy Description & Rationale:** `TASK-015A`, `TASK-033A`, and `TASK-038A` break the `TASK-###` naming convention (`AGENTS.md:104`). Section 2 Exit criteria abbreviates test IDs (`TEST-002 to 006`, `007, 021`, `010, 018, 022`, `TEST-011 to 013`), breaking exact-string traceability lookup.

### N-05 — Canonical Document List & Authority Order Omissions
1. **Severity:** Nit
2. **Affected Scope:** `GEMINI.md:5`, `AGENTS.md:29–49`, `README.md:102–112`, `prd.md:183–193`.
3. **Discrepancy Description & Rationale:** `GEMINI.md:5` omits `README.md` from its source-of-truth list; `AGENTS.md:29–38`, `README.md:102–112`, and `prd.md:183–193` omit `GEMINI.md`; `AGENTS.md:40–49` Authority Order omits `README.md`, `AGENTS.md`, and `GEMINI.md`.

### N-06 — Minor Prose, Traceability, and Markdown Formatting Glitches
1. **Severity:** Nit
2. **Affected Scope:**
   - `prd.md:13`: Includes `ekstensi` in the core goal without the optional/deferred caveat (compare `prd.md:149`).
   - `prd.md:103` (`FLOW-003`): Lists `ekstensi` as a standard selectable import category without the informational-only/non-MVP caveat (`REQ-008`, `design.md:91`).
   - `prd.md:126` (`AC-001`) & `prd.md:139` (`AC-014`): Overlapping Win11/Win10 smoke-test criteria; `AC-001` is the sole AC citing a `TEST-###` ID.
   - `prd.md:140`: Blank line splits `AC-001..014` and `AC-015..018` into two unheaded Markdown lists; `prd.md:34–35` and `architecture.md:10–11` contain consecutive blank lines.
   - `prd.md:141` (`AC-015`) & `testing.md:55` (`TEST-023`): Omit `startup website` from `REQ-015` (`prd.md:66`).
   - `decisions.md:19` (`ADR-010` index): Lists only `REQ-009`, omitting `REQ-008` even though `ADR-010` body (`line 111`) defines the `REQ-008` import scope.
   - `rules.md:58`: Cites interfaces `IBrowserRuntime`, `IBrowserProfileContext`, `IBrowserWindowHost` which do not appear in `architecture.md` or `schema.md`.
   - `rules.md:38`: Mentions an explicit confirmed "copy browser data" duplication action absent from `design.md:54, 73`.
   - `README.md:69–70` vs `architecture.md:66–108`: `README.md` includes `artifacts/` and `docs/` in target repository structure while `architecture.md` omits them.

---

## End-to-End Traceability Matrix Summary

| REQ | Priority | AC | TEST | ENT | UI | ADR | Milestone(s) | Status / Gaps |
|---|---|---|---|---|---|---|---|---|
| REQ-001 | Must | AC-001 | TEST-001 | — | UI-001, UI-003 | ADR-001, 002, 003 | M-002 | **B-06:** `TEST-001` orphaned from `plan.md` |
| REQ-002 | Must | AC-002 | TEST-002 | ENT-001 | UI-002, UI-004 | ADR-004 | M-003 | **M-02:** `AC-002`/`TEST-002` miss duplicate-config isolation; `UI-004` misses Edit |
| REQ-003 | Must | AC-003, AC-004 | TEST-003, 004, 017 | ENT-001 | *(none)* | ADR-003, 011 | M-001, M-003 | **M-04/M-07:** No `UI-###` tag; `ADR-011` not propagated to `architecture.md`/`schema.md` |
| REQ-004 | Must | AC-005 | TEST-005 | ENT-002, ENT-003 | UI-001 | — | M-003 | **M-04:** `ENT-002` missing `IsHidden` field |
| REQ-005 | Must | AC-006 | TEST-006, 016 | ENT-004 | UI-003 | ADR-001, 003 | M-001, M-003, M-004, M-008 | OK (note `M-008` dep bug M-05) |
| REQ-006 | Must | AC-007 | TEST-007, 021 | ENT-004..007 | UI-005..007 | ADR-004 | M-004, M-008 | **B-03:** `TEST-021` used in M-001 tasks but missing from M-001 index row |
| REQ-007 | Should | AC-008 | TEST-008, 019 | ENT-008 | UI-008 | ADR-003 *(body only)* | M-005 | **B-06/M-08:** `TEST-008`/`019` orphaned from `plan.md`; `ADR-003` index omits `REQ-007` |
| REQ-008 | Must | AC-009, AC-010 | TEST-009, 010, 018 | ENT-009, ENT-012 | UI-009 | *(ADR-010 body)* | M-006 | **M-04:** No Vault UI for `ENT-009`; `ADR-010` index omits `REQ-008` |
| REQ-009 | Must | AC-009 | TEST-009 | — | UI-009 | ADR-010 | M-006 | OK |
| REQ-010 | Must | **MISSING** *(overloads AC-010)* | TEST-010, 011, 022 | ENT-009, ENT-010 | UI-010 | ADR-005 | M-006, M-007 | **B-04:** No AC for DPAPI/master password/Hello unlock or log redaction |
| REQ-011 | Must | AC-011 | TEST-012, 013, 020 | ENT-011 | UI-011 | ADR-006 | M-007 | **B-07:** Scheduled backup passphrase contradiction; `ENT-011` omitted from ER diagram |
| REQ-012 | Must | AC-012 | TEST-014 | — | UI-013 | ADR-009 | M-002, M-009 | **B-06:** `TEST-014` orphaned from `plan.md` |
| REQ-013 | Must | AC-013 | TEST-015 | — | UI-013 | ADR-009 | M-009 | **B-06:** `TEST-015` orphaned from `plan.md` |
| REQ-014 | Must | AC-001, AC-014 | TEST-001 | — | — | ADR-002, 008 | M-001, M-008, M-009 | **B-03/B-06:** `TEST-001` orphaned from `plan.md`; Win10 omitted from M-001 completion evidence |
| REQ-015 | Should | AC-015 | TEST-023 | **MISSING `IsFavorite`** | UI-002 | — | M-003 | **B-05:** `ENT-001` missing `IsFavorite` & `REQ-015` tag; `AC-015`/`TEST-023` omit startup URL |
| REQ-016 | Should | AC-016 *(partial)* | TEST-024 *(partial)* | — | **MISSING** | — | M-003 | **M-03:** Drops tabs/bookmarks; no `UI-###` tag; M-003 precedes M-004 tabs/bookmarks |
| REQ-017 | Should | AC-017 | TEST-025 | ENT-010 | UI-012 *(mismatch)* | — | M-007 | **M-04:** Tagged on `UI-012` (Settings) instead of `UI-011` (Backup & Restore) |
| REQ-018 | Should | AC-018 | TEST-026 | *(ENT-010 prose)* | UI-012 *(mismatch)* | — | M-008 | **M-04:** Missing from `UI-001` workspace; `ENT-010` omits `REQ-018` tag |
