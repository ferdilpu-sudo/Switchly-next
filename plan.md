# Implementation Plan — Switchly Next

**Status:** Not started — specification and discovery handoff only  
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
| M-001 | CEF engine, provider and OS proof-of-concept | None | CefSharp harness, profile isolation demo, priority-provider compatibility matrix, performance baseline | REQ-003, REQ-005, REQ-014 | TEST-003, 004, 006, 016, 017 pass or explicit architecture re-decision |
| M-002 | Desktop solution foundation | M-001 pass | WPF shell, MVVM, composition, SQLite migration baseline, app lifecycle | REQ-001, REQ-012 | Builds and launches; basic app works without login |
| M-003 | Profile manager and isolation | M-002 | CRUD, group model, favorites, search/filter, per-profile request context, multiple windows | REQ-002, REQ-003, REQ-004, REQ-005, REQ-015, REQ-016 | TEST-002 to 006, TEST-017, TEST-023, TEST-024 pass |
| M-004 | Browser essentials | M-003 | Tabs, navigation, bookmarks, history, downloads, session restore | REQ-005, REQ-006 | TEST-006, 007, 021 pass |
| M-005 | Optional extension support (deferred) | M-001, M-003 | Only a limited extension capability if later approved | REQ-007 | Informative optional gate; not required for MVP release |
| M-006 | Browser import | M-004 | Chrome/Edge discovery, preview, import adapters, CSV vault import | REQ-008, REQ-009, REQ-010 | TEST-009, 010, 018, 022 pass |
| M-007 | Security and backup/restore | M-003, M-006 | DPAPI/Hello unlock, encrypted archive, scheduler/retention settings, restore staging | REQ-010, REQ-011, REQ-017 | TEST-011 to 013, 020, 022, 025 pass |
| M-008 | Performance, stability, accessibility | M-004 to M-007 | Resource manager/indicator, UX hardening, recovery, high-DPI/keyboard QA | REQ-005, REQ-006, REQ-014, REQ-018 | TEST-016, TEST-026 and full acceptance suite pass |
| M-009 | Freemium and release readiness | M-008 + product decisions | License boundary, installer, signing, notices, support docs | REQ-012, REQ-013, REQ-014 | Product owner approves pricing/Free-Pro/OS policy and beta release gates |

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
- TASK-007: create solution/projects and WPF shell.
- TASK-008: establish MVVM, DI/composition root and logging.
- TASK-009: create SQLite context/migrations and health check.
- TASK-010: create CI build/test baseline.
- Exit: clean checkout builds and app launches.

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
- TASK-028: password CSV parser and encrypted vault writer.
- Exit: preview/partial failure/report behavior validated.

### M-007 — security and backups
- TASK-029: local vault key wrapping with DPAPI.
- TASK-030: master password and Windows Hello unlock UX.
- TASK-031: backup manifest and authenticated encryption.
- TASK-032: automatic backup scheduler.
- TASK-033: staged restore and rollback handling.
- TASK-033A: backup schedule/retention settings.
- Exit: cross-device restore and negative security tests pass; TEST-025 passes.

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
| M-001 to M-009 | Not started | No code or test execution has been performed |

## 7. Milestone definition of done

- All linked REQ/AC/TEST obligations for milestone are implemented or explicitly deferred with owner approval.
- `dotnet build`, relevant test suites, and platform smoke tests are actually run.
- Security-sensitive workflows have negative tests.
- No unhandled critical data-loss or profile-isolation defects.
- Docs are updated to match actual code.
- Report PASS/FAIL/BLOCKED/NOT RUN honestly.
- Product owner explicitly approves any change to scope, browser engine, OS support or business model.
