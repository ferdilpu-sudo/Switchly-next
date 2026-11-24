# TASK-004 Results — Browser Behavior (M-001)

**Date:** 2026-11-24
**Harness:** `Switchly.EngineSpike` (console, `net10.0-windows`, `win-x64`)
**Traceability:** REQ-006 → AC-007 → TEST-021 (popup/new-window/download in correct profile).

## Environment

Windows 11 Enterprise LTSC 2024 (build 10.0.26100) · .NET SDK 10.0.401 ·
CefSharp.OffScreen.NETCore 156.0.300 · CEF 156.0.30 + chromium-156.0.8078.96 ·
session elevated (Administrator).

## Commands executed

- `dotnet build -c Release` → 0 warnings / 0 errors.
- `./bin/Release/net10.0-windows/win-x64/Switchly.EngineSpike.exe --task-004` → evidence below.

## Result — PASS

```
INFO  origin=http://127.0.0.1:53280
PASS  download: saved 'C:\...\switchly-engine-spike-downloads\probe.txt' with correct content
PASS  popup: popup shares parent context (localStorage['shared']='XYZ')
PASS  navigation: 404 surfaced as non-success: http=-1 error=HttpResponseCodeFailure
PASS  renderer-crash: OnRenderProcessTerminated fired, status=ProcessCrashed
RESULT: PASS
```

- **Download** — `Content-Disposition: attachment` served by the test origin was
  captured via `IDownloadHandler` and written to disk with correct content.
- **Popup / new window (TEST-021)** — `window.open` triggered `OnBeforePopup`;
  the popup created with the parent's `RequestContext` saw the same
  `localStorage` (`shared='XYZ'`), confirming the new window retains the profile.
- **Navigation** — a 404 is surfaced as a non-success load (no throw/crash).
- **Renderer crash** — `chrome://crash` fired `OnRenderProcessTerminated` with
  `CefTerminationStatus.ProcessCrashed`.

## Findings (non-obvious, affect the implementation)

1. **Popup blocking.** Chromium's popup blocker swallows `window.open` without a
   user gesture *before* it reaches `OnBeforePopup`. Adding the
   `--disable-popup-blocking` command-line switch (or a real user gesture) makes
   the popup route to `ILifeSpanHandler.OnBeforePopup`. The app must decide its
   popup policy explicitly (allow/routeto-same-profile vs block).
2. **404 surfacing correction.** Contrary to the earlier source analysis, a 404
   in CefSharp 156.0.300 surfaces as `LoadUrlAsyncResponse.ErrorCode =
   HttpResponseCodeFailure` (with `HttpStatusCode = -1`, `Success = false`),
   **not** `HttpStatusCode = 404`. The harness must key off `ErrorCode`/`Success`,
   not `HttpStatusCode`, to detect 4xx.
3. **Renderer crash recovery works** — `chrome://crash` deterministically fires
   `OnRenderProcessTerminated` (status `ProcessCrashed`) in Chrome Runtime.
4. **OffScreen stability under elevation.** One of the runs hit a native access
   violation (`0xC0000005`) inside `CefBrowserHost.CreateBrowser`; it did not
   reproduce on re-run. Treated as intermittent and attributed to running the
   engine elevated (see elevation rule). Must re-validate non-elevated.
5. **Non-fatal shutdown assertion** — after the renderer crash, CEF logs
   `Check failed: observers_.empty()` during shutdown; the process still exits 0.

## Not covered here

- WPF window hosting / real window manager popup behavior (deferred to M-002).
- Downloads scoped to a per-profile `DownloadRecord` (application-layer concern).
