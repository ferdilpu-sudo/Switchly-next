# Testing and Quality Plan — Switchly Next

**Status:** ACTIVE 0.3 — M-001..M-004, M-006..M-008, D6, and D7 verified on Windows 11 (see `docs/compatibility-and-qa-matrix.md` and `artifacts/engine-spike/`); Win10 physical validation (`OPEN-002`) and M-009 release checks pending  
**Evidence policy:** no PASS claim without actual run output/evidence.

## 1. Test strategy

- **Unit:** domain policies, URL normalization, group classification, validation, backup manifest and import parsers.
- **Application tests:** use cases with fake repositories/browser ports.
- **Infrastructure tests:** SQLite migrations, file-system operations, DPAPI adapter, archive encryption, import adapters using synthetic browser fixtures.
- **Browser integration:** real CefSharp/CEF contexts, cookies, localStorage, downloads, popups, media/WebGL where relevant, renderer failures.
- **E2E/manual:** profile creation, account switching, imports, backup/restore, Windows Hello and installer.
- **Performance:** repeatable RAM/CPU/startup/restore test scenarios across target devices.
- **Security:** profile isolation, path traversal, malformed archives, secret redaction, password CSV handling, browser-to-app trust boundaries.

## 2. Environment matrix

| Target | Environment | Purpose | Status |
|---|---|---|---|
| Windows 11 x64 | Current supported Windows 11 release | Primary release target | Verified (M-001..M-008, D6, D7) |
| Windows 10 22H2 Home/Pro | Clean consumer test VM/device | Best-effort compatibility; vendor support caveat | Planned / policy open |
| Windows 10 Enterprise/LTSC | Supported .NET target edition | Compatibility validation | Planned |
| Low-resource device | Agreed baseline RAM/CPU/disk | Resource behavior | Device specs open |
| Mid-range device | Agreed baseline RAM/CPU/disk | Multi-profile behavior | Device specs open |
| Two Windows devices | Separate local users/devices | Portable backup and restore | Planned |

Test data must be synthetic. Never use a user's real passwords, private browsing history or production account dataset in automated tests.

## 3. Traceable test cases

| Test ID | Requirement | Acceptance | Level | Steps / input | Expected result |
|---|---|---|---|---|---|
| TEST-001 | REQ-001, REQ-014 | AC-001, AC-014 | Install/E2E | Install and launch on each supported OS matrix | App starts; unsupported OS state is documented; no false compatibility claim |
| TEST-002 | REQ-002 | AC-002 | Unit/Integration | Create/edit/duplicate-config/archive/delete profile | Metadata and generated directory remain consistent; duplicate config does not copy cookies/session storage |
| TEST-003 | REQ-003 | AC-003 | Browser integration | Login to synthetic local test site in Profile A; open Profile B; inspect cookies/storage | Profile B cannot read A's session or local storage |
| TEST-004 | REQ-003 | AC-004 | Browser integration | Set session/localStorage, close app, restart profile | Persistent storage behaves according to configured cookie/session policy |
| TEST-005 | REQ-004 | AC-005 | Unit/UI | Create manual groups; visit test domains; refresh auto groups | Membership displays correctly; auto group source remains read-only |
| TEST-006 | REQ-005 | AC-006 | E2E | Open multiple profiles; add multiple windows/tabs; close one | Other profiles/windows remain active and correctly isolated |
| TEST-007 | REQ-006 | AC-007 | Integration/E2E | Add bookmark, visit pages, download a fixture, close and reopen | Data is recorded and visible only under correct profile |
| TEST-008 | REQ-007 | AC-008 | Optional browser integration | If optional extension support is implemented, load one explicitly approved compatible extension and test permission/error reporting | Unsupported API is reported honestly; this test is not an MVP release gate |
| TEST-009 | REQ-008, REQ-009 | AC-009 | Import integration | Import synthetic Chrome/Edge bookmark/history/tab fixtures | Preview counts are correct; no cookies/session imported |
| TEST-010 | REQ-008, REQ-010 | AC-010 | *(Reopened & implemented per D6)* Security integration | Import synthetic exported password CSV into per-profile vault (`ENT-009`) | Credentials stored encrypted per profile (unique-nonce AES-256-GCM + AAD wrapped by DPAPI); no plaintext in DB/log/temp output |
| TEST-011 | REQ-010 | AC-019 | Security/manual | Lock app; attempt unlock using Windows account/master password/Hello if present | Only configured unlock methods are accepted; errors do not leak secrets |
| TEST-012 | REQ-011 | AC-011 | Integration/E2E | Create manual chunked AEAD backup; restore via restart-to-restore swap on second Windows device with correct passphrase | Profiles and metadata restored cleanly; device-bound Chromium cookies excluded (user signs in again); archive authenticated |
| TEST-013 | REQ-011 | AC-011 | Security | Use wrong passphrase, corrupt bytes, archive path traversal and truncated archive | Restore rejects archive; existing profile data stays unchanged |
| TEST-014 | REQ-012 | AC-012 | E2E | Fresh install; do not register or sign in; create profile and browse | Basic use is available offline after install |
| TEST-015 | REQ-013 | AC-013 | Unit/UI | Inspect feature gate behavior with no paid entitlement backend | No undocumented feature restrictions appear |
| TEST-016 | REQ-005 | AC-006 | Performance | Open 1, 3, 5 and 10 profiles progressively on baseline hardware | Metrics recorded; UI remains responsive or shows resource warning without data loss |
| TEST-017 | REQ-003 | AC-003 | Concurrency | Open same profile from two app processes | Second process is blocked or directed to existing instance; cache is not corrupted |
| TEST-018 | REQ-008 | AC-009 | Import error | Source browser open/DB locked, malformed history DB, unsupported version | User asked to close source browser; partial failure report is accurate |
| TEST-019 | REQ-007 | AC-008 | Optional security integration | If extension support exists, test permission request and an unavailable API | Unsupported capability fails explicitly; optional feature cannot weaken profile isolation |
| TEST-020 | REQ-011 | AC-011 | Reliability | Cancel backup/restore at supported checkpoints; simulate disk full | Temporary data cleaned best-effort; original data remains valid |
| TEST-021 | REQ-006 | AC-007 | Browser integration | Popup/new-window/download in correct profile | New windows retain originating profile context |
| TEST-022 | REQ-010 | AC-019 | Security | Search logs, crash report fixtures and diagnostics after security/import/backup operations | No secrets, passwords, query strings, or cookie values appear |
| TEST-023 | REQ-015 | AC-015 | UI/E2E | Mark profile as favorite and change icon/color/startup URL | Favorite, appearance, and startup URL persist without changing profile identity or storage path |
| TEST-024 | REQ-016 | AC-016 | Unit/UI | Search by name/domain and combine supported filters across profiles/groups (M-003) and tabs/bookmarks (M-004) | Only matching items appear; no underlying membership or ordering is altered |
| TEST-025 | REQ-017 | AC-017 | *(Deferred post-MVP — D2)* Unit/Integration | If scheduled backup is later enabled, change schedule and retention, then trigger scheduled run | Valid preferences persist and apply to the next scheduler cycle |
| TEST-026 | REQ-018 | AC-018 | UI/Performance | Create active/inactive profiles and simulate measured resource pressure | Counts/status are accurate and warning appears without closing tabs or losing data |

## 4. Browser engine proof-of-concept — release blocker

M-001 must create a small throwaway test harness before building the full UI.

### Required PoC checks
1. Create two CEF request contexts with unique persistent cache paths.
2. Navigate both to a local synthetic test origin.
3. Write distinct cookie and localStorage values per context.
4. Verify no cross-read and persistence after restart.
5. Test Google Flow (https://flow.google), ChatGPT (https://chatgpt.com), Dola (https://dola.com), and SeaArt (https://seaart.ai) normal navigation and supported authentication behavior.
6. Test downloads, uploads, popups/new windows, clipboard, media playback, and WebGL where relevant.
7. Test renderer crash recovery, browser shutdown, and process cleanup.
8. Measure RAM/CPU for the shell, one active profile, and multiple active profiles.
9. Test on Windows 11 and the target Windows 10 editions.
10. Record exact CefSharp/CEF/Chromium versions and test artifacts.

Do not attempt to bypass provider restrictions or embedded-user-agent blocks.

**Exit:** PoC must prove complete profile isolation, basic browser stability, acceptable integrated UI behavior, and no critical blocker for priority website workflows. Extension support is informational only and does not gate MVP.

## 5. Performance protocol

Do not invent a fixed RAM-per-profile promise.

Record:
- Cold app startup time.
- Time to open a profile and first useful paint.
- Idle memory after opening/closing tabs.
- Incremental memory for each additional active profile.
- CPU usage at idle and under navigation.
- Time to suspend/close a profile.
- Time to restore N tabs.
- Peak disk use per profile.
- App responsiveness under memory pressure.

Test matrix: 1, 3, 5, 10 concurrently active profiles. Higher counts only if baseline results justify it. Saved-but-closed profiles should not spawn browser renderers.

## 6. Security test scenarios

- Profile A cookies/localStorage/IndexedDB cannot be accessed by Profile B.
- App crash during profile open does not cause context path reuse or cross-profile sharing.
- A malicious archive cannot write outside the staging directory.
- If extension support is later implemented, its source and requested permissions are shown before install.
- Credential import rejects invalid CSV and does not log row values.
- Wrong backup passphrase never produces partially restored live data.
- DPAPI key unavailable under another Windows account must fail closed.
- Untrusted website content cannot call privileged app commands without a narrow validated bridge.
- URL schemes such as `javascript:` and `file:` are rejected from imported tab/bookmark restore.

## 7. Tooling and commands

Planned candidate toolchain:
- .NET SDK 10.x.
- Visual Studio 2026 or current supported Visual Studio with WPF/.NET desktop workload.
- CefSharp WPF package pinned: `CefSharp.Wpf.NETCore 156.0.300` (CEF `156.0.30` / Chromium `156.0.8078.96`).
- `xUnit` (`xunit 2.9.3`) chosen as test framework.
- Microsoft.Extensions.Logging.
- EF Core SQLite.

Commands below are **Direncanakan — verifikasi setelah scaffolding**:
```powershell
dotnet restore
dotnet build .\Switchly.sln -c Release
dotnet test .\Switchly.sln -c Release
dotnet format .\Switchly.sln --verify-no-changes
```

CEF integration tests require Windows and an installed/bundled CEF runtime; do not run them as if they were platform-independent unit tests.

## 8. CI gates

Every change:
- Build succeeds.
- Format/lint checks succeed.
- Relevant unit tests pass.
- Integration tests for affected data/browser contracts pass.
- Security-sensitive changes add negative tests.
- No secrets are added.
- Relevant docs and traceability are updated.

Before beta:
- Full browser compatibility matrix passes.
- Backup/restore cross-device passes.
- Installer/uninstaller/upgrade works.
- Supported Windows matrix is documented.
- Dependency and third-party license inventory is reviewed.
- Browser/provider compatibility limitations are published honestly; extension limitations are documented only if the optional feature is shipped.

## 9. Test reporting format

For each milestone/task, record:
- Date and OS/runtime/browser versions.
- TEST IDs executed.
- Exact command or manual steps.
- PASS / FAIL / BLOCKED / NOT RUN.
- Evidence path (logs/screenshots) with secrets removed.
- Known failures and retest status.
