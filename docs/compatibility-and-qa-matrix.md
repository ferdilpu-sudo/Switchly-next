# Switchly Next — Windows Compatibility & QA Regression Matrix (M-008)

**Date:** 2026-11-24  
**Runtime:** `.NET 10.0` (`net10.0-windows`, `win-x64`)  
**Browser Engine:** `CefSharp.Wpf.NETCore 156.0.300` / CEF `156.0.30` / Chromium `156.0.8078.96` (Chrome Runtime)  
**Primary Verified OS:** Windows 11 x64

## 1. OS & Platform Compatibility Matrix (`TASK-037`)

| Platform | Support Status | Evidence / Notes |
|---|---|---|
| Windows 11 x64 (non-elevated) | **Verified (Primary Target)** | M-001 CEF PoC (`artifacts/engine-spike/task-002..006`), M-002..M-007 + D6 desktop GUI verified by Product Owner. |
| Windows 11 x64 (elevated Admin session) | **Guarded by Design** | Release build enforces pre-init Administrator check (CEF exit code 38 policy, `rules.md` §5); Debug build supports local elevated dev testing. |
| Windows 10 Enterprise / LTSC x64 | **NOT RUN (Pending Target Hardware)** | Supported by Microsoft .NET 10 matrix; requires physical/VM validation before public beta (`OPEN-002`). |
| Windows 10 Home / Pro x64 | **Best-Effort Only (Unqualified)** | Past regular Microsoft support (14 Oct 2025) and not listed on .NET 10 consumer support matrix (`OPEN-002`). |

## 2. Priority Provider Compatibility (`M-001` Verified)

| Target | URL | M-001 Tier-2 Manual Sign-In | Notes |
|---|---|---|---|
| Google Flow | `https://flow.google` | **PASS** | Recorded in `artifacts/engine-spike/task-003-results.md`. |
| ChatGPT | `https://chatgpt.com` | **PASS** | Recorded in `artifacts/engine-spike/task-003-results.md`. |
| Dola | `https://dola.com` | **PASS** | Recorded in `artifacts/engine-spike/task-003-results.md`. |
| SeaArt | `https://seaart.ai` | **PASS** | Recorded in `artifacts/engine-spike/task-003-results.md`. |

## 3. Acceptance Test Traceability (`TEST-001`–`TEST-026`)

| TEST ID | Requirement | Status | Verification Evidence |
|---|---|---|---|
| `TEST-001` | `REQ-001`, `REQ-014` | **PARTIAL (Win11 PASS / Win10 NOT RUN)** | Win11 WPF launch verified in M-002..M-007; Win10 pending `OPEN-002` hardware. |
| `TEST-002` | `REQ-002` | **PASS** | `ProfileCrudIntegrationTests` + M-003 GUI check. |
| `TEST-003` | `REQ-003` | **PASS** | M-001 `task-002-results.md` + `ProfileSessionManagerTests` + M-003 GUI check. |
| `TEST-004` | `REQ-003` | **PASS** | M-001 `task-002-results.md` + `PersistSessionCookies=true` default (D4). |
| `TEST-005` | `REQ-004` | **PASS** | `GroupServiceIntegrationTests` + M-003 GUI check. |
| `TEST-006` | `REQ-005` | **PASS** | `ProfileSessionManagerTests` + M-004 multi-window/tab GUI check. |
| `TEST-007` | `REQ-006` | **PASS** | `BookmarkAndHistoryIntegrationTests`, `DownloadServiceIntegrationTests`, `TabSessionIntegrationTests`. |
| `TEST-008` | `REQ-007` | **DEFERRED** | Optional extensions deferred post-MVP (`M-005`). |
| `TEST-009` | `REQ-008`, `REQ-009` | **PASS** | `BrowserImportIntegrationTests` + M-006 GUI check (no cookies/sessions imported). |
| `TEST-010` | `REQ-008`, `REQ-010` | **PASS** | `PasswordVaultIntegrationTests` (reopened per D6: unique-nonce AES-256-GCM + AAD, DPAPI key, CSV import). |
| `TEST-011` | `REQ-010` | **PASS** | `SecurityAndBackupIntegrationTests` (DPAPI round-trip, master password verify, fail-closed on unreadable DPAPI). |
| `TEST-012` | `REQ-011` | **PASS** | `SecurityAndBackupIntegrationTests` (chunked AES-256-GCM backup, cache/cookie exclusion D3, restart-to-restore swap). |
| `TEST-013` | `REQ-011` | **PASS** | `SecurityAndBackupIntegrationTests` (wrong passphrase, byte tamper, header tamper, truncation, trailing bytes, future version, `../` path traversal). |
| `TEST-014` | `REQ-012` | **PASS** | Offline Free MVP launches and creates profiles without registration/sign-in. |
| `TEST-015` | `REQ-013` | **PASS** | `FreeBetaEntitlementServiceTests` (108/108 Release tests PASS on 2026-11-25): Free Beta runs offline with no account requirement and no artificial profile quotas (D8). |
| `TEST-016` | `REQ-005` | **PARTIAL (M-001 Baseline PASS / Full 10-profile WPF stress NOT RUN)** | M-001 resource baseline recorded in `artifacts/engine-spike/task-005-results.md`; live RAM monitor added in M-008. |
| `TEST-017` | `REQ-003` | **PASS** | `ProfileLockingTests` (`SingleInstanceMutexGuard`, `InProcessProfileLockManager`). |
| `TEST-018` | `REQ-008` | **PASS** | `BrowserImportIntegrationTests` (locked History DB detection without killing Chrome/Edge, malformed DB, unsupported schema/SNSS version). |
| `TEST-019` | `REQ-007` | **DEFERRED** | Optional extensions deferred post-MVP (`M-005`). |
| `TEST-020` | `REQ-011` | **PASS** | `SecurityAndBackupIntegrationTests` (mid-flight cancellation cleanup & rollback path). |
| `TEST-021` | `REQ-006` | **PASS** | `ProfileLifeSpanHandler` + `ProfileDownloadHandler` + M-004 GUI check. |
| `TEST-022` | `REQ-010` | **PASS** | Verified zero plaintext passwords/cookies/tokens in SQLite, `ImportJob`, `BackupJob`, and redacted CSV warnings. |
| `TEST-023` | `REQ-015` | **PASS** | `ProfileCrudIntegrationTests` + M-003 GUI check. |
| `TEST-024` | `REQ-016` | **PASS** | `ProfileFilterTests` + M-003/M-004 search/filter checks. |
| `TEST-025` | `REQ-017` | **DEFERRED** | Scheduled automatic backup deferred post-MVP (D2). |
| `TEST-026` | `REQ-018` | **PASS** | `ResourcePressureEvaluatorTests` + `WindowsResourceMonitor` + non-modal warning banner in `MainWindow`. |
