# Switchly Next — Design Critic Report

**Reviewer:** egm-design (Goldfish — fresh context)  
**Scope:** Full specification corpus (9 documents)  
**Date:** 2026-11-24  
**Status:** Report delivered inline; subagent terminated with model error after completing review.  
**Verdict:** The specification corpus is unusually thorough for a pre-implementation draft. It correctly front-loads the riskiest decision (CEF engine validation) and maintains honest "Open" markers for unresolved decisions. However, there are concrete gaps that will cause implementation ambiguity, security under-specification, and potential catastrophic data loss if not addressed before coding begins.

---

## 1. ARCHITECTURAL RISKS

### 1.1 🔴 Blocker — CEF Process Model and WPF Thread Affinity Not Specified

**Affected:** architecture.md §5, rules.md §5

CEF runs a multi-process architecture (browser process, GPU process, renderer processes). The specification mentions "marshal callbacks to the correct dispatcher" (rules.md L59) but never specifies:
- Whether the app uses `CefSettings.MultiThreadedMessageLoop` or integrates into WPF's dispatcher.
- How the CEF message loop interacts with WPF's `Dispatcher` — this is a known source of deadlocks and UI freezes in CefSharp WPF applications.
- The exact process model: will each profile get its own renderer process, or will CEF share renderer processes across profiles?

**Recommendation:** ADR-003 must specify the CEF message loop integration strategy and document the expected process tree topology. This directly affects both isolation guarantees and resource scaling.

### 1.2 🟡 Major — Single-Instance Enforcement Mechanism Undefined

**Affected:** architecture.md §5.2 L142, testing.md TEST-017

The spec says "One app instance must prevent simultaneous write access to the same profile cache path" and "Use per-profile named mutex or lock file." TEST-017 tests this. But there is no specification for:
- Application-level single-instance enforcement (should only one Switchly process run at a time, or multiple with profile-level locking?).
- Named mutex naming convention (predictable names are a local DoS vector).
- Stale lock recovery algorithm (timeout-based? process-ID check?).

**Recommendation:** Specify the single-instance model explicitly. If multiple Switchly instances are allowed, the profile-level locking protocol must be formally defined including race conditions during startup.

### 1.3 🟡 Major — Module Boundary Contracts Are Aspirational, Not Enforceable

**Affected:** architecture.md §4, rules.md §2

The specification lists 5 projects with dependency direction rules, but provides no interface contracts for the critical boundaries:
- `IBrowserRuntime`, `IBrowserProfileContext`, `IBrowserWindowHost` are mentioned in rules.md L58 but never defined in schema.md or architecture.md.
- The `Switchly.Application` → `Switchly.BrowserHost` port contract is completely unspecified.

**Recommendation:** Define at minimum the port interfaces for BrowserHost before implementation.

### 1.4 🟡 Major — Duplicate Paragraphs in prd.md and architecture.md

**Affected:** prd.md L33-37 (duplicate §3 paragraph), architecture.md L9-13 (duplicate §1 paragraph)

Both files contain exact duplicate paragraphs about priority provider compatibility targets. This is a copy-paste artifact.

**Recommendation:** Remove duplicates. Establish a single canonical location for the provider compatibility target list.

### 1.5 🟢 Nit — No Explicit Dependency Injection Framework Choice

**Affected:** architecture.md §4, rules.md §2

Acceptable for DRAFT but should be pinned during M-002.

---

## 2. SECURITY GAPS

### 2.1 🔴 Blocker — Vault Key Hierarchy and Rotation Not Specified

**Affected:** schema.md ENT-009, architecture.md §8, decisions.md ADR-005

The following are completely unspecified:
- **Key hierarchy**: Is there one vault master key per app, per profile, or per credential?
- **Key derivation path**: DPAPI wraps what exactly?
- **Master password integration**: Does it replace DPAPI, add a layer, or derive the key directly?
- **Key rotation protocol**: What triggers rotation?
- **Windows account password change**: DPAPI `CurrentUser` keys survive normal password changes but NOT admin resets.

**Recommendation:** Create a formal key hierarchy diagram before M-007.

### 2.2 🟡 Major — Backup Re-encryption of Vault Records Under-specified

**Affected:** schema.md ENT-009 L136, architecture.md §6 Flow C

Unclear whether whole-archive encryption or per-record re-encryption. CEF directory cookies in backups are NOT re-encrypted.

### 2.3 🟡 Major — CEF Profile Directories in Backup Contain Sensitive Data in Plaintext

**Affected:** architecture.md §6 Flow C

Crash during backup leaves plaintext copy of all browser data on disk. "Best-effort" cleanup is insufficient.

### 2.4 🟡 Major — JavaScript Bridge Trust Boundary Not Defined

**Affected:** architecture.md §8, rules.md §5

No explicit statement whether any JS bridge is exposed to web content.

### 2.5 🟢 Nit — No Threat Model Document

Scattered controls are good but need systematic coverage verification before M-007.

---

## 3. SCALABILITY CONCERNS

### 3.1 🔴 Blocker — CEF Resource Scaling Cliff Not Quantified or Mitigated

**Affected:** architecture.md §5.3, prd.md §7, testing.md §5

Each active profile spawns at minimum 1 renderer process (~80-150 MB). 10 active profiles = ~1-1.5 GB for renderers alone. No CEF suspend/hibernate API exists. Tab discarding availability through CefSharp is unknown.

**Recommendation:** M-001 must answer: Can renderers be terminated per-profile without losing session state? Is tab discarding available? What's the realistic max concurrent profiles on 8 GB / 16 GB?

### 3.2 🟡 Major — SQLite Contention Under Multi-Profile Writes

Single SQLite database, multiple profiles writing history/tab updates. No WAL mode specification.

### 3.3 🟡 Major — Disk Space Scaling for Profiles and Backups

Each CEF profile directory can grow to hundreds of MB. 50+ profiles with daily backups * 7-day retention = massive disk usage. No low-disk-space handling.

### 3.4 🟢 Nit — Installer/Package Size Not Estimated

CEF runtime ~200-300 MB. Total installer 350-500+ MB. No size estimate in specs.

---

## 4. DATA MODEL ISSUES

### 4.1 🟡 Major — No Schema for Profile-Scoped Settings

ENT-010 AppSetting is global. Per-profile proxy, user-agent, content settings, download directory not modeled.

### 4.2 🟡 Major — History Deduplication Strategy Missing

Contradictory signals: `VisitCount >= 1` suggests aggregation, but index on `VisitedAtUtc DESC` suggests per-visit rows.

### 4.3 🟡 Major — Bookmark Import Conflict Resolution Undefined

No merge/deduplication strategy for importing into profiles with existing bookmarks.

### 4.4 🟢 Nit — No Soft-Delete or Audit Trail for Profiles

Profile deletion is permanent ("remove metadata and directory"). No recovery without backup.

### 4.5 🟢 Nit — BackupJob Has No Reference to Archive Encryption Parameters

Archive manifest format including KDF parameters is unspecified.

---

## 5. BROWSER LIFECYCLE GAPS

### 5.1 🔴 Blocker — CEF Initialization Parameters Not Specified

~40 `CefSettings` parameters unspecified, including: `CachePath` for global cache, `LogFile`/`LogSeverity`, `UserAgent` (directly affects provider acceptance), `PersistSessionCookies` global vs per-context, sandbox mode.

**Recommendation:** Create a CefSettings specification table. User-agent strategy must be an ADR.

### 5.2 🟡 Major — Renderer Crash Recovery Protocol Incomplete

Single tab crash vs entire profile crash vs browser process crash — no defined recovery paths.

### 5.3 🟡 Major — CEF Shutdown Sequencing Not Specified

Strict shutdown required: close browsers → dispose contexts → Cef.Shutdown(). Getting this wrong causes hangs, AVs, corrupted data.

### 5.4 🟡 Major — Popup and New-Window Routing Policy Incomplete

`window.open()`, `target="_blank"`, OAuth redirect popups — no routing policy defined. Directly affects provider login compatibility.

### 5.5 🟢 Nit — DevTools Access Not Mentioned

No decision on whether users can open Chrome DevTools (F12).

---

## 6. BACKUP/CRYPTO GAPS

### 6.1 🔴 Blocker — KDF Algorithm and Parameters Not Specified

Which KDF? PBKDF2? Argon2id? scrypt? No parameters proposed. No CryptoVersion-to-parameters mapping.

### 6.2 🟡 Major — Archive Format Not Specified

Container format, header structure, encryption envelope, file hash algorithm, file extension — all undefined.

### 6.3 🟡 Major — Passphrase Strength Validation Absent

No minimum length, complexity, or empty-passphrase prevention.

### 6.4 🟢 Nit — No Specification for Backup Verification After Write

No post-write validation defined.

---

## 7. UNMITIGATED CATASTROPHIC RISKS

### 7.1 🔴 Blocker — No Recovery Path for DPAPI Key Loss

Windows profile corruption or admin password reset = all vault credentials permanently lost. No recovery policy defined.

### 7.2 🔴 Blocker — Priority Provider Rejection Has No Fallback Strategy

If ALL FOUR priority providers reject embedded CEF, no minimum viable provider threshold or product pivot is defined.

### 7.3 🟡 Major — CEF Security Update Lag

No maximum acceptable lag behind Chromium stable. No emergency patching process.

### 7.4 🟡 Major — Concurrent Backup and Profile Activity Can Corrupt Archive

CEF continuously writes to profile directory during active browsing. No consistency strategy (VSS, shutdown-required, or accepted inconsistency).

### 7.5 🟡 Major — No Anti-Tampering for the Application SQLite Database

Unencrypted SQLite at known path. No integrity verification.

---

## Summary Counts

| Severity | Count |
|----------|-------|
| 🔴 Blocker | 6 |
| 🟡 Major | 16 |
| 🟢 Nit | 5 |
| **Total** | **27** |

### Critical Path Blockers (must resolve before M-001 implementation):
1. CEF process model and WPF thread affinity (§1.1)
2. CefSettings specification including user-agent strategy (§5.1)
3. Minimum provider acceptance threshold (§7.2)

### Must Resolve Before M-007 (Security/Backup milestone):
4. Vault key hierarchy and rotation (§2.1)
5. KDF algorithm and parameters (§6.1)
6. Archive format specification (§6.2)
7. DPAPI key loss recovery policy (§7.1)
8. Backup consistency during active browsing (§7.4)

### Should Resolve Before M-002 (Foundation):
9. Browser host port interface contracts (§1.3)
10. Resource scaling cliff analysis and suspend strategy (§3.1)
11. SQLite WAL mode and contention strategy (§3.2)
