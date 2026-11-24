# TASK-005 Results — Resource Baseline (M-001)

**Date:** 2026-11-24
**Traceability:** REQ-005 → AC-006 → TEST-016; NFR-PERF-001/002/003/004.

## Device

| Item | Value |
|---|---|
| Model | HP Pavilion x360 Convertible 14-dy0xxx |
| CPU | 11th Gen Intel Core i7-1165G7 @ 2.80 GHz (4C/8T) |
| RAM | 7.8 GB |
| OS | Windows 11 Enterprise LTSC 2024 (build 10.0.26100) |
| Engine | CefSharp.OffScreen.NETCore 156.0.300 / CEF 156.0.30 + chromium-156.0.8078.96 (session elevated) |

## Commands

- `dotnet build -c Release` → 0 warnings / 0 errors.
- `./bin/Release/net10.0-windows/win-x64/Switchly.EngineSpike.exe --task-005` → below.

## Measurements

| Stage | CEF processes | Private (MB) | Working set (MB) |
|---|---|---|---|
| CEF startup (0 profiles) | 1 | 32 | 99 |
| 1 active profile | 6 | 220 | 421 |
| 3 active profiles | 8 | 335 | 648 |
| 5 active profiles | 11 | 423 | 801 |
| 10 active profiles | 17 | 654 | 1295 |
| all profiles closed | 5 | 396 | 529 |

- `Cef.Initialize` = **238 ms**.
- Total run = 12.5 s.

## Findings

1. **Incremental cost ≈ 45–55 MB private per additional active profile** (one
   renderer process each). 10 profiles ≈ **654 MB private / ~1.3 GB working set**
   on 7.8 GB RAM — feasible but tight on low-RAM hardware.
2. **NFR-PERF-001/004 confirmed:** closing all profiles dropped CEF processes
   **17 → 5** (12 renderer processes exited) and freed ~260 MB private. Closed
   profiles do not keep renderer processes alive. The remaining 5 processes
   (browser / GPU / utility) stay until `Cef.Shutdown`, and the browser/GPU
   processes retain their working set — expected.
3. **NFR-PERF-003:** no universal RAM cap is set from this single device; these
   numbers are a single low-RAM baseline, not a product promise.
4. **Caveat:** measured against a trivial local page via OffScreen; real web
   content (JS, media, WebGL) will be heavier — treat as a lower bound.

## Not covered here

- Time-to-first-useful-paint per profile, tab restore time, disk use per profile
  (measured later when the WPF shell exists — M-002/M-008).
