# EGM Comprehension Review — Switchly Next Canonical Specification Suite

**Reviewer Persona:** Naive Reader (`egm-comprehension`, fresh-context Goldfish subagent `cbccea69-0a80-4e8a-96bd-9fc93a0fc54a`)  
**Date:** 2026-11-24  
**Standard:** None (self-containment and clarity evaluation per `egm-review` Stage 6)

---

## Naive Paraphrase

### What this proposes
**Switchly Next** is a local-first Windows desktop application (C# / WPF / MVVM on .NET 10 LTS, targeting Windows 11 x64 with best-effort Windows 10 validation) for managing multiple isolated web browser profiles without logging out and back in. Each profile gets its own persistent Chromium browser storage context via CefSharp/CEF (Chrome Runtime), supporting multiple tabs and secondary windows, manual and automatic domain-based grouping, per-profile bookmarks/history/downloads/session restore, read-only import from Chrome and Edge (including user-exported password CSVs into an encrypted per-profile vault, but explicitly excluding cookies/login sessions), local security (Windows DPAPI plus optional master password / Windows Hello unlock), and portable AES-256-GCM passphrase-encrypted backup and restore. Chromium extension support and a freemium Pro licensing tier are explicitly deferred beyond the core MVP, and four web apps (Google Flow, ChatGPT, Dola, and SeaArt) are designated as priority compatibility test targets.

### How the existing system works, per this document
There is no existing production codebase; the canonical suite describes a greenfield build (separately referencing an unlinked "original Switchly repository" only as historical product reference, explicitly excluding WebView2). Architecturally, the planned system splits into a WPF presentation shell (`Switchly.App`), pure domain rules (`Switchly.Domain`), application use-cases (`Switchly.Application`), local persistence/crypto/import/backup adapters (`Switchly.Infrastructure`), and a narrow CEF isolation wrapper (`Switchly.BrowserHost`), storing app metadata in one local SQLite database and browser runtime storage in per-profile subdirectories directly under `%LOCALAPPDATA%\SwitchlyNext\Profiles\`. **However, the suite contradicts itself on the current project phase:** `README.md`, `prd.md`, `architecture.md`, and `testing.md` describe the project as pre-code specification-only with zero tests run and M-001 not yet started, whereas `plan.md` and `decisions.md` state that M-001 (the CEF engine spike) and an M-002 manual provider sign-in gate have already passed and that M-002 is now in progress (while `plan.md` simultaneously lists `M-002 to M-009` as "Not started" with "No code or test execution").

---

## Findings

### Blocker Findings

1. **Severity**: Blocker
   **Affected Scope**: Project phase & test execution status — `README.md` (L3, L19, L53, L125, L127), `prd.md` (L3), `architecture.md` (L3, L35, L236–248), `testing.md` (L3), `plan.md` (L3, L148–152), `decisions.md` (L12, L20, L47, L54, L116)
   **Discrepancy Description & Rationale**: The canonical documents directly contradict one another — and `plan.md` contradicts itself on consecutive lines — regarding whether M-001 has been executed and whether any code or tests have run:
   - `README.md` L3 ("Specification only — no application code has been implemented or tested"), L125 ("Start with M-001"), L127 ("No tests have run"), `testing.md` L3 ("all tests planned; none have been executed"), `prd.md` L3 ("architecture and compatibility proof still required"), and `architecture.md` L3/L238 ("gated by M-001 proof-of-concept") all state M-001 has not happened.
   - Conversely, `plan.md` L3 & L150–151 state `M-001` is `Complete`, `M-002` is `In progress` with manual sign-in PASS for all 4 providers; `decisions.md` L12, L20, L47, L116 mark `ADR-003` and `ADR-011` as `Validated`; and `architecture.md` L35 says `TASK-001` already pinned `CefSharp.Wpf.NETCore 156.0.300`.
   - Inside `plan.md` Section 6 (Progress tracker), L151 states `M-002 | In progress | Sign-in gate PASS (all 4 providers, manual); TASK-007..010 remaining`, and the very next row (L152) states `M-002 to M-009 | Not started | No code or test execution has been performed`.
   - Even inside `decisions.md` `ADR-003`, L47 says M-001 + M-002 sign-in gate passed, while L54 still reads in future tense ("Validate the chosen CefSharp/CEF version and four priority websites in M-001").
   A new reader cannot determine the true state of the project or what work to start next.

2. **Severity**: Blocker
   **Affected Scope**: Scheduled / automatic encrypted backup vs. zero passphrase storage — `prd.md` §4.1 `REQ-011` (L58), §5 `FLOW-004` (L109–113), `design.md` §4.5 (L106–107), `schema.md` §3 `ENT-011` (L153), `rules.md` §4 (L47)
   **Discrepancy Description & Rationale**: The design requires automatic scheduled portable encrypted backups (`REQ-011`, `FLOW-004`, `TASK-032`), specifies that backup archives are encrypted using a passphrase-derived key (`architecture.md` L31, L173; `decisions.md` `ADR-006` L82), and states that the user enters the passphrase (`FLOW-004` L112; `design.md` L107) while forbidding storing any backup passphrase or derived key (`schema.md` L153: "No backup passphrase or derived key stored"; `rules.md` L47). The documents never explain how an unattended scheduled backup encrypts a portable archive without a stored passphrase/key (e.g., whether scheduled runs prompt interactively, use a DPAPI-wrapped backup key stored outside SQLite, or use public-key encryption). As written, the automatic backup workflow cannot work.

---

### Major Findings

3. **Severity**: Major
   **Affected Scope**: M-001 engine spike harness (`OffScreen` console app vs. `Wpf` hosting & sign-in validation) — `architecture.md` §2 (L35), §12 (L241), `testing.md` §4 (L62, L73, L78), `plan.md` §3 `TASK-002` (L41), §6 (L150–151), `decisions.md` §4 `ADR-003` (L47), §13 `OPEN-008` (L133)
   **Discrepancy Description & Rationale**:
   - `architecture.md` L35 says `TASK-001` pinned `CefSharp.Wpf.NETCore 156.0.300`, and `architecture.md` L241 / `testing.md` L78 require M-001 to validate **WPF window hosting**, **acceptable integrated UI behavior**, and **Windows 10 editions** (`testing.md` L73).
   - Yet `plan.md` L41 (`TASK-002`) and `decisions.md` L133 (`OPEN-008`) specify a **console application using `CefSharp.OffScreen.NETCore`**, while `plan.md` L150–151 and `decisions.md` L47 declare M-001 complete and a manual provider sign-in gate passed even though `TASK-007` (WPF shell) is still "remaining".
   - It is unexplained how WPF hosting, interactive manual sign-in across 4 providers, `TEST-006`, and Windows 10 checks were verified if M-001 only ran a headless `OffScreen` console harness and no WPF app exists yet. Additionally, `OPEN-008` is still listed under "Open decisions with impact" (`decisions.md` L133) despite being marked `Confirmed 2026-11-24`.

4. **Severity**: Major
   **Affected Scope**: Missing evidence citations for claimed PASS results — `plan.md` §6 (L150–151), `decisions.md` §4 (L47), §12 (L116), `testing.md` Preamble (L4), §9 (L155)
   **Discrepancy Description & Rationale**: `testing.md` L4 enforces "no PASS claim without actual run output/evidence" and L155 requires recording the evidence path. However, `plan.md` L150–151 and `decisions.md` L47/L116 declare PASS for `TASK-001..006`, `TEST-003/004/017/021`, resource baseline, and all 4 provider sign-in checks without citing or linking a single evidence artifact path (not even `artifacts/engine-spike/engine-version-report.md` mentioned in `plan.md` L37). A fresh reader cannot locate or inspect the evidence behind these validations.

5. **Severity**: Major
   **Affected Scope**: CEF version when Chrome Runtime became exclusive — `README.md` §10 (L116), `architecture.md` §2 (L28), `prd.md` §8 (L173), `decisions.md` §4 `ADR-003` (L54)
   **Discrepancy Description & Rationale**: `README.md` L116 states "Chrome Runtime is the only CEF mode from CEF 156+" (and `architecture.md` L28 says "in CEF 156+ (Alloy removed)"), whereas `prd.md` L173 ("satu-satunya mode sejak CEF 128") and `decisions.md` L54 ("the Alloy bootstrap was removed in CEF 128") state it has been the only mode since CEF 128. The documents disagree on which CEF version dropped Alloy.

6. **Severity**: Major
   **Affected Scope**: Portable backup of DPAPI-encrypted `VaultCredential` records & exclusion of CEF global state — `architecture.md` §6 `Flow C` (L169–175), `schema.md` §3 `ENT-009` (L136), `decisions.md` §6 `ADR-005` (L73–74), §12 `ADR-011` (L119), `prd.md` §5 `FLOW-004` (L111)
   **Discrepancy Description & Rationale**:
   - `schema.md` L136 states that `VaultCredential` records in SQLite are encrypted with a DPAPI-wrapped local key and that "Backup re-encrypts vault records under portable archive encryption." However, `architecture.md` `Flow C` (L169–175) simply checkpoints SQLite and encrypts the payload with the archive passphrase — never describing how DPAPI-protected vault records are decrypted during backup and re-wrapped under the target machine's DPAPI key during cross-device restore (`AC-011`, `TEST-012`).
   - Similarly, `ADR-011` (`decisions.md` L119) introduces a critical rule that backups must copy only `<profile-guid>` folders and **never** CEF's global state / `"Default"` folder inside `Profiles`, but `architecture.md` `Flow C` (L171) and `prd.md` `FLOW-004` (L111) do not state this constraint.

7. **Severity**: Major
   **Affected Scope**: Restore semantics (full atomic swap vs. partial-profile backup) — `prd.md` §5 `FLOW-004` (L114–115), `architecture.md` §6 `Flow C` (L170), `design.md` §4.5 (L110), `schema.md` §3 `ENT-011` (L150), `rules.md` §7 (L85)
   **Discrepancy Description & Rationale**: `architecture.md` L170 ("blocks writes to selected profile"), `schema.md` L150 (`IncludedProfilesJson`), and `schema.md` L229 (optional backup before deleting one profile) imply backups can contain a subset of profiles. Yet `design.md` L110 specifies "staged restore dan atomic swap" without explaining whether restoring a backup **replaces** the entire application database and all existing profiles or **merges** selected profiles. Performing a full-DB atomic swap from a partial backup would destroy existing unbacked-up profiles.

8. **Severity**: Major
   **Affected Scope**: Missing `IsFavorite` and `IsHidden` persistence fields in authoritative data contract — `schema.md` §3 `ENT-001` (L34–50) & `ENT-002` (L52–59) vs. `prd.md` §4.2 `REQ-015` (L66), §6 `AC-015` (L141), `design.md` §2 (L20), `testing.md` §3 `TEST-023` (L55), `plan.md` §4 `TASK-015A` (L85)
   **Discrepancy Description & Rationale**:
   - Favorites are explicitly required (`REQ-015`, `AC-015`, `TEST-023`, `TASK-015A`, `design.md` Workspace -> Favorites), yet `ENT-001 BrowserProfile` (`schema.md` L34–50) has **no `IsFavorite` column** and no favorites entity exists anywhere in `schema.md`.
   - `schema.md` L59 states that users may hide system-derived `WebsiteDomain` groups, yet `ENT-002 ProfileGroup` (`schema.md` L52–59) has **no `IsHidden` column** (nor is hiding stored in `ENT-010`).

9. **Severity**: Major
   **Affected Scope**: Missing Acceptance Criterion for `REQ-010` local unlock & broken test traceability — `prd.md` §4.1 `REQ-010` (L57), §6 `AC-010` (L135), `testing.md` §3 `TEST-010`, `TEST-011`, `TEST-022` (L42–43, L54)
   **Discrepancy Description & Rationale**: `prd.md` §6 has **no Acceptance Criterion covering `REQ-010`** (local protection via Windows DPAPI, optional master password, and Windows Hello). `AC-010` only specifies CSV password import rules (`REQ-008`). As a result, `testing.md` maps `TEST-011` (app lock/unlock via Windows account, master password, and Windows Hello) and `TEST-022` (log/crash-report redaction) to `AC-010`, even though `AC-010` does not mention app unlock or Windows Hello.

10. **Severity**: Major
    **Affected Scope**: Unexplained local unlock security model, scope of "locking the app", and missing key recovery policy — `prd.md` §4.1 `REQ-010` (L57), `README.md` §2 (L22), `architecture.md` §8 (L190–191, L198–199), `decisions.md` §6 `ADR-005` (L73–74), `schema.md` §8 (L234), `design.md` §3 `UI-010` (L60), `testing.md` §3 `TEST-011` (L43)
    **Discrepancy Description & Rationale**:
    - It is unclear what "locking the app" protects: `architecture.md` L190 says local metadata and CEF profile directories are protected only by Windows user ACLs, while only `VaultCredential` is encrypted at rest (L191). Does master password / Windows Hello gate opening browser profiles, or only decrypting `VaultCredential`? If a master password is set, does it cryptographically wrap the vault key or act solely as a UI gate (and how does Windows Hello unlock it)?
    - `schema.md` L234 mandates that "Windows account or master password changes must be handled by a documented key recovery policy", but no such policy exists anywhere in the canonical documents.

11. **Severity**: Major
    **Affected Scope**: Imported password vault has no UX to view, manage, or use stored credentials — `prd.md` §4.1 `REQ-008` (L55), §6 `AC-010` (L135), `schema.md` §3 `ENT-009` (L126–136), §9 (L240), `design.md` §2 (L17–45), §3 (L49–63), §4.3 (L97)
    **Discrepancy Description & Rationale**: Passwords imported via CSV are stored in encrypted per-profile `VaultCredential` rows, and `schema.md` L240 explicitly defers deciding whether autofill will be supported. Yet `design.md` defines **no screen, panel, or flow** in the Information Architecture or Screen Catalog (`UI-001`..`UI-013`) to view, copy, edit, or delete stored vault credentials. As specified, imported passwords enter a write-only vault with no user-facing way to use them.

12. **Severity**: Major
    **Affected Scope**: Unexplained `Closed` vs. `Suspended` profile lifecycle states — `architecture.md` §5.2 (L133–139), §5.3 (L145), `design.md` §4.1 (L68–72)
    **Discrepancy Description & Rationale**: `architecture.md` defines both `Closed` ("no live CEF browser for the profile") and `Suspended` ("no renderer associated with the profile, profile data stays on disk"), and L145 says closing the final browser window "suspends/closes that profile". The documents never explain the technical or user-visible difference between `Closed` and `Suspended` (e.g., whether `CefRequestContext` remains instantiated, how tabs/restore state differ, or why a user would see both states in `design.md` L68).

13. **Severity**: Major
    **Affected Scope**: Multi-process profile locking vs. single shared CEF `RootCachePath` — `architecture.md` §5.1 (L122–123), §5.2 (L140), `decisions.md` §12 `ADR-011` (L118–119), `testing.md` §3 `TEST-017` (L49), `rules.md` §3 (L37)
    **Discrepancy Description & Rationale**: `architecture.md` L140 and `TEST-017` describe per-profile cross-process locks when opening the same profile from "two app processes". However, `ADR-011` sets a single shared `CefSettings.RootCachePath = %LOCALAPPDATA%\SwitchlyNext\Profiles` for all profiles. Chromium/CEF Chrome Runtime enforces a process-level singleton lock on the entire user-data directory (`RootCachePath`). The specification never clarifies whether Switchly Next is single-instance at the app level or allows multiple concurrent app processes sharing `RootCachePath`.

14. **Severity**: Major
    **Affected Scope**: Ambiguous `WebsiteDomain` auto-grouping rules — `prd.md` §4.1 `REQ-004` (L52), `schema.md` §3 `ENT-002` (L55–59), `ENT-003` (L64), `testing.md` §3 `TEST-005` (L37)
    **Discrepancy Description & Rationale**: The docs refer to automatic grouping by "recognized website/domain" (`website/domain yang dikenali`) and `TEST-005` says "visit test domains; refresh auto groups", but nowhere is the classification algorithm explained: does auto-grouping scan `HistoryEntry`, `StartupUrl`, `Bookmark`, or open tabs? Is there a curated list of recognized websites, or does every visited domain (including OAuth/CDN/redirect hosts) create a group? How are subdomains normalized into `DomainKey`?

15. **Severity**: Major
    **Affected Scope**: Unexplained boundary between SQLite `Bookmark`/`HistoryEntry`/`DownloadRecord` and CEF Chrome Runtime's internal storage — `architecture.md` §7 (L179–180), `schema.md` Preamble (L4), §3 `ENT-005..007` (L81–111), §8 (L230)
    **Discrepancy Description & Rationale**: `architecture.md` and `schema.md` state that SQLite owns bookmarks, an "application history projection", and download records while CEF owns internal browser databases. Because CEF Chrome Runtime also persists Chromium's own internal `History`, `Bookmarks`, and downloads inside each profile's `CachePath`, the specification leaves unclear how the two stay synchronized, how history query-string stripping (`schema.md` L94) or clearing history (`schema.md` L230: "call matching browser-data cleanup where required") keeps CEF's native SQLite history consistent with the app's projection, and which store is authoritative on conflict.

16. **Severity**: Major
    **Affected Scope**: Unexplained `Switchly.Infrastructure/BrowserEngine/` directory conflicting with `Switchly.BrowserHost/` — `architecture.md` §4 (L90–102, L115–116), `rules.md` §2 (L19–20, L26)
    **Discrepancy Description & Rationale**: `architecture.md` L92 places a `BrowserEngine/` folder inside `Switchly.Infrastructure/`, while L98–102 defines a separate top-level project `Switchly.BrowserHost/`. Both `architecture.md` L115–116 and `rules.md` L26 assign all CefSharp/CEF APIs and lifecycle exclusively to `Switchly.BrowserHost`, never explaining what belongs in `Switchly.Infrastructure/BrowserEngine/`.

17. **Severity**: Major
    **Affected Scope**: Ambiguous base paths for `StorageRelativePath` and `InstallPathRelative` — `schema.md` §1 (L10), §3 `ENT-001` (L42), `ENT-008` (L120), `decisions.md` §12 `ADR-011` (L117–118), `rules.md` §5 (L65)
    **Discrepancy Description & Rationale**: `ADR-011` warns that if `CachePath` is not an exact **direct child** of `RootCachePath` (`%LOCALAPPDATA%\SwitchlyNext\Profiles`), CEF silently falls back to an in-memory profile. Yet `schema.md` L42 (`StorageRelativePath`) does not state whether it is relative to `%LOCALAPPDATA%\SwitchlyNext\` (e.g., `Profiles\<guid>`) or to `RootCachePath` (e.g., `<guid>`), and `ENT-008` L120 (`InstallPathRelative`) ambiguously says "app/profile-relative path only" without specifying which root applies.

18. **Severity**: Major
    **Affected Scope**: Contradiction on profile duplication scope (settings-only vs. optional browser data copy) — `prd.md` §4.1 `REQ-002` (L50), `design.md` §3 `UI-004` (L54), §4.1 (L73), `rules.md` §3 (L38)
    **Discrepancy Description & Rationale**: `prd.md` L50 says duplication must not silently copy secret sessions, and `design.md` L54/L73 only defines `duplicate config` / `duplicate settings`. `rules.md` L38 adds that "copying browser data requires a separate explicit, confirmed action." Neither `prd.md` nor `design.md` specifies whether copying browser data is an actual feature or how it would safely handle locks, active sessions, or encrypted `VaultCredential` records.

19. **Severity**: Major
    **Affected Scope**: Inconsistent milestone dependencies, test exit criteria, and installer PoC scheduling — `plan.md` §2 (L20–30), §4 (L72–132), `decisions.md` §1 (L10–20), §9 `ADR-008` (L100)
    **Discrepancy Description & Rationale**:
    - `plan.md` L22 (`M-001`) and L24 (`M-003`) require `TEST-006` ("add multiple windows/tabs; close one") to pass before tabs (`TASK-016`) are even implemented in `M-004`.
    - `plan.md` L29 lists `M-008`'s dependencies as `M-004 to M-007`, which pulls deferred optional milestone `M-005` (extensions) into the MVP dependency chain.
    - `plan.md` L28 has `M-007` (backup/restore) depend on `M-003, M-006`, omitting `M-004` (bookmarks, history, downloads, tab sessions that backup must capture).
    - `TEST-001`, `TEST-014`, and `TEST-015` are not assigned to any milestone's Exit Criteria in `plan.md` §2.
    - `decisions.md` L100 (`ADR-008`) states "Build install/uninstall/upgrade PoC in M-001 or M-002", but `plan.md` has no installer task until `M-009` (`TASK-040`).
    - `decisions.md` L10–13 keeps `ADR-001` (WPF), `ADR-002` (.NET 10), and `ADR-004` (SQLite) at `Proposed` even though `plan.md` L3 claims `M-002` (building the WPF + .NET 10 + SQLite foundation) is already `In progress`.

20. **Severity**: Major
    **Affected Scope**: Unqualified references to deferred extensions in core MVP sections — `prd.md` §1 (L13), §5 `FLOW-003` (L103), `design.md` §2 (L35), `plan.md` §4 `TASK-027` (L106)
    **Discrepancy Description & Rationale**: Although extensions are deferred (`REQ-007`), `prd.md` L13 lists `ekstensi` inline as part of the core product isolation goal without qualification; `prd.md` `FLOW-003` L103 lists `ekstensi` as a selectable import category without noting that MVP only shows an optional read-only list (`REQ-008` L55, `design.md` L91); and `design.md` L35 places `Extensions` in the main `Profile Details` IA tree without a `(deferred/optional)` tag (unlike `License / Upgrade` on L44).

21. **Severity**: Major
    **Affected Scope**: Unexplained per-profile resource monitoring under CEF shared processes — `prd.md` §4.2 `REQ-018` (L69), §6 `AC-018` (L144), `design.md` §8 (L145–147), `architecture.md` §5.3 (L147–148)
    **Discrepancy Description & Rationale**: `REQ-018` / `AC-018` require displaying resource indicators per active profile/window and warning when resource pressure crosses a tested threshold, but the docs never explain what metric defines pressure (system RAM vs. CEF process tree working set) or how shared Chromium processes (browser, GPU, network) are attributed to individual profiles.

22. **Severity**: Major
    **Affected Scope**: Missing architectural/schema representation for Free/Pro entitlement gate (`REQ-013`) — `prd.md` §4.1 `REQ-013` (L60), `design.md` §3 `UI-013` (L63), `testing.md` §3 `TEST-015` (L47), `architecture.md` §4 (L66–108), `schema.md` §2 (L17–30)
    **Discrepancy Description & Rationale**: `REQ-013` is listed under **Must — MVP inti** and requires an extensible Free/Pro entitlement boundary (`UI-013`, `TEST-015`), yet neither `architecture.md` (module structure) nor `schema.md` (entity catalog) includes any entitlement interface, module, or local state contract. It is unclear whether a stub entitlement boundary is expected in MVP or deferred entirely to `M-009` (`TASK-039`).

23. **Severity**: Major
    **Affected Scope**: Undefined workspace-specific references (`EGM`, `Antigravity`, "original Switchly repository") — `README.md` §8 (L98–100)
    **Discrepancy Description & Rationale**: `README.md` L98–100 refers to **Antigravity**, **EGM** (never expanded or defined inline anywhere in the 9 canonical documents; only linked to `AGENTS.md` and `docs/EGM-*.md`), and **"The original Switchly repository"** (neither linked nor briefly described in any of the 9 canonical documents). A fresh reader reading the canonical suite does not know what EGM stands for, what Antigravity is, or where the reference Switchly repository lives.

---

### Nit Findings

24. **Severity**: Nit
    **Affected Scope**: Inconsistent UI language vs. wireframe labels & mixed-language status vocabulary — `design.md` §2 (L17–45), §6 (L133), `prd.md` §5 `FLOW-001` (L87), §7 (L149), §8 (L170–181), `README.md` §4 (L43), `rules.md` §11 (L126), `testing.md` §7 (L119), `schema.md` §8–9 (L233, L236), `decisions.md` §1 (L8–20)
    **Discrepancy Description & Rationale**: `design.md` L133 states MVP UI language is Bahasa Indonesia, yet UI labels in `design.md` and `prd.md` are written in English (`New Profile`, `Open URL in profile...`, `Favorites`, `Backup & Restore`). Several English documents embed Indonesian status tokens (`Direncanakan — verifikasi setelah scaffolding`, `Terbuka`, `Asumsi`) and `prd.md` L149 switches languages mid-sentence, while `decisions.md` uses a separate English status taxonomy (`Proposed`, `Validated`, `Open`, `Confirmed scope`, `Confirmed by user`) with no shared legend defining each status term.

25. **Severity**: Nit
    **Affected Scope**: Undefined CEF-specific historical terms ("Alloy" / "Alloy bootstrap" / "Chrome Runtime") — `architecture.md` §2 (L28), `decisions.md` §4 `ADR-003` (L54)
    **Discrepancy Description & Rationale**: `Alloy` / `Alloy bootstrap` and `Chrome Runtime` are referenced without a brief inline definition or link to CEF's runtime documentation, assuming the reader already knows CEF's architectural history.

26. **Severity**: Nit
    **Affected Scope**: Unspecified UX behavior on elevated launch / CEF auto-de-elevation — `rules.md` §5 (L66)
    **Discrepancy Description & Rationale**: `rules.md` L66 prohibits shipping a `do-not-de-elevate` workaround and cites CEF exit code 38 (`ResultCode.NormalExitAutoDeElevated`), but neither `architecture.md` nor `design.md` explains what the user sees or how the app handles being launched as Administrator.

27. **Severity**: Nit
    **Affected Scope**: Unenumerated "supported internal URLs" — `schema.md` §3 `ENT-004` (L72), §5 (L187)
    **Discrepancy Description & Rationale**: `ENT-004` permits persisting `http/https` and "supported internal URLs", while §5 rejects `javascript:`, `file:`, `data:`, and custom schemes. The allowlisted internal URLs (e.g., `about:blank` or custom app pages) are never listed.

28. **Severity**: Nit
    **Affected Scope**: `PersistSessionCookies` default and UI placement untracked — `schema.md` §3 `ENT-001` (L48), `architecture.md` §5.1 (L128), `design.md` §3 `UI-002` (L52), `decisions.md` §13 (L124–133)
    **Discrepancy Description & Rationale**: `architecture.md` L128 calls `persist_session_cookies` a "user-visible privacy/session setting" and `schema.md` L48 says its default is "selected by product privacy review", yet it is neither tracked in `decisions.md` `OPEN-###` / `prd.md` §8 nor listed among the UI controls in `design.md` `UI-002` or `Profile Details`.

29. **Severity**: Nit
    **Affected Scope**: `REQ-007` numbering gap in Must table — `prd.md` §4.1 (L54–55), §4.2 (L65)
    **Discrepancy Description & Rationale**: Section 4.1 jumps from `REQ-006` directly to `REQ-008`, with `REQ-007` appearing in Section 4.2 without a brief inline note explaining that `REQ-007` was moved to `Should`.

30. **Severity**: Nit
    **Affected Scope**: Illustrative DTO inconsistencies and ER diagram omissions — `schema.md` §4 (L168–179), §6 (L197–204)
    **Discrepancy Description & Rationale**: `CreateProfileCommand` uses `string? GroupId` (single string) instead of `Guid` / collection matching `ENT-003`'s many-to-many `Guid` keys, and omits `ColorHex`/`IconKey` (`UI-002`). Additionally, `AppSetting` (`ENT-010`) and `BackupJob` (`ENT-011`) are omitted from the ER diagram without note.

31. **Severity**: Nit
    **Affected Scope**: Minor naming inconsistency (`Switchly` vs. `SwitchlyNext` vs. `switchly-next`) — `README.md` §4–5 (L47–68), `schema.md` §1 (L9–10)
    **Discrepancy Description & Rationale**: The docs mix `SwitchlyNext` (repo directory & `%LOCALAPPDATA%` folder), `Switchly.sln` / `Switchly.*` (solution and project namespaces), and `switchly-next.db` (database filename) without noting the convention.
