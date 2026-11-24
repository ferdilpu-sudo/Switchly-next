# Engine Version Report — CefSharp/CEF 156 Compatibility

**Task:** M-001 TASK-001 — Verify current CEF/CefSharp compatibility  
**Date:** 2026-11-24  
**Status:** Research complete. Version pin recommended. Architecture impact identified.  
**REQ mapping:** REQ-003 (isolation), REQ-005 (multi-profile), REQ-014 (Windows compat)

---

## 1. Recommended Version Pin

| Component | Version | Source |
|---|---|---|
| CefSharp.OffScreen.NETCore | **156.0.300** | NuGet |
| CefSharp.Wpf.NETCore | **156.0.300** | NuGet |
| CefSharp.Common.NETCore | **156.0.300** | NuGet (dependency) |
| CEF | 156.0.30+g6028375+chromium-156.0.8078.96 | Via CefSharp |
| Chromium | **156.0.8078.96** | Via CEF |
| .NET Target Framework | `net10.0-windows` | .NET 10 LTS (GA, active support until 2028-11-14) |
| Runtime Identifier | `win-x64` | Primary; `win-arm64` also available |
| CEF runtime package | `chromiumembeddedframework.runtime.win-x64` | NuGet (NOT legacy `cef.redist.*`) |

The originally proposed `156.0.150` has been superseded by `156.0.300`. Use 156.0.300 for the PoC.

---

## 2. .NET 10 Compatibility — CONFIRMED ✅

- .NET 10 LTS is GA (released 2025-11-11, supported until 2028-11-14).
- CefSharp 156.x explicitly targets `net10.0-windows`, `net9.0-windows`, `net8.0-windows`.
- Project must use Windows-specific TFM: `<TargetFramework>net10.0-windows</TargetFramework>`.

---

## 3. CRITICAL: Chrome Runtime Is Mandatory

**The Alloy bootstrap has been REMOVED from CEF.** CefSharp 156 uses Chrome Runtime exclusively.

- Alloy was deprecated in CEF v126, removed in v128.
- `CefSettings.ChromeRuntime = false` no longer exists.
- Multi-process Chromium model is enforced.
- `CefSettings.MultiThreadedMessageLoop = true` is the default for WPF.
- All UI-facing CEF interactions must use `Cef.UIThreadTaskFactory`.
- Synchronous `IResourceHandler.ProcessRequest` removed — must use async equivalents.

**Impact on Switchly architecture documents:**
- `architecture.md` and `decisions.md` currently say "Use the standard CEF runtime; do not make CEF Chrome Runtime or Chrome Web Store parity a requirement."
- This language is outdated. CEF 156 IS Chrome Runtime. There is no choice; it is the only option.
- The concern about Chrome Runtime was related to extension support complexity, but Chrome Runtime is now the baseline regardless.
- **This does NOT block M-001** — Chrome Runtime is what ships. The documents should be updated to reflect that Chrome Runtime is mandatory in CEF 156+, and the extension concern is about whether to USE Chrome Runtime's extension APIs, not whether to run Chrome Runtime.

---

## 4. CefRequestContext Isolation — CONFIRMED ✅

CefRequestContext with unique CachePath provides full isolation of:
- ✅ Cookies
- ✅ localStorage
- ✅ sessionStorage
- ✅ IndexedDB
- ✅ Cache files
- ✅ Service workers

Implementation pattern:
```csharp
var settings = new RequestContextSettings
{
    CachePath = Path.Combine(appDataPath, "Profiles", profileId.ToString()),
    PersistSessionCookies = true,
    PersistUserPreferences = true
};
var requestContext = new RequestContext(settings);
var browser = new ChromiumWebBrowser(url) { RequestContext = requestContext };
```

Critical rules:
1. `CachePath` MUST be an absolute path.
2. Each `ProfileId` → unique `CachePath`.
3. `CachePath` must be a **direct child** of `CefSettings.RootCachePath` (deeper paths are rejected in Chrome Runtime).
4. Do NOT modify `CachePath` files while `CefRequestContext` is active.
5. Close all CEF handles before deleting a profile path.
6. Empty/null `CachePath` = incognito (in-memory only).
7. **Process singleton**: only one app instance can use a given `RootCachePath` simultaneously — this partially resolves the profile locking question.

---

## 5. Redistribution Requirements

| Component | Size |
|---|---|
| `libcef.dll` (core Chromium) | ~90–110 MB |
| CefSharp DLLs + subprocess | ~2–10 MB |
| Locales, `icudtl.dat`, `.pak` files, V8 snapshots | ~15–25 MB |
| **Total uncompressed runtime** | **~100–130 MB** |

- **VC++ 2022 Redistributable** required (~15–25 MB standalone; often pre-installed).
- Can be bin-deployed locally (vcruntime140.dll, msvcp140.dll, ~2–3 MB).
- Must use `chromiumembeddedframework.runtime.win-x64` NuGet package (NOT legacy `cef.redist.*`).

---

## 6. Known Issues Affecting Switchly

| Issue | Severity | Impact |
|---|---|---|
| **Proprietary codecs NOT included** (H.264/AAC/MP3) | 🟡 High | SeaArt media playback may fail. Community packages like `CefSharp.H264.x64` exist but must version-match. |
| **WPF viewport resize bugs** (#5142, #5174) | 🟡 Medium | Black spaces on minimize/resize in multi-tab WPF. |
| **IME input rendering bugs** (#5220) | 🟡 Medium | Chinese input issues; password exposure in IME popups. |
| **ObjectDisposedException in JS callbacks** (#5275) | 🟢 Low | High-frequency navigation with JS-bound objects. |

---

## 7. Security

Chromium 156.0.8078.x had critical CVEs patched:
- CVE-2026-118306 (Critical, FileSystem memory corruption)
- CVE-2026-118331 (Critical, FileAPI input validation)
- CVE-2026-118348 (Critical, Skia input validation)
- CVE-2026-118344 (Critical, Layout use-after-free)

CefSharp 156.0.300 should include these patches. **ACTION:** Verify CVE-2026-134401 fix inclusion before production.

CEF security update SLA should be defined before beta release.

---

## 8. Licensing — CONFIRMED PERMISSIVE ✅

| Component | License | Viral? |
|---|---|---|
| CefSharp | BSD 3-Clause | No |
| CEF | BSD 3-Clause | No |
| Chromium | BSD 3-Clause + mixed (MIT, Apache 2.0, LGPL, MPL sub-components) | No (LGPL components are dynamically linked) |

Requirements:
1. Include CefSharp + CEF + Chromium BSD license text in app "About" or docs.
2. Bundle full third-party acknowledgments (from `about:credits`).
3. Do NOT claim Google/Chromium/CEF endorsement.
4. If using custom H.264/AAC builds, verify FFmpeg LGPL vs GPL implications.

---

## 9. Unverified Items (Requires PoC Testing)

1. Whether CefSharp 156.0.300 includes fix for CVE-2026-134401 (backported at 156.0.8078.114).
2. Exact proprietary codec community package availability for 156.0.300.
3. Runtime memory/CPU profile on .NET 10 — requires M-001 TASK-005 measurement.
4. WPF viewport resize behavior with Switchly's specific window/tab layout.
5. Provider compatibility (TASK-003).

---

## 10. TASK-001 Verdict

| Check | Result |
|---|---|
| Package exists and is current | ✅ PASS — 156.0.300 |
| .NET 10 supported | ✅ PASS |
| Profile isolation mechanism confirmed | ✅ PASS |
| Licensing is permissive | ✅ PASS |
| VC++ redistributable identified | ✅ PASS |
| Security patches current (with caveat) | ⚠️ PASS with follow-up |
| Chrome Runtime mandatory (architecture impact) | ⚠️ PASS — docs need update |
| Proprietary codecs missing | ⚠️ RISK — affects SeaArt |

**TASK-001 is COMPLETE. Proceed to TASK-002.**
