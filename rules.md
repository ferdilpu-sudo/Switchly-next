# Development Rules — Switchly Next

**Status:** ACTIVE 0.2 — updated per PO decisions D1–D5 (2026-11-24)  
These rules are mandatory for all human and AI contributors.

## 1. Non-negotiable modularity

- **1 file = 1 primary responsibility.**
- No single WPF ViewModel may own profile CRUD, browser runtime creation, backup crypto, import parsing and UI state.
- Keep presentation, application use cases, domain rules, persistence, browser engine adapters and Windows security adapters separate.
- A source file with roughly 250–350 nonblank lines is a review trigger, not a hard failure. Split when it has multiple reasons to change.
- Do not create many meaningless one-line wrappers or a generic `Helpers.cs` containing unrelated utilities.

## 2. Dependency direction and ownership

Allowed dependency direction:
- `Switchly.App` → `Switchly.Application` and UI-facing contracts.
- `Switchly.Application` → `Switchly.Domain` and application ports.
- `Switchly.Infrastructure` → application ports/domain models.
- `Switchly.BrowserHost` → CEF/CefSharp APIs and application browser ports.
- `Switchly.Domain` must not reference WPF, CefSharp, SQLite, Windows UI APIs or infrastructure packages.

Rules:
- Views render and bind; ViewModels coordinate view state; use cases implement application actions.
- Repositories own database access.
- `Switchly.BrowserHost` is the sole owner of `CefSharp.Wpf.NETCore` calls and CEF lifecycle; `Switchly.App` must **not** directly reference `CefSharp.*` NuGet packages (D5).
- Align `Microsoft.Extensions.*` and EF Core NuGet packages to `10.x` LTS via `Directory.Packages.props` (D5).
- WindowsSecurity adapter owns DPAPI/Hello interop.
- Composition root wires implementations; avoid static service locators and ambient mutable singletons.

## 3. Browser profile isolation rules

- Every profile must resolve to a unique application-generated single-segment `<profile-guid>` directory directly under `RootCachePath` (`ADR-011`).
- Never share a `CefRequestContext` between different `ProfileId` values.
- Tabs/windows within one profile may share that profile's context.
- Never use global CEF context for a user profile.
- Do not reuse a path after profile deletion until CEF confirms all browser/context handles are closed (use a startup deferred-delete tombstone queue if Windows retains a Chromium handle).
- Enforce an **app-level single-instance guard** (named mutex + IPC focus handoff) before `Cef.Initialize()` (required by Chrome Runtime's `RootCachePath` `ProcessSingleton`), paired with in-process per-profile state locks.
- Profile duplication copies profile settings only by default; copying browser data requires a separate explicit, confirmed action.
- Never copy source browser cookies/session in import.
- Any isolation regression is a release blocker, not a warning.

## 4. Security and secrets

- Never log passwords, cookies, access tokens, auth headers, CSV row contents, backup passphrases, DPAPI blobs, private browsing data or URL query strings.
- No secrets in source, appsettings files, test snapshots, sample data or crash messages.
- No website passwords, cookies, or OAuth tokens are stored in `switchly-next.db` in MVP (custom CSV password vault `ENT-009` is deferred post-MVP per D1). If reopened later, credentials must be stored only as single-payload authenticated ciphertext with unique nonces and AAD binding.
- Do not store master password, backup passphrase, encryption key or recovery key in SQLite.
- Windows DPAPI `CurrentUser` is for local protection; portable backup must use independent passphrase-based **chunked streaming AES-256-GCM** encryption (`ADR-006`) and exclude CEF global state, disposable caches, and device-bound cookies on cross-device restore (D3).
- Validate archive paths against traversal and symlink escapes before extraction.
- Extension management is not a core MVP feature. If later implemented, never install an extension without showing source and permissions and receiving user consent.
- Validate extension manifests and file paths; do not auto-run scripts from imported files.
- Do not attempt to defeat website access controls, MFA, CAPTCHA or rate limits.
- Keep telemetry disabled unless an explicit opt-in policy and privacy notice are approved.

## 5. CEF/browser rules

- Pin CefSharp and CEF versions together; never auto-upgrade runtime without the full browser test suite.
- Keep all engine interactions behind `IBrowserRuntime`, `IBrowserProfileContext`, `IBrowserWindowHost` and other narrow adapters.
- Respect CEF thread-affinity requirements; marshal callbacks to the correct dispatcher.
- Handle `OnBeforeClose`, download callbacks, popup creation, renderer termination and shutdown sequencing.
- Treat all website HTML, titles, downloads, history URLs and extension metadata as untrusted.
- Do not use unsupported flags as a substitute for a documented feature without a decision record.
- Do not advertise Chrome Web Store compatibility unless a narrowly defined compatibility matrix passes; no general Chrome parity claim is permitted.
- Engine failure must not cause metadata corruption or silent profile storage sharing.
- Each profile's `RequestContextSettings.CachePath` must be a **direct child** of `CefSettings.RootCachePath`; deeper paths are silently rejected in Chrome Runtime.
- Run the browser non-elevated (`asInvoker`). CEF auto-de-elevation (exit code 38 / `ResultCode.NormalExitAutoDeElevated`) indicates an elevated launch; never ship a `do-not-de-elevate` workaround to production.

## 6. Import/export rules

- Source Chrome/Edge profile data is read-only.
- Ask the user to close source browser if SQLite/session files are locked; never terminate a browser process automatically.
- Import only categories selected in the preview screen.
- Password CSV import is deferred post-MVP (D1). If later reopened, show plaintext-file warnings before selection and commit, and never write CSV content to temp files or logs.
- Import must support partial success with per-category counts and redacted errors.
- Never infer cookies/session import from another selected category.

## 7. Storage and migration rules

- SQLite queries must use EF Core parameters or safe parameterization; no SQL concatenation from input.
- Migrations must be versioned and tested using a copy of the prior schema.
- The database is not the owner of CEF internal profile data.
- Database transactions must keep profile metadata and import state consistent.
- File-system actions that span DB changes use staging and compensating cleanup.
- Backup restore validates archive integrity into staging and promotes via **restart-to-restore directory swap** before CEF/SQLite open file handles, leaving current data untouched on failure (D3).

## 8. Error handling and diagnostics

- Use typed application errors for expected conditions: profile locked, runtime unavailable, unsupported extension, invalid import, invalid archive, wrong backup passphrase, insufficient disk space.
- User-facing errors should state what happened and the safe next action.
- Logs use structured event IDs and correlation IDs.
- Redact URL query/fragment, file paths, import row values, credentials and tokens.
- Avoid retry loops for non-idempotent import/restore actions.
- Shutdown operations must be cancellable where safe; never cancel halfway through an atomic file promotion.

## 9. UI behavior standards

- Every data screen includes loading, empty, success, failure and permission/unsupported states.
- Destructive actions require explicit confirmation and explain impact.
- Keyboard navigation and visible focus are required.
- No action may claim success until the service reports success.
- Profile color/icon is a usability label only, never an authorization or isolation boundary.
- Multi-window focus and ownership must be explicit.

## 10. Performance standards

- Lazy-create browser contexts when profiles are opened.
- Saved but inactive profiles must not have active renderers.
- Avoid polling each profile; use engine events and bounded diagnostics.
- Profile switching should focus existing windows rather than create duplicates.
- Measure startup time, idle memory, active profile memory, additional profile cost, tab restore time and shutdown time.
- Do not set universal memory limits without baseline measurements.
- Resource warnings must not discard unsaved page work.

## 11. Test and CI gates

Run the verified project commands listed in `README.md` once scaffolding exists. Planned pipeline:
1. `dotnet format --verify-no-changes`
2. `dotnet build`
3. unit tests
4. infrastructure/integration tests
5. Windows desktop smoke tests
6. CEF browser compatibility suite
7. dependency/license/security review

Until commands are verified against the actual solution, label them `Direncanakan — verifikasi setelah scaffolding`. Report all unrun checks as `NOT RUN`.

## 12. Dependency and license review

- Keep a `THIRD-PARTY-NOTICES` or equivalent inventory at release.
- Verify CEF/Chromium/CefSharp licensing and all transitive dependencies.
- Pin exact versions after M-001; review security advisories before updating.
- Never ship a binary dependency from an untrusted mirror.
- Record significant upgrades in `decisions.md`.

## 13. Code review checklist

- Does each changed file have one clear responsibility?
- Are dependencies flowing in the allowed direction?
- Does each profile get a unique persistent browser context?
- Are secrets redacted and encrypted?
- Are error/empty/loading states present?
- Does the feature map to REQ/AC/TEST?
- Are migrations and restore safety covered?
- Did the author run and report actual tests?
- Are docs updated when contracts or decisions change?
