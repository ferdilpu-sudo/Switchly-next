# TASK-002 Results — Profile Storage Isolation & Persistence (M-001)

**Date:** 2026-11-24
**Harness:** `Switchly.EngineSpike` (console, `net10.0-windows`, `win-x64`)
**Traceability:** REQ-003 → AC-003 (isolation) → TEST-003, TEST-017; REQ-003 → AC-004 (persistence) → TEST-004.

## Environment

| Item | Value |
|---|---|
| OS | Windows 11 Enterprise LTSC 2024 (build 10.0.26100) |
| .NET SDK | 10.0.401 |
| CefSharp.OffScreen.NETCore | 156.0.300 |
| CEF | 156.0.30 + chromium-156.0.8078.96 (`chromiumembeddedframework.runtime.win-x64` 156.0.30) |
| Session elevation | Elevated (Administrator) |

## Commands executed

- `dotnet build -c Release` → succeeded, 0 warnings / 0 errors.
- `./bin/Release/net10.0-windows/win-x64/Switchly.EngineSpike.exe` → evidence below.

## Result — PASS

```
INFO  test origin = http://127.0.0.1:51138
PASS  A: isolated + persisted. write=[{"ls":"A","cookie":"probe=A"}] reopen=[{"ls":"A","cookie":"probe=A"}]
PASS  B: isolated + persisted. write=[{"ls":"B","cookie":"probe=B"}] reopen=[{"ls":"B","cookie":"probe=B"}]
RESULT: PASS
```

- **TEST-003 (isolation):** Profile A's `localStorage`/cookie never appear in Profile B and vice-versa.
- **TEST-004 (persistence):** a fresh `RequestContext` on the same `CachePath` reloads the persisted value from disk.
- **TEST-017 (single-instance):** a second process attempting `Cef.Initialize` on the same `RootCachePath` is blocked with `Cef.Initialize=false` and exit code `NormalExitProcessNotified` (24).

```
INFO  holder initialized CEF; attempting a second Cef.Initialize on the same RootCachePath...
PASS  TEST-017: second process blocked by singleton lock. Cef.Initialize=false, exit code=24 (NormalExitProcessNotified).
```

## Findings (non-obvious, affect the architecture)

1. **Chrome Runtime requires `RequestContextSettings.CachePath` to be a *direct child* of `CefSettings.RootCachePath`.**
   A deeper path (e.g. `<root>/profiles/<guid>`) is rejected with
   `Cannot create profile at path` (`chrome_browser_context.cc:116`) and CEF
   silently falls back to an in-memory (off-the-record) profile — persistence
   silently fails. Direct-child paths (`<root>/<guid>`) create a real on-disk
   profile and persist.
   - **Implication for the docs:** the documented layout
     `%LOCALAPPDATA%\SwitchlyNext\Profiles\<guid>` is only valid if `RootCachePath`
     is set to `%LOCALAPPDATA%\SwitchlyNext\Profiles` (so `<guid>` is its direct
     child). This needs a doc/architecture decision (see `architecture.md` §5.1,
     `schema.md` §1, and TASK-001's "CachePath should be under RootCachePath"
     wording, which is too loose).

2. **Running elevated (Administrator) triggers CEF auto-de-elevation**
   (exit code 38 / `ResultCode.NormalExitAutoDeElevated`), which makes
   `Cef.Initialize()` return false. For this local PoC, adding
   `CefCommandLineArgs["do-not-de-elevate"] = "1"` disables the relaunch.
   Production must run non-elevated (`asInvoker`) — running browser content at
   high integrity is a security risk.

3. **Confirmed CefSharp 156 API facts** (from the v156 source): no
   `CefSettings.ChromeRuntime` property; no
   `RequestContextSettings.PersistUserPreferences`; the OffScreen settings type
   is `CefSharp.OffScreen.CefSettings`.

## Not covered here

- WPF window hosting — out of scope for the OffScreen PoC (deferred to M-002).
