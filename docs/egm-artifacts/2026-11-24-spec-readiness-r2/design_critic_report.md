# Hostile Design Critique — Switchly Next Canonical Specification Suite

**Reviewer Persona:** Hostile Design Critic (`egm-design`, fresh-context Goldfish subagent `0580783e-939b-45ae-93b8-73d152a1b2e9`)  
**Date:** 2026-11-24  
**Target Standard:** `.agents/plugins/egm/resources/document_standards/feature-spec-standard.md`  
**Audited Documents:** `prd.md`, `architecture.md`, `schema.md`, `design.md`, `rules.md`, `testing.md`, `decisions.md`, `plan.md`

**Summary Verdict:** **FAIL (6 Blockers, 8 Majors, 1 Grouped Nit).** The specification suite has solid product framing and clear anti-bypass/modularity boundaries, but contains fatal cryptographic, CEF Chrome Runtime lifecycle, Windows filesystem atomicity, and cross-device backup/restore contradictions that will break implementation or create critical security defects if not resolved before coding `M-002+`.

---

## Blockers

### B-1. [Blocker] Cross-Device Backup/Restore Breaks Chromium `os_crypt` DPAPI Cookie Encryption
- **Affected Scope:** `prd.md:58, 110-115, 136` (`REQ-011`, `FLOW-004`, `AC-011`), `architecture.md:169-175, 180, 198`, `decisions.md:77-84, 118-119` (`ADR-006`, `ADR-011`), `testing.md:44` (`TEST-012`)
- **Finding & Recommendation:**
  - `architecture.md:171` copies `<profile-guid>` directories into backups, while `decisions.md:119` (`ADR-011`) mandates: *"Backups must copy only `<profile-guid>` directories, never CEF global state."*
  - In CEF 156 (Chromium 156 Chrome Runtime), Chromium encrypts each profile's `Cookies` (and `Login Data` / `Web Data`) using an `os_crypt` AES-256-GCM key stored in **`RootCachePath\Local State`**, and that key is wrapped by **Windows DPAPI (`CurrentUser`)** (and/or Chromium App-Bound Encryption on Windows).
  - Two fatal consequences:
    1. **Cross-device restore (`TEST-012`, `AC-011`) cannot restore Chromium sessions/cookies:** Even if `Local State` were backed up, Device B cannot decrypt Device A's DPAPI-wrapped `os_crypt` key. Every restored profile's cookies will fail decryption or be purged by Chromium.
    2. **Even same-machine clean restore fails** if `ADR-011` excludes `Local State`: Chromium generates a fresh `os_crypt` key, rendering restored `Cookies` DBs undecryptable.
  - **Recommendation:** Explicitly define the cross-device restore contract in `REQ-011`, `AC-011`, `ADR-006`, and `ADR-011`: state that **browser login cookies/sessions are device-bound and NOT portable across machines** (consistent with `REQ-009`), and strip/exclude `Cookies` / `Network\Cookies` during cross-device restore (or detect machine mismatch via manifest) so Chromium boots cleanly without cookie-DB decryption errors. If same-machine session backup is desired, explicitly design `Local State` handling and its security boundary.

### B-2. [Blocker] `VaultCredential` Schema Has a Catastrophic AEAD Nonce-Reuse Flaw & Missing Key Hierarchy
- **Affected Scope:** `schema.md:126-137, 232` (`ENT-009 VaultCredential`), `architecture.md:30-31, 198-199`, `decisions.md:68-84` (`ADR-005`, `ADR-006`), `prd.md:57` (`REQ-010`)
- **Finding & Recommendation:**
  - `schema.md:130-133` defines **two separate ciphertext columns** (`UsernameCiphertext`, `PasswordCiphertext`) alongside a **single** `Nonce` and **single** `AuthenticationTag` column. Encrypting two plaintexts under the same `(Key, Nonce)` in AES-GCM completely breaks confidentiality (plaintext XOR recovery) and GHASH authenticity (key/forgery break); a single tag also cannot independently authenticate two ciphertexts unless they are serialized into one plaintext blob.
  - **Missing AAD (Associated Authenticated Data):** Without binding `(CryptoVersion, CredentialId, ProfileId, Origin)` into AES-GCM AAD, rows/ciphertexts can be transplanted across profiles or origins in SQLite without failing authentication, violating `REQ-003` isolation.
  - **Underspecified & contradictory key hierarchy (`ADR-005`, `schema.md:136, 232`):**
    - `decisions.md:73` wraps the local vault key with DPAPI `CurrentUser` and adds *"optional master-password protection and Windows Hello as local unlock experience."* If the DEK is wrapped solely by DPAPI `CurrentUser`, any process under the same Windows user can unwrap it without the master password—turning master password into bypassable UI theater. If the master password cryptographically wraps the DEK (via KDF), the spec never defines how Windows Hello unlocks it, which KDF + parameters are used, or where the wrapped keyblob is stored (*"held/wrapped outside DB"* in `schema.md:136` has no path/store contract).
    - `architecture.md:198` implies one app-wide *"vault master key"*, yet `schema.md:232` says deleting a profile's vault *"rotates/invalidates the relevant wrapped key"*—which would brick all other profiles unless per-profile DEKs exist.
  - **Recommendation:**
    1. Collapse `UsernameCiphertext` + `PasswordCiphertext` into one `SecretPayloadCiphertext` (canonical UTF-8 JSON `{username, password}` encrypted with a unique 96-bit CSPRNG nonce + 128-bit tag) OR give each ciphertext its own `(Nonce, Tag)`.
    2. Mandate AAD binding `(CryptoVersion, CredentialId, ProfileId, Origin)`.
    3. Specify the exact key hierarchy (App Master Key vs. per-profile DEKs, KDF algorithm & minimum parameters e.g. Argon2id or PBKDF2-SHA256 >= 600k iterations, wrapped-key storage location/ACL, exact cryptographic role of DPAPI vs. master password vs. Windows Hello, and key recovery policy).

### B-3. [Blocker] Scheduled Automatic Backup Contradicts "No Stored Passphrase" Rule & Open-Profile Snapshot Requirement
- **Affected Scope:** `prd.md:58, 110-113, 143, 178` (`REQ-011`, `FLOW-004`, `AC-017`), `schema.md:144-153` (`ENT-011`), `rules.md:47, 113`, `architecture.md:169-175`, `design.md:148`, `plan.md:114` (`TASK-032`), `testing.md:57` (`TEST-025`)
- **Finding & Recommendation:**
  - `FLOW-004` (`prd.md:110-113`) requires scheduled automatic backups (`ENT-011.Mode = Scheduled`, `TASK-032`, `TEST-025`) AND requires encrypting with a user passphrase (`ADR-006`: symmetric AES-256-GCM + passphrase KDF), while `schema.md:153` and `rules.md:47` forbid storing the backup passphrase or derived key. An unattended scheduler cannot derive a symmetric key from a passphrase it does not have.
  - Simultaneously, `architecture.md:170` requires closing/blocking profiles for a consistent snapshot, whereas `design.md:148` and `rules.md:113` forbid automatically closing tabs or discarding user forms. The spec does not define what scheduled backup does when profiles are actively open.
  - **Recommendation:**
    - Resolve the cryptographic contradiction via **hybrid public-key envelope encryption** for scheduled backups: generate a backup keypair whose **public key** is stored locally (allowing unattended encryption of per-archive ephemeral AES-256-GCM keys) while the **private key** is passphrase-encrypted (and embedded in the archive header or separately portable) for cross-device restore—OR explicitly downgrade scheduled backups to interactive prompt reminders / DPAPI-wrapped local key with a documented threat-model exception.
    - Define open-profile behavior during scheduled backup (e.g., skip open profiles with status `Partial` + notification, or defer until all target profiles close; note VSS requires elevation, which `rules.md:66` forbids).

### B-4. [Blocker] CEF Chrome Runtime `RootCachePath` ProcessSingleton & Profile File-Lock Lifecycle Breaks Multi-Process Locking, Live Delete, and Live Backup
- **Affected Scope:** `architecture.md:122-129, 137-140, 170-171`, `decisions.md:114-120` (`ADR-011`), `rules.md:36-37, 65`, `testing.md:49` (`TEST-017`)
- **Finding & Recommendation:**
  - Under `ADR-011`, `CefSettings.RootCachePath = %LOCALAPPDATA%\SwitchlyNext\Profiles`. In CEF Chrome Runtime, `RootCachePath` is Chromium's `user-data-dir` and acquires a **process-wide Chromium `ProcessSingleton` lock** across the ENTIRE `Profiles` directory during `Cef.Initialize()`. Two Switchly Next processes cannot both initialize CEF against the same `RootCachePath`—even for *different* profiles. Yet `architecture.md:140`, `rules.md:37`, and `testing.md:49` (`TEST-017`) only design **per-profile** locking and never specify an **app-level single-instance guard + IPC handoff** before `Cef.Initialize()`.
  - Furthermore, inside a running process, closing all browser windows for a `CefRequestContext` destroys renderer processes, **not necessarily Chromium's browser-process `Profile` / `NetworkContext` file handles** in `<profile-guid>` (which Chromium Chrome Runtime frequently retains on Windows until process exit). Consequently:
    1. Live profile deletion (`architecture.md:129`, `rules.md:36`) will hit Windows `ERROR_SHARING_VIOLATION` on locked files.
    2. Live backup (`architecture.md:171`) of a "closed" profile may fail to read locked files or copy torn SQLite WAL files.
  - **Recommendation:**
    1. Mandate an **app-level single-instance guard** (Windows named mutex + named-pipe IPC forwarding arguments/focus to the primary instance) executing *before* `Cef.Initialize()`.
    2. Verify via an explicit Windows file-handle test in `M-002`/`M-003` whether CEF 156 releases all `<profile-guid>` handles on context disposal; regardless, design a **deferred-delete tombstone queue** processed on next app startup before `Cef.Initialize()`.

### B-5. [Blocker] "Atomic Swap" Staged Restore Is Impossible on Windows While CEF & SQLite Hold Open Handles, and Plaintext Staging Risks Disk Exhaustion/Exposure
- **Affected Scope:** `prd.md:114-115, 153` (`FLOW-004`), `architecture.md:171-175`, `design.md:108-110`, `rules.md:85, 94`, `schema.md:9-10`, `plan.md:115` (`TASK-033`)
- **Finding & Recommendation:**
  - `design.md:110` and `rules.md:85` require staged restore with *"atomic swap"* / *"atomic promotion"* leaving live data untouched on failure.
  - NTFS does not provide an atomic transaction across two separate trees (`Data\switchly-next.db` and `Profiles\<guid>`). Worse:
    1. `Microsoft.Data.Sqlite` pools open file handles by default (`SqliteConnection.ClearAllPools()` is required to release `switchly-next.db`).
    2. CEF holds active handles inside `RootCachePath` (`Local State`, `Default`, opened profiles) for the entire process lifetime—you cannot replace `Profiles\` while CEF is initialized.
    3. A crash or antivirus file lock (`MsMpEng.exe`) mid-swap between `Data\` and `Profiles\` corrupts cross-store consistency.
    4. Copying plaintext profiles to a staging directory (`architecture.md:171, 175`) duplicates multi-GB Chromium folders in cleartext on SSD (requiring 2x–3x free disk space) and leaves plaintext remnants if interrupted.
  - **Recommendation:**
    - Design a **two-phase restart-to-restore protocol**: validate and decrypt into staging (after checking free disk space >= 2x uncompressed manifest size + margin), write a signed/checksummed `pending-restore.json`, restart Switchly Next, and execute a **single-parent directory generation swap** (unify `Data\` and `Profiles\` under a single live root e.g. `State\live\` -> `State\prev\`, `State\staging\` -> `State\live\`) *before* `Cef.Initialize()` or EF Core initialization, with deterministic boot-time rollback if interrupted.
    - For backup creation, stream-encrypt directly into the archive without writing a full plaintext staging copy to disk.

### B-6. [Blocker] AES-256-GCM Archive Framing & Chunking Unspecified (OOM / 2 GB `Span` Ceiling & Truncation Attack Risk)
- **Affected Scope:** `architecture.md:31, 173`, `decisions.md:77-84` (`ADR-006`), `schema.md:190`, `testing.md:45` (`TEST-013`)
- **Finding & Recommendation:**
  - Chromium profile directories routinely reach 500 MB–5 GB+. .NET's `AesGcm.Encrypt()` requires contiguous in-memory buffers (`Span<byte>`, capped at 2 GB `int.MaxValue`). Encrypting a whole archive in one `AesGcm` call will OOM or crash on real user profiles.
  - Splitting into chunks without a formal authenticated streaming protocol (deterministic per-chunk nonces, `chunkIndex` + `isFinalChunk` + `header` bound in AAD) enables silent chunk truncation or reordering, directly failing `TEST-013`.
  - Additionally, backing up unfiltered `<profile-guid>` folders copies huge disposable caches (`Cache`, `Code Cache`, `GPUCache`, `GrShaderCache`, `DawnCache`, `Crashpad`).
  - **Recommendation:** Define an explicit **chunked AEAD archive wire format** in `ADR-006`/`schema.md` (magic bytes, format version, KDF ID + params + salt, chunk size e.g. 64 KiB–1 MiB, STREAM-style nonce derivation with final-chunk bit and header digest in AAD) plus an explicit Chromium cache-exclusion list for backups.

---

## Majors

### M-1. [Major] Feature Spec Standard Violation: Missing `CefSharp.Wpf` (OSR) vs. `CefSharp.Wpf.HwndHost` Evaluation & Incomplete Alternatives Analysis
- **Affected Scope:** `feature-spec-standard.md:13-28`, `architecture.md:24-35, 241`, `decisions.md:22-33, 45-66, 86-91, 114-120, 133` (`ADR-001`, `ADR-003`, `ADR-004`, `ADR-007`, `ADR-011`, `OPEN-008`), `plan.md:3, 41, 150`
- **Finding & Recommendation:**
  - `CefSharp` on WPF has two radically different hosting models that are never even mentioned or evaluated:
    - `CefSharp.Wpf` (Off-Screen Rendering / OSR): avoids WPF airspace issues, but suffers from DPI/IME/accessibility/touch/drag-drop defects and higher CPU/GPU overhead on video/WebGL.
    - `CefSharp.Wpf.HwndHost` (native Win32 child HWND): native rendering/IME/WebGL performance, **but suffers from WPF's airspace problem** (WPF overlays, popups, dropdowns, modals, and sidebar flyouts clip behind the browser HWND).
  - Furthermore, `OPEN-008` (`decisions.md:133`, `plan.md:41`) ran M-001 as a headless `CefSharp.OffScreen.NETCore` console app, yet `plan.md:150` declares `M-001` complete and `decisions.md:47` marks `ADR-003` "Validated"—leaving WPF hosting (`architecture.md:241`), DPI scaling (`design.md:130`), and Windows 10 testing (`testing.md:73`) unvalidated!
  - Additional Alternatives Rule gaps: `ADR-007` lists zero alternatives; `ADR-004` never evaluates SQLCipher/encrypted SQLite despite `schema.md:241` leaving DB encryption open while storing private browsing history; `schema.md:190` specifies *"Limit row sizes and archive entry counts"* with zero concrete limits; M-001 resource baselines are not recorded in `architecture.md`/`testing.md` despite M-001 being marked complete.
  - **Recommendation:** Add an ADR comparing `CefSharp.Wpf` (OSR) vs. `CefSharp.Wpf.HwndHost` (and airspace mitigation strategy), gate `M-002 TASK-007` on a real WPF hosting spike (menus/modals/DPI/IME/WebGL), add alternatives to `ADR-004`/`ADR-007`/`ADR-011`, and specify concrete numerical bounds for `schema.md:190` and `AC-018`.

### M-2. [Major] Split-Brain Data Ownership Between Switchly SQLite (`Bookmark`, `HistoryEntry`, `DownloadRecord`) and Chromium Internal DBs
- **Affected Scope:** `architecture.md:158, 179-180`, `schema.md:4, 13, 72, 81-112, 230`, `decisions.md:64-65` (`ADR-004`), `rules.md:44`
- **Finding & Recommendation:**
  - In CEF Chrome Runtime, Chromium still writes its own `History`, `Visited Links`, and `Bookmarks` inside `<profile-guid>`. Deleting rows from Switchly's `HistoryEntry` (`schema.md:230`) does NOT erase Chromium's `History` / `Visited Links` (causing `:visited` CSS and Chromium-side history leaks), and CEF/CefSharp does not expose Chromium's full `BrowsingDataRemover` API (only cookie deletion).
  - `schema.md:94` leaves `HistoryEntry.Url` query-string retention unspecified by default, and `schema.md:72` does not specify stripping OAuth tokens/secrets from `BrowserTabSession.Url` query strings.
  - `schema.md:111` leaves `DownloadRecord.State = InProgress` (and similarly `BackupJob`/`ImportJob` `Running` states) with no crash-recovery startup reconciliation rule.
  - **Recommendation:** Verify CEF 156 browsing-data-clearing capabilities (or implement closed-profile cleanup of Chromium `History`/`Visited Links` files and document UI honesty in `UI-006`). Default query-string stripping/redaction for sensitive auth params in `HistoryEntry` and `BrowserTabSession`. Add startup reconciliation transitioning stale `InProgress`/`Running` records to `Failed`/`Interrupted`.

### M-3. [Major] Auto-Grouping (`REQ-004`) Has Unbounded Sidebar Growth, Missing eTLD+1 Rules, PK Collisions, and Missing Schema Columns
- **Affected Scope:** `prd.md:52, 66, 130, 141` (`REQ-004`, `REQ-015`, `AC-005`, `AC-015`), `schema.md:34-66, 185` (`ENT-001`, `ENT-002`, `ENT-003`)
- **Finding & Recommendation:**
  - **Missing schema columns:** `ENT-001 BrowserProfile` (`schema.md:34-49`) has **no `IsFavorite` column** despite `REQ-015`/`AC-015`/`TEST-023` and `design.md:20`. `ENT-002 ProfileGroup` (`schema.md:52-59`) has **no `IsHidden` column** despite `schema.md:59` stating *"users may hide them."*
  - **Unbounded auto-group sprawl:** `REQ-004` never defines what input populates `WebsiteDomain` groups (only `StartupUrl`? bookmarks? all history?), creating risk of hundreds of noisy redirect/SSO/CDN groups in the sidebar.
  - **Domain normalization (`schema.md:185`):** Lowercase URI host splits `www.seaart.ai` and `seaart.ai` (no Public Suffix List / eTLD+1 rule).
  - **Membership PK collision (`schema.md:65`):** Composite PK `(ProfileId, GroupId)` has one `MembershipSource` (`Manual | Auto`) with no rule for pruning stale `Auto` memberships when history is cleared or resolving conflicts.
  - **Recommendation:** Add `BrowserProfile.IsFavorite` and `ProfileGroup.IsHidden` to `schema.md`. Define exact auto-group derivation criteria (e.g., eTLD+1 via Public Suffix List from `StartupUrl`, bookmarks, and top visited domains above a threshold, excluding SSO/redirect hosts) and pruning rules.

### M-4. [Major] Inverted Milestone Dependencies, Contradictory Progress Tracker, Missing `REQ-010` Acceptance Criteria, and Missing FK Delete Rules
- **Affected Scope:** `plan.md:27-28, 107, 111, 148-152`, `schema.md:48, 138-179, 200`, `prd.md:57, 135`, `testing.md:43, 54`
- **Finding & Recommendation:**
  1. **Inverted milestone dependency (`plan.md:27-28, 107, 111`):** `M-006` (`TASK-028`) builds the encrypted password CSV vault writer (`TEST-010`, `TEST-022`), but `M-007` (`TASK-029`: DPAPI local vault key wrapping) depends on `M-006`. `M-006` cannot write encrypted vault records before `TASK-029` exists.
  2. **Progress tracker contradiction (`plan.md:150-152`):** Line 150 says `M-001 | Complete`, line 151 says `M-002 | In progress`, and line 152 says `M-002 to M-009 | Not started | No code or test execution has been performed`.
  3. **Missing Acceptance Criterion for `REQ-010` unlock:** `AC-010` (`prd.md:135`) only covers password CSV import; `prd.md` has NO `AC` covering DPAPI/master password/Windows Hello lock & unlock, forcing `TEST-011` (`testing.md:43`) to mis-map onto `AC-010`.
  4. **Missing `ON DELETE` rules across schema (`schema.md:168-179`):** Referential actions (`CASCADE` vs `SET NULL` vs `RESTRICT`) are omitted for 7 of 8 FKs (notably `ImportJob.TargetProfileId` and self-FK `Bookmark.ParentBookmarkId`). Deleting a profile with `ImportJob` history will fail with a FK constraint error.
  5. **Unresolved `M-002` migration blocker (`schema.md:48`):** `ENT-001.PersistSessionCookies` default is still undecided despite `TASK-009` creating the SQLite migration baseline. Also, `ENT-010 AppSetting` lacks allowlisted key contracts for `REQ-017` backup schedule/retention, and `CreateProfileCommand.GroupId` (`schema.md:200`) is `string?` instead of `Guid?` / `IReadOnlyList<Guid>`.
  - **Recommendation:** Move `TASK-029` (vault crypto + DPAPI wrapping) ahead of `M-006`; fix `plan.md:152`; add a dedicated `AC` for `REQ-010` lock/unlock and remap `TEST-011`; specify `ON DELETE` semantics for every FK and lock `PersistSessionCookies` default before `TASK-009`.

### M-5. [Major] Browser Attack Surface Gaps: External Protocol Handlers, DevTools Port, Download Sanitization/MoTW, Vault UX, and App-Lock Semantics
- **Affected Scope:** `architecture.md:184-202`, `rules.md:55-67`, `schema.md:102-111, 240`, `design.md:59-61, 148`, `testing.md:106-107`
- **Finding & Recommendation:**
  1. **External URI protocol launching:** No policy restricts CEF from invoking dangerous OS protocol handlers (`smb://` NTLM leak, `ms-msdt:`, `search-ms:`, `file://`, custom handlers) via `OnBeforeBrowse` / external protocol interception.
  2. **Production CEF hardening:** Spec never explicitly mandates disabling `CefSettings.RemoteDebuggingPort` in release builds (an open localhost port lets any local process dump every profile's cookies/DOM) or forbidding `JavascriptObjectRepository` registration on web origins.
  3. **Download filename & MoTW (`schema.md:105`):** Sanitization must reject Windows reserved device names (`CON`, `NUL`, `COM1`–`LPT9`), trailing dots/spaces, NTFS Alternate Data Streams (`:`), and Unicode RLO (`U+202E`) spoofing, and preserve Windows Mark-of-the-Web (`Zone.Identifier`) for SmartScreen protection.
  4. **Write-only Password Vault (`schema.md:240`, `design.md`):** `REQ-008` imports CSV passwords into `VaultCredential`, but `design.md` defines NO UI to view/copy/edit/delete stored credentials (nor clipboard auto-clear / `ExcludeClipboardContentFromMonitorProcessing` so Windows Clipboard History doesn't persist copied passwords).
  5. **Undefined App-Lock behavior (`UI-010`, `REQ-010`):** Spec never states whether locking the app hides/minimizes live CEF windows behind a lock screen (preserving forms) or only locks `VaultCredential`, nor defines auto-lock idle timeout or master-password brute-force throttling.
  - **Recommendation:** Add a concrete **Browser Hardening Policy** to `architecture.md`/`rules.md`, add a minimal Vault Management UI (with clipboard protection) to `design.md`, and define exact App-Lock state transitions for active browser windows.

### M-6. [Major] CEF Subprocess Zombie Leaks, Elevated Launch UX, PID Lock-File Races, and Shutdown Deadlocks
- **Affected Scope:** `architecture.md:133-149`, `rules.md:60, 66`, `testing.md:71, 100`
- **Finding & Recommendation:**
  1. **Orphaned subprocesses:** A hard crash of `Switchly.App` can leave `CefSharp.BrowserSubprocess.exe` children holding locks on `RootCachePath`. No Windows Job Object (`JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`) is specified.
  2. **Elevated launch UX (`rules.md:66`):** `rules.md:66` forbids `do-not-de-elevate`, meaning an elevated launch (including from an elevated installer's "Launch now" checkbox) silently exits with CEF code 38 unless the app checks elevation *before* `Cef.Initialize()` and shows an actionable message (and the `ADR-008` installer de-elevates post-install launch).
  3. **Stale PID lock files (`architecture.md:140`):** PID-based lock files suffer from Windows PID reuse after crash/reboot. Kernel-managed locks (named `Mutex` + `FileStream(FileShare.None)`) are released instantaneously by the OS on process death with zero stale locks.
  4. **CEF Shutdown sequencing (`rules.md:60`):** `Cef.Shutdown()` deadlocks or access-violates unless invoked on the initialization UI thread *after* all browsers finish `OnBeforeClose`. No bounded shutdown state machine or forced-exit timeout is specified.
  - **Recommendation:** Specify a Windows Job Object in `Switchly.BrowserHost`, a pre-`Cef.Initialize()` elevation guard, kernel-managed mutex/file locks instead of PID files, and an explicit bounded CEF shutdown state machine.

### M-7. [Major] Browser Import Edge Cases: Proprietary SNSS Session Binary Format, Background Chrome/Edge Locks, Managed-Memory Password Lifetime, and Undefined Deduplication Keys
- **Affected Scope:** `prd.md:55, 100-107`, `architecture.md:204-214`, `design.md:95`, `rules.md:46, 71, 74, 93`, `plan.md:105` (`TASK-026`)
- **Finding & Recommendation:**
  1. **Chromium SNSS binary format (`architecture.md:211`, `TASK-026`):** Chrome/Edge open tabs are stored in Chromium's undocumented binary SNSS format (`Sessions/Session_*`, `Tabs_*`), not JSON/SQLite. Parsing untrusted binary formats in C# requires strict bounds checking and fuzz testing, yet `architecture.md` does not acknowledge SNSS complexity or fallback options.
  2. **Background browser locks (`architecture.md:208`, `rules.md:71`):** Chrome background apps and Edge Startup Boost keep `History`/`Sessions` locked even after the user closes all browser windows, leaving `UI-009` users stuck unless guided to exit background tray processes.
  3. **Managed `string` password lifetime (`rules.md:46`):** Immutable .NET `string` objects cannot be zeroed from GC memory. CSV parsing should use `byte[]`/`char[]` buffers wiped via `CryptographicOperations.ZeroMemory`, and the spec should explicitly document residual managed-heap / crash-dump boundaries.
  4. **Missing deduplication keys (`design.md:95`):** Preview promises duplicate counts, but `schema.md` defines no dedup identity for Bookmarks, History, or `VaultCredential` (where encrypted `UsernameCiphertext` uses a random nonce and cannot be deduplicated via SQL `WHERE`).
  - **Recommendation:** Document SNSS scope/bounds checks (or narrow `TASK-026` fallback), add background-process guidance to `UI-009`, mandate `CryptographicOperations.ZeroMemory` buffer handling for CSV parsing, and define explicit deduplication keys per imported category.

### M-8. [Major] `ADR-011` Shared `RootCachePath` Directory Has Silent Global-Context Fallback & `MAX_PATH` Risks
- **Affected Scope:** `architecture.md:122-129`, `decisions.md:114-120` (`ADR-011`), `schema.md:10, 42`, `rules.md:35, 65`
- **Finding & Recommendation:**
  1. **Silent fallback to global `Default` profile:** Because `Profiles\` also hosts CEF's global `Default` context (`ADR-011`), any bug constructing a `ChromiumWebBrowser` or popup without a valid `RequestContext` silently shares `Profiles\Default` across accounts! No runtime fail-closed invariant is specified.
  2. **Mixed directory enumeration:** `Profiles\` mixes `<profile-guid>` folders with CEF runtime folders (`Default`, `GrShaderCache`, `Crashpad`, `Local State`); backup/cleanup/disk-usage scanners must strictly allowlist known `ProfileId` GUIDs.
  3. **Windows `MAX_PATH` (260 chars):** `%LOCALAPPDATA%\SwitchlyNext\Profiles\<36-char-guid>\...` leaves limited headroom for deeply nested Chromium Service Worker/CacheStorage paths without `<longPathAware>true</longPathAware>` in the app manifest.
  - **Recommendation:** Enforce runtime fail-closed assertions in `Switchly.BrowserHost` verifying non-global `RequestContext`, direct-child GUID `CachePath`, and `OnBeforePopup` context inheritance (`RequestContext.IsSame()`). Add `<longPathAware>true</longPathAware>` to `Switchly.App` manifest and use 32-char lowercase hex GUID directory names (`Guid.ToString("N")`).

---

## Nits

### N-1. [Nit] Minor Traceability, Allowlist & Copy Inconsistencies
- **Affected Scope:** `prd.md:47-66, 103`, `schema.md:41, 72, 86`, `testing.md:34` (`TEST-002`), `design.md:133`
- **Finding & Recommendation:**
  - `prd.md:47-61` jumps from `REQ-006` to `REQ-008` because `REQ-007` moved to Should (`prd.md:65`); add a one-line cross-reference in the Must table to prevent reader confusion.
  - `prd.md:103` (`FLOW-003` step 3) still lists *"ekstensi"* without the *"daftar opsional / informasi saja"* qualifier used in `REQ-008` (`prd.md:55`) and `design.md:91`.
  - `schema.md:72` permits *"supported internal URLs"* for `BrowserTabSession.Url` without enumerating the exact allowlist (e.g., `about:blank`).
  - `TEST-002` (`testing.md:34`) tests create/edit/archive/delete for `REQ-002`, but omits testing **profile duplication** (verifying config is copied without copying browser storage/cookies/vault credentials as required by `prd.md:50` and `rules.md:38`).
  - `design.md:133` specifies Bahasa Indonesia for MVP UI with localization support; add `.resx` localization scaffolding explicitly to `M-002` (`TASK-007`/`TASK-008`).
