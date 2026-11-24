# Specification-Readiness Review — Switchly Next (Direct, non-EGM)

**Date:** 2026-11-24
**Surface:** Claude Code CLI (not Antigravity)
**Reviewer:** Claude (direct parent-context review — **not** an EGM Goldfish review)
**Scope:** All 9 canonical documents + `AGENTS.md` + `GEMINI.md`, against the current document state.
**Task:** Specification-readiness review only. No application source code was created.

---

## 1. Review method and honesty statement

This review was performed directly in the Claude Code parent context. The EGM
review workflow could **not** be invoked in this surface:

- The Antigravity `agy` CLI is **not on PATH** (checked; `agy NOT FOUND`).
- The EGM skills (`egm-review`, `egm-brainstorm`, …) are **not** in this
  surface's discoverable skill list. The skills visible here are Claude Code
  skills (dataviz, artifact-design, code-review, …), none of which are EGM.
- The Antigravity `invoke_subagent` capability could **not** be verified here,
  because this is not an Antigravity session.

Per `AGENTS.md` §3, this is reported as **EGM workflow BLOCKED**, and this
report is **not** claimed to be an independent EGM Goldfish review. No fresh
reviewers were simulated in the parent context.

A **prior EGM review** exists under `docs/egm-artifacts/`
(`consolidated_review_report.md` and four supporting reports). That prior run
was authored in a different (Antigravity) session and reported `invoke_subagent`
as available. I did not re-run it and cannot re-verify its claims here. The
documents have since been updated, so this review re-checks the current state.

---

## 2. Documents and EGM materials actually read

Canonical documents read in full:
1. `GEMINI.md`
2. `AGENTS.md` (also loaded as project instructions)
3. `README.md`
4. `prd.md`
5. `plan.md`
6. `architecture.md`
7. `schema.md`
8. `design.md`
9. `rules.md`
10. `testing.md`
11. `decisions.md`

Supporting materials read:
12. `docs/EGM-ANTIGRAVITY-SETUP.md`
13. `docs/EGM-WORKFLOW-MAP.md`
14. `.agents/plugins/egm/plugin.json` (EGM plugin manifest)

Prior EGM artifacts read (for state continuity):
15. `docs/egm-artifacts/consolidated_review_report.md`
16. `docs/egm-artifacts/comprehension_report.md`
17. `docs/egm-artifacts/design_critic_report.md`
18. `docs/egm-artifacts/implementation_readiness_report.md`
19. `docs/egm-artifacts/structural_report.md`

**EGM skills actually invoked: none.** EGM plugin files verified present on
disk (`.agents/plugins/egm/` with `plugin.json`, `skills/`, `agents/`,
`resources/`, `docs/`), but plugin discovery/enablement in Antigravity could not
be confirmed from this surface.

---

## 3. Confirmed product decisions (consistent across documents)

| Decision | Source | Status |
|---|---|---|
| WebView2 excluded; no fallback | `AGENTS.md` §1, `decisions.md` ADR-003 | Confirmed (product owner) |
| C# + WPF/MVVM + standard CEF via CefSharp | `decisions.md` ADR-001/003 | Proposed, gated by M-001 |
| Standard CEF runtime (not CEF Chrome Runtime) | `decisions.md` ADR-003 | Proposed |
| No source cookie/session import | `decisions.md` ADR-010, `prd.md` REQ-009 | Confirmed (user selection) |
| Basic use without registration; Pro login only for upgrade/activation | `decisions.md` ADR-009, `prd.md` REQ-012 | Confirmed |
| Extensions optional, deferred, not MVP gate | `AGENTS.md` §1, `prd.md` §4.2, `decisions.md` OPEN-003 | Confirmed scope |
| .NET 10 LTS | `decisions.md` ADR-002 | Proposed |
| SQLite (app metadata) + CEF owns browser storage | `decisions.md` ADR-004 | Proposed |
| DPAPI CurrentUser local wrapping + optional master password/Hello | `decisions.md` ADR-005 | Proposed |
| AES-256-GCM passphrase-derived portable backup | `decisions.md` ADR-006 | Proposed |
| Local structured logs, no telemetry until opt-in | `decisions.md` ADR-007 | Proposed |
| Windows 11 primary target; Windows 10 best-effort pending policy | `prd.md` REQ-014, `decisions.md` ADR-002/OPEN-002 | Confirmed primary; Win10 open |
| Priority provider URLs | `decisions.md` §12 (2026-11-24) | Confirmed |
| M-001 harness = console app + `CefSharp.OffScreen.NETCore` at `artifacts/engine-spike/` | `decisions.md` OPEN-008, `plan.md` TASK-002 | Confirmed (2026-11-24) |

Priority provider URLs (confirmed): Google Flow `https://flow.google`,
ChatGPT `https://chatgpt.com`, Dola `https://dola.com`, SeaArt `https://seaart.ai`.

---

## 4. Resolved since the prior EGM review

The prior EGM review (2026-11-24) reported 3 blockers. All three are now
resolved in the current documents:

1. **"Google Flow" / "Dola" ambiguity** → resolved. Exact URLs now recorded in
   `decisions.md` §12, `prd.md` §3, `architecture.md` §1, `testing.md` §4.
2. **M-001 test harness type** → resolved. `decisions.md` OPEN-008 +
   `plan.md` TASK-002 specify a console app (`Switchly.EngineSpike`) using
   `CefSharp.OffScreen.NETCore` under `artifacts/engine-spike/`.

Also fixed (verified in current files): duplicate provider paragraph in
`prd.md` §3, duplicate paragraph in `architecture.md` §1, duplicate README
"§9" numbering, and the CEF 145.0 → 156.0 doc-URL mismatch in
`architecture.md` §5.1.

---

## 5. Remaining open decisions and discrepancies

### 5.1 Material — M-001 scoping contradiction (WPF hosting)

`architecture.md` §12 states M-001 "must test … CEF Standard CEF runtime
startup **and WPF window hosting**", and `testing.md` §4's exit language requires
"acceptable integrated UI behavior." But the confirmed harness (OPEN-008 /
`plan.md` TASK-002) is a **console + OffScreen** PoC, which cannot exercise WPF
hosting. WPF hosting is the M-002 shell concern. **The two scopes disagree and
should be reconciled** (either drop "WPF window hosting" from M-001, or add a
minimal WPF-host spike to M-001).

### 5.2 Material — provider compatibility has no REQ/AC/TEST traceability

Priority-provider compatibility is a M-001 must-have (plan.md TASK-006: "If any
must-have fails, stop") but has **no REQ, no AC, and no TEST ID**. It exists only
in prose (`prd.md` §3, `plan.md` TASK-003, `testing.md` §4 PoC check 5).

### 5.3 Material — M-001 exit criteria incomplete

`plan.md` M-001 exit criteria = "TEST-003, 004, 006, 016, 017". Missing:
- provider compatibility (TASK-003), which is a must-have;
- browser behavior TEST-021 (used by TASK-003 and TASK-004);
- "packaging" — named as a TASK-006 go/no-go dimension but no M-001 task covers
  it (ADR-008 defers installer PoC to "M-001 or M-002").

### 5.4 Material — no provider acceptance threshold / fallback

If one or all four priority providers reject the embedded browser, no minimum
acceptance threshold or product pivot is defined. TASK-006 says "stop and
present alternatives" but does not say which provider failures are
must-have vs. acceptable.

### 5.5 Should-resolve — user-agent strategy

No decision on default CEF user-agent vs. a customized UA. Directly affects
provider acceptance (M-001) and has no ADR/OPEN entry.

### 5.6 Should-resolve — smaller M-001 items

- **Synthetic local test origin** (TEST-003/004, TASK-002) is undefined.
- **Profile locking mechanism** (mutex vs lock file) is left open
  (`architecture.md` §5.2, TEST-017).
- **Test framework** xUnit vs NUnit ("choose at scaffold") — minor, not an
  M-001 blocker (the PoC is a console app).
- **TASK-003 vs TASK-004** overlap on downloads/popups.

### 5.7 Deferred to M-007 (correctly not M-001 blockers)

- Vault key hierarchy (per-app/per-profile/per-credential)
- KDF algorithm (PBKDF2 / Argon2id / scrypt) and parameters
- Archive format / encryption envelope
- DPAPI key-loss recovery policy
- Backup consistency during active browsing

These are genuinely open and **not tracked as discrete entries** in
`decisions.md`'s open-decision list (only backup schedule/retention is, via
OPEN-006). Recommend adding explicit OPEN entries so M-007 doesn't silently
inherit them.

### 5.8 Minor consistency notes

- `decisions.md` OPEN-008 is listed in the "Open decisions" table but marked
  "Confirmed 2026-11-24" — it should move out of the open list or be relabeled.
- REQ-007 sits in §4.2 (Should) while §4.1 jumps REQ-006 → REQ-008 (intentional,
  but a light readability hazard).
- `schema.md` ENT-006 `HistoryEntry` mixes an aggregate signal (`VisitCount >= 1`)
  with a per-visit index (`VisitedAtUtc DESC`) — data-model ambiguity (M-004).
- `ENT-001 PersistSessionCookies` has no default; the "product privacy review"
  that selects it is not yet recorded as a decision (affects AC-004).
- Mixed Indonesian/English across product vs engineering docs; no stated
  language policy (product/UX docs are Indonesian, engineering docs English).

---

## 6. M-001 readiness verdict

**READY TO BEGIN, with a short must-decide list.** The prior three blockers are
resolved. No application code exists (verified: spec-only tree, no `.sln`, no
`src/`). Note: `artifacts/engine-spike/engine-version-report.md` shows **TASK-001
is already complete** (2026-11-24), so `plan.md` §6 ("all milestones Not
started") is stale — see §8.

- **Can start immediately:** TASK-001 (CefSharp/CEF version research) — no blockers.
- **Can start once §5.1 is acknowledged:** TASK-002 (isolated contexts, OffScreen).
- **Must decide before TASK-006 (go/no-go):** §5.2–§5.4 (provider traceability,
  exit criteria, acceptance threshold).

### Recommend resolving before/early in M-001
1. Reconcile WPF-hosting scope (architecture.md §12 + testing.md §4 vs OffScreen harness).
2. Add a formal REQ/AC/TEST chain for priority-provider compatibility.
3. Add provider compatibility + TEST-021 (and packaging, or drop it) to the M-001 exit criteria.
4. Define the provider acceptance threshold / fallback.
5. Decide the user-agent strategy (default CEF vs. customized).

---

## 7. Commands / tests actually executed (or not)

| Item | Result |
|---|---|
| `agy plugin list` / `agy --version` | **NOT RUN (unavailable)** — `agy` not found on PATH |
| `dotnet build` | NOT RUN — no solution/projects exist |
| `dotnet test` | NOT RUN — no solution/projects exist |
| `dotnet format --verify-no-changes` | NOT RUN — no solution exists |
| CEF/CefSharp browser or provider tests | NOT RUN — no code exists |
| Performance/resource measurements | NOT RUN — no code exists |
| EGM review workflow | **BLOCKED** — not available in this surface |
| `invoke_subagent` (Antigravity) | **Not verifiable** — not an Antigravity session |

No PASS claims are made for any command or test. No canonical document was
modified; this artifact is the only file written.

---

## 8. Post-review discovery — TASK-001 report and Chrome Runtime

After writing the initial version of this review, I inspected the working tree
for the git setup and found `artifacts/engine-spike/engine-version-report.md`
(dated 2026-11-24) — the **TASK-001 deliverable**, marked "TASK-001 is COMPLETE."

Consequences:
- `plan.md` §6 ("all milestones Not started") is stale; TASK-001 research is done.
- The report recommends pinning **CefSharp 156.0.300 / CEF 156.0.30 +
  chromium-156.0.8078.96**, TFM `net10.0-windows`, RID `win-x64`, using the
  `chromiumembeddedframework.runtime.win-x64` NuGet package. This supersedes the
  `CefSharp.Wpf.NETCore 156.0.150` named in `architecture.md` §2.

**Material conflict (not in the earlier sections of this review):**
The report states CEF 156 **removed the Alloy bootstrap** (Alloy deprecated in
CEF v126, removed in v128), so **Chrome Runtime is now the only mode** —
`CefSettings.ChromeRuntime = false` no longer exists and CefSharp 156 uses
Chrome Runtime exclusively. This contradicts the still-current wording in:

- `AGENTS.md` §1 — "standard CEF through CefSharp"
- `AGENTS.md` §5 — "do not add Chrome Runtime complexity"
- `decisions.md` ADR-003 — "Use the standard CEF runtime; do not make CEF Chrome Runtime … a requirement" (also the ADR index row and options list)
- `architecture.md` §2 / §12 — "standard CEF runtime"
- `prd.md` §8 — "Chrome Runtime khusus tidak menjadi kebutuhan" ("Chrome Runtime specifically is not needed")
- `README.md` §10 — "Do not require CEF Chrome Runtime or promise Chrome Web Store parity"

The "standard CEF vs Chrome Runtime" distinction is obsolete. The project's
underlying intent remains valid and unchanged: (a) no Chrome Web Store extension
parity requirement, and (b) extension APIs stay optional/deferred. Only the
runtime-mode language needs correcting. Per the report this does **not** block
M-001.

**Caution:** the Alloy-removal claim comes from the TASK-001 research artifact
and has **not been independently re-verified** in this session. Treat it as a
TASK-001 finding to confirm against the CefSharp 156 release notes before the
canonical documents are rewritten.

---

## 9. Proposed canonical-document corrections (pending product-owner approval)

Exact wording changes proposed to reconcile the docs with the TASK-001 finding.
No canonical document has been modified; this is for review.

### 9.1 `decisions.md` ADR-003 "Decision" paragraph

**Current:**
> Decision: Use C# + WPF + CefSharp/CEF as the current implementation proposal. Use the standard CEF runtime; do not make CEF Chrome Runtime or Chrome Web Store parity a requirement. Validate the chosen CefSharp/CEF version and four priority websites in M-001.

**Proposed:**
> Decision: Use C# + WPF + CefSharp/CEF as the current implementation proposal. CEF 156+ ships **Chrome Runtime exclusively** (the Alloy bootstrap was removed in CEF 128), so Chrome Runtime is the baseline runtime rather than an option to avoid. The earlier "standard CEF vs Chrome Runtime" distinction is obsolete. What remains deferred and optional is the use of Chrome Runtime's **extension APIs** and any Chrome Web Store parity — neither is an MVP requirement. Validate the chosen CefSharp/CEF version and four priority websites in M-001.

### 9.2 `AGENTS.md` §1 baseline sentence

**Current:** "…C# + WPF/MVVM + .NET 10 candidate + standard CEF through CefSharp + SQLite/EF Core…"

**Proposed:** "…C# + WPF/MVVM + .NET 10 candidate + CEF (Chrome Runtime) through CefSharp + SQLite/EF Core…"

### 9.3 `AGENTS.md` §5 implementation rule

**Current:** "Do not implement extensions as a blocker or add Chrome Runtime complexity unless the product owner reopens REQ-007."

**Proposed:** "Do not implement extensions as a blocker or add Chrome Runtime **extension API** complexity unless the product owner reopens REQ-007."

### 9.4 `architecture.md` §2 embedded-browser row (tradeoff/condition)

**Current:** "extensions are optional and may have limited compatibility"

**Proposed:** "Chrome Runtime is the only mode in CEF 156+ (Alloy removed); extensions are optional and may have limited compatibility"

### 9.5 `architecture.md` §12 bullet

**Current:** "CEF Standard CEF runtime startup and WPF window hosting."

**Proposed:** "CEF (Chrome Runtime) startup and WPF window hosting."

### 9.6 Version pin (`architecture.md` §2)

**Current:** `CefSharp.Wpf.NETCore 156.0.150` (`architecture.md` §2 only — the
version string does not appear in `README.md` or `decisions.md`)

**Proposed:** update to **156.0.300** per the TASK-001 pin, with CEF/Chromium
`156.0.30+g6028375+chromium-156.0.8078.96`.

### 9.7 `prd.md` §8 (assumptions table, "CEF runtime" row)

**Current:** "Gunakan runtime CEF standar melalui CefSharp; Chrome Runtime khusus tidak menjadi kebutuhan karena ekstensi bukan prioritas"

**Proposed (Indonesian — owner to confirm wording):** "Gunakan CEF (Chrome Runtime, satu-satunya mode sejak CEF 128) melalui CefSharp; penggunaan **API ekstensi** Chrome Runtime bukan prioritas karena ekstensi opsional"

### 9.8 `README.md` §10 (known limitations)

**Current:** "Do not require CEF Chrome Runtime or promise Chrome Web Store parity."

**Proposed:** "Chrome Runtime is the only CEF mode from CEF 156+; do not promise Chrome Web Store extension parity."
