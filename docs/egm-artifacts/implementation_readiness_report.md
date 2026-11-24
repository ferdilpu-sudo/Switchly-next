# Implementation Readiness Report — M-001 Assessment

**Reviewer:** egm-implementation-readiness (Goldfish — fresh context)  
**Scope:** Full specification corpus (9 documents), focused on M-001  
**Date:** 2026-11-24  
**Status:** Report delivered inline; subagent terminated with model error after completing review.

---

## 1. M-001 TASK-BY-TASK READINESS

### TASK-001 — Verify current CEF/CefSharp compatibility

**Provided:**
- Candidate package: `CefSharp.Wpf.NETCore 156.0.150` (architecture.md L38)
- Research scope: release notes, .NET targets, lifecycle/threading APIs, security advisories, known issues, redistribution requirements (plan.md L35-36)
- Output artifact path: `artifacts/engine-spike/engine-version-report.md` (plan.md L37)

**Missing:**
- 🟡 **Major** — No specific acceptance criteria for the version report itself. What constitutes a "pinned" version?
- 🟡 **Major** — No guidance on where to find or how to validate CefSharp's VC++ redistributable requirements.
- 🟢 **Nit** — No template or expected sections for `engine-version-report.md`.

**Verdict:** ✅ Mostly ready. Research task; implementer can start. Pinning mechanism slightly ambiguous.

---

### TASK-002 — Create isolated profile contexts

**Provided:**
- Create two `CefRequestContext` instances with unique persistent cache paths
- Verify cookie/localStorage separation and persistence
- Linked tests: TEST-003, TEST-004, TEST-017
- Profile storage path pattern: `%LOCALAPPDATA%\SwitchlyNext\Profiles\<profile-guid>`

**Missing:**
- 🔴 **Blocker** — "Minimal test harness" is undefined. Console app? WPF window? Test project? Project name? Location in repo?
- 🟡 **Major** — "Synthetic local test origin" is undefined. What technology? What test pages?
- 🟡 **Major** — TEST-017 locking mechanism not specified (mutex vs lock file).
- 🟢 **Nit** — No explicit guidance on CEF initialization settings.

**Verdict:** 🚫 Not ready without clarifying harness project type and synthetic test origin.

---

### TASK-003 — Validate priority website workflows

**Provided:**
- Target websites: Google Flow, ChatGPT, Dola, SeaArt
- Test scope: navigation, authentication, uploads, downloads, popups, clipboard, media, WebGL, recovery
- Output: provider compatibility matrix

**Missing:**
- 🟡 **Major** — "Google Flow" is ambiguous. No commonly recognized Google product by this name.
- 🟡 **Major** — No template for compatibility matrix.
- 🟡 **Major** — No definition of "supported authentication behavior" vs "restriction that must be respected."
- 🟡 **Major** — "Dola" is ambiguous — no URL provided.
- 🟢 **Nit** — No guidance on test accounts.

**Verdict:** 🚫 Not ready. "Google Flow" and "Dola" need exact URLs.

---

### TASK-004 — Validate browser behavior

**Provided:**
- Test scope: downloads, popups, new windows, renderer crash/shutdown, navigation callbacks
- Linked test: TEST-021

**Missing:**
- 🟡 **Major** — Significant overlap with TASK-003 scope.
- 🟡 **Major** — Renderer crash testing mechanism unspecified.
- 🟡 **Major** — "Navigation callbacks" scope is vague.
- 🟢 **Nit** — No defined output format.

**Verdict:** ⚠️ Partially ready. Scope overlap with TASK-003 will cause questions.

---

### TASK-005 — Measure resource behavior

**Provided:**
- Measure: startup, idle, one active, multiple active contexts
- Closed profiles must not keep renderers alive
- Performance protocol in testing.md §5

**Missing:**
- 🟡 **Major** — "Named hardware" is unspecified. Device specs marked "open."
- 🟡 **Major** — No specific measurement tooling specified.
- 🟢 **Nit** — No baseline report template.

**Verdict:** ⚠️ Partially ready. Can measure on available hardware, but baselines undefined.

---

### TASK-006 — Decide go/no-go

**Provided:**
- Summarize pass/fail against: isolation, site workflows, browser behavior, packaging, resources
- Extension capability is informational only

**Missing:**
- 🟡 **Major** — "Packaging" is a go/no-go criterion but no M-001 task covers packaging.
- 🟡 **Major** — No pass/fail thresholds for resource expectations (intentionally qualitative, but circular with go/no-go gate).
- 🟢 **Nit** — No decision document template.

**Verdict:** ⚠️ Partially ready. Packaging criterion has no backing task.

---

## 2. TECHNOLOGY SELECTION GAPS

| # | Decision Left to Implementer | Severity |
|---|---|---|
| 1 | 🔴 Test harness project type — Console app, WPF app, or test project? | Blocker |
| 2 | 🟡 Test framework — xUnit vs NUnit | Major |
| 3 | 🟡 Synthetic test origin setup | Major |
| 4 | 🟡 Profile locking mechanism — mutex vs lock file | Major |
| 5 | 🟡 CEF initialization settings | Major |
| 6 | 🟡 Performance measurement tooling | Major |
| 7 | 🟡 CefSharp package variant for PoC (WPF vs OffScreen) | Major |
| 8 | 🟢 Solution/project naming for PoC harness | Nit |

## 3. TEST INFRASTRUCTURE

1. 🔴 **Blocker** — No test harness project definition.
2. 🟡 **Major** — No synthetic test server/pages.
3. 🟡 **Major** — No guidance on automated vs manual test execution for M-001.
4. 🟡 **Major** — TEST-017 concurrent access requires launching two processes — no scripting guidance.
5. 🟢 **Nit** — Evidence/artifact storage path inconsistent across tasks.

## 4. EXIT CRITERIA CLARITY

| Criterion | Measurable? | Issue |
|---|---|---|
| TEST-003 (isolation) | ✅ Yes | — |
| TEST-004 (persistence) | ✅ Yes | — |
| TEST-006 (multi-profile) | ⚠️ Partially | Does PoC need multi-window WPF? |
| TEST-016 (performance) | ⚠️ Partially | No "responsive" threshold |
| TEST-017 (concurrent access) | ✅ Yes | — |
| Packaging | ⚠️ No task | Orphaned criterion |
| Provider compatibility | ⚠️ Not in exit formula | TASK-003 not listed in exit criteria |

**Key finding:** 🟡 **Major** — TASK-003 (provider compatibility) is described as critical but NOT listed in M-001's exit criteria test IDs.

## 5. TRACEABILITY COMPLETENESS

| Requirement | M-001 Coverage | Gap |
|---|---|---|
| REQ-003 (isolation) | ✅ Full | — |
| REQ-005 (multi-profile) | ⚠️ Partial | TEST-006 scope unclear for PoC |
| REQ-014 (Windows compat) | ⚠️ Partial | TEST-001 not in M-001 exit criteria |
| Priority providers | ❌ Missing | No REQ, AC, or TEST ID |

**Key finding:** 🟡 **Major** — Priority website compatibility has no REQ ID, no AC ID, and no TEST ID. Traceability chain is broken.

## 6. VERDICT

**Conditionally yes, but with 1 blocker and 16 majors.**

### Must resolve before starting:
1. 🔴 Define the PoC test harness project type, name, and location.

### Should resolve before starting:
2. 🟡 Clarify "Google Flow" and "Dola" — exact URLs or product names.
3. 🟡 Define or provide synthetic test origin.
4. 🟡 Choose test framework (xUnit vs NUnit).
5. 🟡 Decide profile locking mechanism.
6. 🟡 Add provider compatibility to M-001 exit criteria explicitly.
7. 🟡 Clarify TASK-003 vs TASK-004 scope boundary.
8. 🟡 Resolve whether "packaging" is a go/no-go gate.
9. 🟡 Add REQ/AC/TEST chain for priority provider compatibility.

### Total finding count:
- 🔴 Blockers: 1
- 🟡 Majors: 16
- 🟢 Nits: 7
