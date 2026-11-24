# Consolidated Specification-Readiness Review (Round 2) — Switchly Next

**Date:** 2026-11-24  
**Scope:** Specification-readiness audit of `GEMINI.md`, `AGENTS.md`, and all nine canonical documents (`README.md`, `prd.md`, `plan.md`, `architecture.md`, `schema.md`, `design.md`, `rules.md`, `testing.md`, `decisions.md`) against current workspace state.  
**Code / Canonical Doc Edits Made:** None (review artifacts written under `docs/egm-artifacts/2026-11-24-spec-readiness-r2/` only; existing morning artifacts under `docs/egm-artifacts/` preserved untouched).

---

## 1. EGM Plugin, Skill Discovery, and Fresh-Context Subagent Verification

| Check | Result | Exact Evidence |
|---|---|---|
| Workspace-local EGM plugin exists | **PASS** | `.agents/plugins/egm/` exists with `plugin.json` (`egm` v`0.1.1`), `skills/` (all 8 skills), `agents/` (10 personas), `resources/`, `docs/`, `assets/`. |
| Antigravity loaded EGM plugin | **PASS (partial surface)** | All 10 EGM named subagent types (`egm-zoom-out`, `egm-comprehension`, `egm-design`, `egm-structural`, `egm-implementation-readiness`, `egm-codebase-details`, `egm-inventive-catalyst`, `egm-style`, `egm-synthesizer`, `egm-bootstrap-orchestrator`) are registered and discoverable in this Antigravity session. |
| EGM skills visible in model `<skills>` list | **NOT EXPOSED (by skill manifest)** | `egm-review` and sibling EGM skills are not listed in the model's auto-invocable `<skills>` list because `.agents/plugins/egm/skills/egm-review/SKILL.md` sets `disable-model-invocation: true` (intended for user slash-command invocation). `agy plugin list` returned `CommandNotFoundException` (`agy` CLI not on `PATH`). |
| `invoke_subagent` available | **PASS** | `invoke_subagent` is exposed and executed **5 independent fresh-context EGM Goldfish subagents** to completion (0 quota failures). |

### Fresh-Context Goldfish Subagents Executed (5 / 5 PASS)

| Reviewer Persona | Subagent Type | Conversation ID | Standard Applied | Report Artifact |
|---|---|---|---|---|
| Skeptical Architect | `egm-zoom-out` | `d4de298a-2490-4b19-af7d-ed6acc80217c` | `zoom-out-standard.md` | `zoom_out_report.md` |
| Naive Reader | `egm-comprehension` | `cbccea69-0a80-4e8a-96bd-9fc93a0fc54a` | *(none — self-containment test)* | `comprehension_report.md` |
| Hostile Design Critic | `egm-design` | `0580783e-939b-45ae-93b8-73d152a1b2e9` | `feature-spec-standard.md` | `design_critic_report.md` |
| Structural & Traceability Auditor | `egm-structural` | `a2c6957f-31fe-4d9a-8baf-1ad48b2e1c33` | `feature-spec-standard.md` | `structural_report.md` |
| Pragmatic Tech Lead | `egm-implementation-readiness` | `2093fdd6-40d3-4e85-9082-57dfa09eecdc` | `implementation-readiness-standard.md` | `implementation_readiness_report.md` |

> **Honesty note:** Because the user did not invoke `/egm-review` as a slash command and `egm-review` sets `disable-model-invocation: true`, this run followed the `egm-review` protocol manually after reading `.agents/plugins/egm/skills/egm-review/SKILL.md`, using **real fresh-context Goldfish delegation** via `invoke_subagent` across 5 registered EGM reviewer personas (uninterrupted cross-document review).

---

## 2. Executive Verdict — Is M-001 Ready to Begin?

**Critical Status Discovery:** **M-001 has ALREADY been executed** in earlier commits on `main`, and **M-002 has ALREADY been partially implemented** (`Switchly.sln`, `src/Switchly.*`, `tests/Switchly.Infrastructure.Tests/`, `.github/` exist on disk; `git status` also has uncommitted changes to `Switchly.sln`, `.github/`, and `tests/`).

However, **neither M-001 closure nor M-002 continuation is canonically clean**:
1. **The canonical docs disagree across three timelines:**
   - `README.md` (L3, L43, L53, L125, L127), `prd.md` (L3), `architecture.md` (L3, L236–248), `testing.md` (L3), and `plan.md` (L152) still claim **"Specification only — no code or tests executed — Start with M-001"**.
   - `plan.md` (L3, L150–151) and `decisions.md` (`ADR-003` L12, L47; `ADR-011` L20, L116) claim **M-001 is Complete** and **M-002 is In Progress** with manual sign-in PASS for all 4 providers (`TASK-007..010 remaining`).
   - **Actual workspace reality** (`git log` + filesystem): `TASK-001..006` artifacts exist in `artifacts/engine-spike/`, `TASK-007`, `TASK-008`, and `TASK-009` are already committed (`0047a0c`, `713ac92`, `5111268`, `9e5680b`), and `TASK-010` (`.github/`, `tests/`, `Switchly.sln`) is uncommitted in the working tree.
2. **M-001 still has unverified/unreconciled exit obligations:**
   - `artifacts/engine-spike/task-006-go-nogo.md` still shows **Tier-2 sign-in = `NOT TESTED`**, **Packaging = `NOT TESTED`**, and **"Decision needed: Product owner confirms GO"** (the subsequent Tier-2 manual sign-in PASS was appended only to `artifacts/engine-spike/task-003-results.md` L57–61 without updating `task-006-go-nogo.md` or linking the evidence path in `plan.md`/`testing.md`).
   - `M-001` used `CefSharp.OffScreen.NETCore` in a console harness (`OPEN-008`, `plan.md` L41) running **elevated** with `do-not-de-elevate`, and **never tested Windows 10** (`REQ-014`, `testing.md` L73, `architecture.md` L246) or full **WPF hosting** (`architecture.md` L241) under non-elevated `asInvoker`.
3. **M-002 partial scaffold already exhibits architectural & package drift** (verified directly in `.csproj` files):
   - `src/Switchly.App/Switchly.App.csproj` (L17) directly references `CefSharp.Wpf.NETCore 156.0.300` and does NOT reference `Switchly.BrowserHost`, while `src/Switchly.BrowserHost/Switchly.BrowserHost.csproj` targets `net10.0` with no CefSharp reference — violating `architecture.md` L116 and `rules.md` L26.
   - `src/Switchly.App/Switchly.App.csproj` (L18–20) references `Microsoft.Extensions.*` **`11.0.0`** on `net10.0-windows`, while `src/Switchly.Infrastructure/Switchly.Infrastructure.csproj` (L8, L12) uses EF Core **`10.0.11`**.
   - `tests/Switchly.Infrastructure.Tests/Switchly.Infrastructure.Tests.csproj` (L13) already selected **`xunit 2.9.3`**, while `README.md` L24 and `testing.md` L115 still state *"xUnit or NUnit (choose at scaffold)"*.

**Bottom line:** Do **not** re-run M-001 from scratch, and do **not** proceed to M-003 (or close M-002) until the canonical docs are reconciled with reality and the blockers below are resolved by the product owner.

---

## 3. Confirmed Product Decisions (Consistent Across Documents)

| Decision | Canonical Source | Current Status |
|---|---|---|
| Greenfield Windows desktop workspace; legacy `ferdilpu-sudo/Switchly` is read-only reference only | `AGENTS.md` §1, `README.md` §8 | Confirmed |
| WebView2 strictly excluded (no direct use, no fallback) | `AGENTS.md` §1, `decisions.md` ADR-003 | Confirmed (Product Owner) |
| Priority compatibility targets with exact URLs: Google Flow (`https://flow.google`), ChatGPT (`https://chatgpt.com`), Dola (`https://dola.com`), SeaArt (`https://seaart.ai`); no policy/auth bypass | `prd.md` §3, `architecture.md` §1, `decisions.md` §13 | Confirmed (2026-11-24) |
| Per-profile isolated persistent CEF storage contexts; `CachePath` must be direct child of `RootCachePath` (`%LOCALAPPDATA%\SwitchlyNext\Profiles\<profile-guid>`) | `prd.md` REQ-003, `decisions.md` ADR-011, `architecture.md` §5.1, `schema.md` §1 | Validated (M-001 TASK-002) |
| CEF Chrome Runtime via `CefSharp.Wpf.NETCore 156.0.300` (CEF 156.0.30 + Chromium 156.0.8078.96); non-elevated (`asInvoker`) in production | `decisions.md` ADR-003, `architecture.md` §2, `rules.md` §5 | Validated (Win11; Win10 & non-elevated WPF stress checks pending) |
| Basic local use without registration; Pro account/login only for Pro upgrade/activation | `prd.md` REQ-012/013, `decisions.md` ADR-009 | Confirmed |
| No source browser cookies or login session import; password import only via user-exported CSV | `prd.md` REQ-008/009, `decisions.md` ADR-010 | Confirmed |
| Extensions are optional and deferred (`REQ-007`); not an MVP acceptance gate | `AGENTS.md` §1, `prd.md` §4.2, `decisions.md` OPEN-003 | Confirmed |
| Windows 11 x64 primary target; Windows 10 Home/Pro best-effort pending release policy | `prd.md` REQ-014, `decisions.md` ADR-002/OPEN-002 | Confirmed primary; Win10 open |
| M-001 harness = .NET 10 console app (`CefSharp.OffScreen.NETCore`) at `artifacts/engine-spike/Switchly.EngineSpike/` | `plan.md` TASK-002, `decisions.md` OPEN-008 | Confirmed (2026-11-24; executed) |

---

## 4. Deduplicated Findings & Traceability Matrix

### Blockers (Must Resolve Before Continuing M-002 Closure / M-003+)

| ID | Finding | Reviewers | Canonical Scope |
|---|---|---|---|
| **C-B1** | **Three-way project phase & evidence contradiction** (`README.md`/`testing.md`/`architecture.md`/`prd.md` say pre-code; `plan.md` L150–151 says M-001 complete & M-002 `TASK-007..010` remaining; `plan.md` L152 says `M-002 to M-009 Not started`; git already has `TASK-007..009` + uncommitted `TASK-010`; `task-006-go-nogo.md` still says sign-in `NOT TESTED` and GO pending; no evidence path linked in `plan.md`/`testing.md`). | All 5 | `README.md:3,43,53,125,127`, `plan.md:3,22,148-152`, `decisions.md:12,47`, `testing.md:3-4,73`, `architecture.md:3,35,238-248`, `task-006-go-nogo.md` |
| **C-B2** | **Hard canonical conflict on SQLite password storage & write-only vault UX:** `AGENTS.md:114` forbids storing *any* website passwords in the application SQLite DB, directly contradicting `schema.md` `ENT-009 VaultCredential` (encrypted passwords in `switchly-next.db`). Additionally, `schema.md:240` defers autofill and `design.md` has **no screen** to view/copy/edit/delete stored credentials. | Zoom-Out, Comprehension, Design, Structural, Impl-Readiness | `AGENTS.md:114`, `schema.md:8-9,27,126-136,240`, `prd.md:55,154`, `architecture.md:181`, `design.md:49-63` |
| **C-B3** | **Catastrophic AEAD nonce-reuse hazard & missing key hierarchy in `ENT-009 VaultCredential`:** Two ciphertexts (`UsernameCiphertext`, `PasswordCiphertext`) share a single `Nonce` and `AuthenticationTag`, with no AAD binding `(CredentialId, ProfileId, Origin, CryptoVersion)`. Key hierarchy (app-wide vs per-profile DEK), KDF algorithm/params, DPAPI vs master password vs Windows Hello cryptographic relationship, and key recovery policy (`schema.md:234`) are all unspecified. | Design, Impl-Readiness, Comprehension | `schema.md:126-137,232-234`, `architecture.md:30-31,198-199`, `decisions.md:68-75` (`ADR-005`) |
| **C-B4** | **Logical impossibility for scheduled encrypted backups:** `REQ-011` / `FLOW-004` / `TASK-032` require unattended scheduled encrypted backups with passphrase-derived AES-256-GCM (`ADR-006`), while `schema.md:153` and `rules.md:47` strictly forbid storing the passphrase or derived key. Also conflicts with live-profile snapshot locking (`architecture.md:170`) vs never closing user tabs (`design.md:148`, `rules.md:113`). | Zoom-Out, Comprehension, Design, Structural | `prd.md:58,109-113,178`, `schema.md:144-153`, `rules.md:47,113`, `decisions.md:77-84`, `design.md:106-107,148`, `plan.md:114` |
| **C-B5** | **Cross-device backup/restore breaks Chromium `os_crypt` DPAPI cookie encryption + impossible live NTFS atomic swap + AES-GCM 2 GB buffer ceiling:** (a) Excluding `RootCachePath\Local State` (`ADR-011`) or moving across devices renders Chromium `Cookies` undecryptable; (b) NTFS cannot atomically swap `Data\` and `Profiles\` while CEF and SQLite hold open handles; (c) single-shot `AesGcm` OOMs/exceeds 2 GB `Span` limit on multi-GB Chromium profiles without a chunked AEAD wire format; (d) `ADR-011`'s exclusion of CEF global state & disposable caches is missing from `architecture.md` Flow C. | Design, Impl-Readiness, Comprehension, Structural | `prd.md:58,114-115,136`, `architecture.md:169-175,180,198`, `decisions.md:77-84,118-119`, `design.md:108-110`, `rules.md:85`, `testing.md:44-45` |
| **C-B6** | **Authoritative schema missing required columns & migration-blocking defaults:** `ENT-001 BrowserProfile` is missing `IsFavorite` (required by `REQ-015`/`AC-015`/`TEST-023`/`TASK-015A`), `ENT-002 ProfileGroup` is missing `IsHidden` (`schema.md:59`), `ENT-001.PersistSessionCookies` has no default (`schema.md:48`), whole-DB SQLite encryption is unresolved (`schema.md:241`), and FK `ON DELETE` rules are omitted for 7 of 8 relationships. | All 5 | `schema.md:19,34-59,168-179,241`, `prd.md:66,141`, `testing.md:55`, `plan.md:24,85` |
| **C-B7** | **Broken `REQ-010` traceability & 5 orphaned `TEST-###` cases in `plan.md`:** `prd.md` §6 has **no `AC-###`** for `REQ-010` local unlock (DPAPI / master password / Windows Hello) or log redaction — `AC-010` covers CSV password import only, forcing `TEST-011` and `TEST-022` to mis-map onto `AC-010`. Five tests (`TEST-001`, `TEST-014`, `TEST-015` [Must-have], plus `TEST-008`, `TEST-019`) are completely orphaned from `plan.md`. | Comprehension, Design, Structural, Impl-Readiness | `prd.md:57,124-145`, `testing.md:33,40,42-43,46-47,51,54`, `plan.md:20-30,70-133` |
| **C-B8** | **Governance authority gap (`Accepted` ADRs) & unspecified M-002 `BrowserHost`/WPF boundaries:** `AGENTS.md:43` grants Rank-3 authority to `Accepted ADRs`, yet **zero ADRs** in `decisions.md` carry status `Accepted` (`ADR-001`, `002`, `004`–`007` are still `Proposed` while M-002 code already exists). `rules.md:58` names `IBrowserRuntime`/`IBrowserProfileContext`/`IBrowserWindowHost` without defining signatures or the WPF visual-tree embedding contract — and `src/Switchly.App` already broke isolation by referencing `CefSharp.Wpf.NETCore` directly. | Structural, Impl-Readiness, Zoom-Out | `AGENTS.md:43`, `decisions.md:3,8-20`, `rules.md:17,26,58-60`, `architecture.md:98-102,116`, `src/Switchly.App/Switchly.App.csproj` |

### Majors (Resolve Before M-003 / M-004 / M-006 / M-007)

| ID | Finding | Reviewers | Canonical Scope |
|---|---|---|---|
| **C-M1** | **CEF Chrome Runtime `RootCachePath` ProcessSingleton conflicts with per-profile cross-process locking:** Two Switchly Next processes cannot share `RootCachePath` (`ADR-011`); requires an app-level single-instance guard (named mutex + IPC focus handoff) plus kernel-managed locks, Windows Job Object (prevent orphaned subprocesses), pre-init elevation guard (exit code 38), and deferred-delete tombstone queue for locked Chromium handles. | Zoom-Out, Comprehension, Design, Impl-Readiness | `architecture.md:122-140`, `decisions.md:114-120`, `rules.md:36-37,60,66`, `testing.md:49` |
| **C-M2** | **WPF hosting model (`CefSharp.Wpf` OSR vs `CefSharp.Wpf.HwndHost` airspace) & window topology undefined:** Specs never compare OSR vs HwndHost (airspace clipping of menus/modals vs IME/DPI/WebGL trade-offs), nor lock whether tabs live in Main Workspace vs separate per-profile windows (`UI-001` vs `UI-003`), nor specify CEF `IKeyboardHandler` shortcut interception (`Ctrl+L/T/W/Tab/Shift+T`). | Zoom-Out, Design, Impl-Readiness | `design.md:18-31,51-53,67-83,128`, `architecture.md:24-35,241`, `decisions.md:22-57` |
| **C-M3** | **Split-brain storage (`Bookmark`, `HistoryEntry`, `DownloadRecord`) vs Chromium internal DBs:** Clearing `HistoryEntry` in SQLite does not clear Chromium's native `History` / `Visited Links` inside `<profile-guid>`. Query-string stripping defaults, startup crash-reconciliation of `InProgress`/`Running` jobs, and `WebsiteDomain` auto-group derivation/eTLD+1/pruning rules are unspecified. | Zoom-Out, Comprehension, Design, Impl-Readiness | `architecture.md:158,179-180`, `schema.md:4,55-59,72,81-112,230`, `decisions.md:64-65` |
| **C-M4** | **Milestone dependency & task-plan defects in `plan.md`:** (a) **Inverted crypto dependency:** `M-006` (`TASK-028` encrypted vault writer) precedes `M-007` (`TASK-029` DPAPI key wrapping); (b) `M-008` depends on `M-004 to M-007`, pulling deferred `M-005` into MVP; (c) `M-007` omits `M-004` dependency; (d) `REQ-016` search/filter drops tabs & bookmarks in `AC-016`/`TEST-024` and is assigned to `M-003` before `M-004` creates tabs/bookmarks; (e) `ADR-008` schedules installer PoC in M-001/M-002 while `plan.md` puts it in `M-009` (`TASK-040`); (f) 39 of 40 tasks in `plan.md` §4 lack per-task `REQ/AC/TEST` mapping (`AGENTS.md:120`) and file boundaries. | All 5 | `plan.md:20-30,70-133`, `decisions.md:100`, `prd.md:67,142`, `testing.md:56`, `AGENTS.md:120` |
| **C-M5** | **Browser hardening & import adapter gaps:** Missing policies for external protocol handlers (`smb://`, `ms-msdt:`, `file://`), disabling `RemoteDebuggingPort` in release, forbidding unrestricted `JavascriptObjectRepository`, Windows download filename sanitization + Mark-of-the-Web (`Zone.Identifier`), Chromium binary `SNSS` open-tab format bounds, background Chrome/Edge lock guidance, `CryptographicOperations.ZeroMemory` for CSV parsing, and duplicate-detection keys for `UI-009` preview. | Design, Impl-Readiness | `architecture.md:184-214`, `rules.md:46,55-76`, `schema.md:105,188,190,238`, `design.md:84-98` |
| **C-M6** | **Module & YAGNI contradictions:** `architecture.md:92` has `Switchly.Infrastructure/BrowserEngine/` duplicating `Switchly.BrowserHost/` (`rules.md:26`). Deferred extensions (`ENT-008`, `UI-008`, `OptionalExtensions/`, `FLOW-005`, `prd.md:13,103`, `design.md:35`) and speculative Freemium (`REQ-013` in Must-MVP, `UI-013`, `TEST-015`) are wired into MVP docs without matching architecture/schema support. | Zoom-Out, Comprehension, Structural | `architecture.md:79,92,98-102`, `schema.md:26,113-124`, `prd.md:13,60,103`, `design.md:35,44,58,63` |
| **C-M7** | **Missing `REQ-002` duplicate-config test & `UI-###` / `schema.md` mismatches:** `AC-002` and `TEST-002` omit profile duplication (and verifying sessions are not copied); `rules.md:38` mentions an optional confirmed "copy browser data" action absent from `prd.md`/`design.md`; `UI-004` (`design.md:54` vs L73) omits `Edit`/`Favorite`; `UI-011`/`UI-012`/`UI-001` have mismatched `REQ-017`/`REQ-018` tags; `design.md:68` omits `Starting`/`Suspending` states; `Profile Details` has no `UI-###` ID. | Comprehension, Design, Structural, Impl-Readiness | `prd.md:50,127`, `testing.md:34`, `design.md:32-37,49-63,68,73`, `rules.md:38`, `architecture.md:134-139` |
| **C-M8** | **Fragmented open decisions, CEF version mismatch, and bilingual drift:** `README.md:116` / `architecture.md:28` say Chrome Runtime became sole mode in `CEF 156+` vs `CEF 128` in `prd.md:173` / `decisions.md:54`; `OPEN-008` is confirmed but still in "Open decisions"; `Provider identity clarification` is nested under "Open decisions"; 8+ open items across other docs lack `OPEN-###` IDs; 4 ID families (`FLOW`, `UI`, `ENT`, `NFR-PERF`) are unlinked across docs; 8 of 11 files mix Indonesian and English without a language policy (`design.md:133` declares Indonesian MVP UI while wireframe labels are English); `decisions.md` ADRs 007–011 omit Alternatives sections; `Switchly.App.csproj` pulls `Microsoft.Extensions.* 11.0.0` vs EF Core `10.0.11`. | All 5 | `README.md:24,43,116`, `prd.md:149,173`, `architecture.md:9-35`, `decisions.md:4,86-141`, `design.md:133`, `schema.md:233-242`, `testing.md:23-24,115` |

### Selected Nits
- `prd.md:45-70`: `REQ-007` gap in Must table without inline pointer; `AC-015`/`TEST-023` omit `startup website` from `REQ-015`.
- `schema.md:72`: "supported internal URLs" allowlist (`about:blank`, etc.) unenumerated; `CompletedAtUtc` nullability missing on `ENT-007`/`ENT-012`; `ENT-010`/`ENT-011` omitted from ER diagram; `CreateProfileCommand.GroupId` is `string?` instead of `Guid?`/collection; `StorageRelativePath` base root unspecified (must be single segment `<profile-guid>` under `RootCachePath`, plus `<longPathAware>true</longPathAware>`).
- `plan.md`: Non-uniform `TASK-015A`/`033A`/`038A` IDs and abbreviated `TEST-002 to 006` ranges; `M-008`/`M-009` omit `- Exit:` lines.
- `README.md:98-100`: References `EGM`, `Antigravity`, and "original Switchly repository" without inline expansion/URL; mixes `SwitchlyNext`, `Switchly.sln`, and `switchly-next.db`.
- `GEMINI.md:5` / `AGENTS.md:29-49`: Minor omissions in cross-listed canonical document lists.

---

## 5. Proposed Canonical Document Updates (Awaiting Product-Owner Approval — NOT APPLIED)

Per `AGENTS.md` §2 and user instructions, **no canonical documents were modified**. Proposed updates for Product Owner approval:

1. **Reconcile project state & M-001/M-002 evidence (`README.md`, `plan.md`, `decisions.md`, `testing.md`, `architecture.md`, `prd.md`, `artifacts/engine-spike/task-006-go-nogo.md`):**
   - Remove the contradictory `M-002 to M-009 | Not started` row (`plan.md:152`), update `plan.md` §6 to reflect actual git progress (`TASK-001..006` complete; Tier-2 manual sign-in PASS linked to `artifacts/engine-spike/task-003-results.md`; `TASK-007..009` committed; `TASK-010` in progress; Win10 & non-elevated WPF stress checks explicitly tracked as open M-002/M-008 gates).
   - Sync `README.md`, `testing.md`, `architecture.md`, and `prd.md` status banners, fix `CEF 128` vs `156+` wording, pin `xUnit` (`testing.md`/`README.md`), close `OPEN-008`, and move confirmed Provider URLs out of `## 13. Open decisions`.
2. **Product Owner decisions needed to resolve Blockers C-B2..C-B8:**
   - **D1 (`AGENTS.md:114` vs `ENT-009`):** Defer custom CSV password vault (`ENT-009` / master password / Hello) out of MVP, **OR** keep it in MVP by explicitly amending `AGENTS.md:114` (encrypted-only exception or separate vault store), fixing the AEAD nonce-reuse schema (`SecretPayloadCiphertext` + per-record nonce/tag + AAD), defining the key hierarchy/recovery policy, and adding a Vault Management UI screen to `design.md`.
   - **D2 (Scheduled Encrypted Backups — `REQ-011`):** Make MVP backup **manual-only** (deferring `TASK-032` / `REQ-017`), **OR** approve hybrid public-key envelope encryption in `ADR-006` + open-profile skip/defer policy.
   - **D3 (Cross-Device Restore & Chromium `os_crypt`):** Explicitly document in `REQ-011`/`AC-011`/`ADR-006`/`ADR-011` that browser login cookies are device-bound and excluded on cross-device restore, adopt chunked AEAD streaming + Chromium cache exclusions + restart-to-restore staging swap.
   - **D4 (Schema & Traceability patches before locking `TASK-009`):** Add `IsFavorite` (`ENT-001`), `IsHidden` (`ENT-002`), lock `PersistSessionCookies` default (`true` vs `false`) and plain SQLite vs SQLCipher, define FK `ON DELETE` rules, add a dedicated `AC` for `REQ-010` local unlock, map all 5 orphaned `TEST-###` IDs in `plan.md`, fix `M-006`/`M-007` and `M-008`/`M-005` dependencies, and promote validated/approved ADRs (`ADR-001`, `002`, `003`, `004`, `007`, `009`, `010`, `011`) to `Accepted`.
   - **D5 (M-002 Scaffold Cleanup):** Move `CefSharp.Wpf.NETCore` out of `Switchly.App` into `Switchly.BrowserHost` behind defined `IBrowser*` interfaces, evaluate/confirm `CefSharp.Wpf` (OSR) vs `CefSharp.Wpf.HwndHost`, align NuGet `Microsoft.Extensions.*` from `11.0.0` to `10.x` LTS via `Directory.Packages.props`, and remove `Switchly.Infrastructure/BrowserEngine/` from `architecture.md`.
