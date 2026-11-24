# Consolidated Specification-Readiness Review — Switchly Next

**Date:** 2026-11-24  
**Reviewer:** Coordinator (Elephant context) with EGM Goldfish reviewers  
**Scope:** All 9 canonical project documents + AGENTS.md + GEMINI.md  
**Task:** Specification-readiness review. No application source code created.

---

## Review Methodology

### Documents Read
1. `GEMINI.md` — workspace instructions
2. `AGENTS.md` — mandatory agent operating instructions
3. `README.md` — project orientation and current status
4. `prd.md` — product scope, REQ IDs and acceptance criteria
5. `plan.md` — milestone sequence and current progress
6. `architecture.md` — system boundaries and browser lifecycle
7. `schema.md` — authoritative data contracts
8. `design.md` — UI and interaction behavior
9. `rules.md` — engineering and security rules
10. `testing.md` — test strategy and evidence requirements
11. `decisions.md` — ADR status and unresolved decisions
12. `docs/EGM-ANTIGRAVITY-SETUP.md` — EGM configuration guide
13. `docs/EGM-WORKFLOW-MAP.md` — EGM workflow index
14. `.agents/plugins/egm/skills/egm-review/SKILL.md` — EGM review skill instructions
15. `.agents/plugins/egm/plugin.json` — EGM plugin manifest

### EGM Skills Invoked
- **egm-review** workflow (adapted for cross-document specification review)
- Goldfish reviewer subagents invoked via `invoke_subagent`:

| Reviewer | Subagent Type | Status | Report |
|---|---|---|---|
| Comprehension Auditor | `egm-comprehension` | ✅ Report delivered (subagent terminated after sending) | `comprehension_report.md` |
| Design Critic | `egm-design` | ✅ Report delivered (subagent terminated after sending) | `design_critic_report.md` |
| Implementation Readiness | `egm-implementation-readiness` | ✅ Report delivered (subagent terminated after sending) | `implementation_readiness_report.md` |
| Architecture Reviewer | `egm-zoom-out` | ❌ RESOURCE_EXHAUSTED (429 quota) | Not produced |
| Structural Validator | `egm-structural` | ❌ RESOURCE_EXHAUSTED (429 quota) | Not produced |

**Supplemental analyses:** Structural validation and zoom-out analysis performed in the Elephant (coordinator) context. These are **not** claimed as independent Goldfish reviews.

### Capability Verification
- **`invoke_subagent` available:** ✅ Yes — confirmed in Antigravity tool list and used successfully
- **EGM plugin on disk:** ✅ Yes — `.agents/plugins/egm/` with all required subdirectories
- **EGM skills discoverable:** ✅ Yes — all 8 skills present, reviewer personas under `agents/`, standards under `resources/`
- **Goldfish fresh-context reviews:** ⚠️ Partially successful — 3 of 5 subagents completed; 2 failed due to model quota exhaustion

---

## Confirmed Product Decisions

These decisions are explicitly recorded and consistent across documents:

| Decision | Source | Status |
|---|---|---|
| WebView2 excluded | AGENTS.md §1, decisions.md ADR-003 | Confirmed by product owner |
| CefSharp + standard CEF (not Chrome Runtime) | decisions.md ADR-003 | Proposed, gated by M-001 |
| No source cookie/session import | decisions.md ADR-010, prd.md REQ-009 | Confirmed by user selection |
| Basic use without registration | decisions.md ADR-009, prd.md REQ-012 | Confirmed by user |
| Extensions are optional, not MVP | AGENTS.md §1, prd.md §4.2, decisions.md OPEN-003 | Confirmed scope |
| .NET 10 LTS target | decisions.md ADR-002 | Proposed |
| WPF + MVVM desktop shell | decisions.md ADR-001 | Proposed |
| DPAPI local key + optional master password/Hello | decisions.md ADR-005 | Proposed |
| AES-256-GCM encrypted portable backup | decisions.md ADR-006 | Proposed |
| Local structured logs, no telemetry until opt-in | decisions.md ADR-007 | Proposed |
| Windows 11 primary target | prd.md REQ-014, decisions.md ADR-002 | Confirmed |

---

## Open Decisions Requiring Product Owner Resolution

### Tracked in decisions.md (consistent)

| ID | Decision Needed | Current Assumption | Impact |
|---|---|---|---|
| OPEN-001 | Free vs Pro limits/benefits | No artificial quotas | Subscription model and UX |
| OPEN-002 | Windows 10 Home/Pro support guarantee | Best-effort only | Minimum OS and support workload |
| OPEN-003 | Optional extension scope | Not MVP priority | Post-MVP feature scope |
| OPEN-004 | Product branding | "Switchly Next" working name | Package identity, domain, icon |
| OPEN-005 | License/account provider | Not integrated in MVP | Pro activation flow |
| OPEN-006 | Backup schedule/retention | Daily/7-snapshot proposal | Disk usage |
| OPEN-007 | Installer technology/signing | Signed per-user installer | Distribution and cost |

### Not tracked in decisions.md (discovered by review)

| # | Missing Decision | Severity | Impact |
|---|---|---|---|
| 1 | 🔴 **"Google Flow" identity** — not a recognized Google product name. Must be clarified with exact URL before TASK-003 | Blocker | Blocks M-001 |
| 2 | 🔴 **"Dola" identity** — ambiguous service name, no URL. Must be clarified | Blocker | Blocks M-001 |
| 3 | 🔴 **M-001 test harness project type** — console app, WPF app, or test project? Name? Location? | Blocker | Blocks TASK-002 |
| 4 | 🟡 **Minimum provider acceptance threshold** — if all 4 priority providers reject embedded CEF, what is the product pivot? | Major | Go/no-go framework |
| 5 | 🟡 **User-agent strategy** — default CEF UA or customized? Directly affects provider acceptance | Major | M-001 compatibility |
| 6 | 🟡 **CEF process model** — `MultiThreadedMessageLoop` vs WPF dispatcher integration | Major | Architecture correctness |
| 7 | 🟡 **Test framework** — xUnit vs NUnit (testing.md says "choose at scaffold") | Major | M-001 test infrastructure |
| 8 | 🟡 **Profile locking mechanism** — named mutex vs lock file | Major | TASK-002, TEST-017 |
| 9 | 🟡 **`PersistSessionCookies` default** — no value specified | Major | Profile behavior |
| 10 | 🟡 **Vault key hierarchy** — per-app, per-profile, or per-credential? | Major | M-007 security |
| 11 | 🟡 **KDF algorithm** — PBKDF2, Argon2id, or scrypt? | Major | M-007 backup |
| 12 | 🟡 **Archive format** — container, header, encryption envelope | Major | M-007 backup |
| 13 | 🟡 **DPAPI key loss recovery** — no recovery path defined | Major | Catastrophic risk |
| 14 | 🟡 **Popup routing policy** — `window.open()`, OAuth redirects | Major | Provider login compatibility |
| 15 | 🟡 **Profile archive lifecycle** — what happens when archived? How to unarchive? | Major | REQ-002 |
| 16 | 🟡 **Vault credential UX** — no screen/flow for viewing/managing passwords | Major | REQ-010 |
| 17 | 🟡 **Auto-group creation trigger** — when/how are `WebsiteDomain` groups created? | Major | REQ-004 |

---

## Contradictions and Inconsistencies

### Verbatim Duplications (copy-paste errors)
1. 🟡 **prd.md L33-37** — Priority provider paragraph duplicated within the same section.
2. 🟡 **architecture.md L9-13** — Same paragraph duplicated within §1.
3. 🟡 **README.md L102/L114** — Two sections numbered "9."

### Cross-Document Conflicts
4. 🟡 **architecture.md L133 references CEF 145.0 docs** while candidate version is 156.x (architecture.md L38). Doc URL version mismatch.
5. 🟡 **README.md L47 says `cd Switchly-Browser`** but no such directory is defined. Solution is `Switchly.sln`.
6. 🟡 **prd.md status labels** use Indonesian (Terbuka, Asumsi, Dikonfirmasi) while **decisions.md** uses English (Proposed, Confirmed, Open) for equivalent concepts. No language policy is stated.
7. 🟡 **M-001 exit criteria (plan.md L22)** list `TEST-003, 004, 006, 016, 017` but **omit provider compatibility** (TASK-003), which plan.md §3 describes as a must-have gate ("If any must-have fails, stop").
8. 🟡 **Priority providers have no REQ/AC/TEST chain** — exist only in prose (prd.md §3, plan.md TASK-003). Traceability is broken for this critical M-001 concern.
9. 🟡 **"Packaging" is listed as a TASK-006 go/no-go dimension** but no M-001 task covers it. ADR-008 says "M-001 or M-002" for installer PoC.
10. 🟡 **History data model is contradictory** — schema.md ENT-006 has `VisitCount >= 1` (suggesting aggregation) but index on `(ProfileId, VisitedAtUtc DESC)` suggests per-visit rows.
11. 🟡 **TASK-003 and TASK-004 have overlapping scope** — both test downloads, popups, and browser behavior with no clear boundary.

---

## Technical Risks

### Critical Path (M-001)
| Risk | Severity | Current Mitigation |
|---|---|---|
| Priority providers reject embedded CEF | 🔴 High | M-001 validates; but no minimum acceptance threshold defined |
| CEF/WPF thread integration deadlocks | 🟡 High | Not specified in architecture |
| Active profile RAM scaling (>1 GB for 10 profiles) | 🟡 High | M-001 measures; no suspend API available in CEF |
| CefSharp 156.x + .NET 10 compatibility untested | 🟡 High | TASK-001 validates |

### Deferred but Dangerous
| Risk | Severity | When |
|---|---|---|
| DPAPI key irrecoverable after Windows profile loss | 🔴 Catastrophic | Before M-007 |
| Backup corruption during active browsing | 🟡 High | Before M-007 |
| CEF security update lag behind Chromium stable | 🟡 High | Before beta |
| SQLite write contention under 10+ active profiles | 🟡 Medium | Before M-004 |
| Disk exhaustion from profiles + backup retention | 🟡 Medium | Before M-007 |

---

## M-001 Readiness Assessment

### Verdict: **CONDITIONALLY READY — 3 blockers must be resolved first**

| Gate | Status | Detail |
|---|---|---|
| Specification completeness | ⚠️ Conditional | 3 blockers, 17 majors identified |
| Technology candidate identified | ✅ Ready | CefSharp.Wpf.NETCore 156.0.150 is named |
| Task decomposition | ✅ Ready | TASK-001 through TASK-006 are defined |
| Test cases mapped | ⚠️ Partial | TEST IDs exist but traceability has gaps |
| Priority providers defined | ❌ Blocked | "Google Flow" and "Dola" are ambiguous |
| Test infrastructure defined | ❌ Blocked | Harness project type, synthetic test origin undefined |
| Exit criteria measurable | ⚠️ Partial | Provider compat not in exit formula; "responsive" is subjective |

### Must Resolve Before Starting M-001
1. 🔴 **Clarify "Google Flow"** — provide exact product name and URL
2. 🔴 **Clarify "Dola"** — provide exact URL
3. 🔴 **Define M-001 test harness** — project type, name, repo location, CefSharp package variant

### Should Resolve Before Starting M-001
4. 🟡 Choose test framework (xUnit vs NUnit)
5. 🟡 Define synthetic test origin (local server + test pages)
6. 🟡 Decide profile locking mechanism
7. 🟡 Add provider compatibility to M-001 exit criteria with formal REQ/AC/TEST
8. 🟡 Fix duplicate paragraphs in prd.md and architecture.md
9. 🟡 Fix README section numbering and clone directory name
10. 🟡 Clarify TASK-003 vs TASK-004 scope boundary

---

## Commands and Tests Actually Executed

| Item | Status |
|---|---|
| `dotnet build` | NOT RUN — no solution/projects exist |
| `dotnet test` | NOT RUN — no solution/projects exist |
| `dotnet format` | NOT RUN — no solution/projects exist |
| CEF browser tests | NOT RUN — no code exists |
| Provider compatibility tests | NOT RUN — no code exists |
| Performance measurements | NOT RUN — no code exists |
| EGM Goldfish review (egm-comprehension) | ✅ EXECUTED — report delivered |
| EGM Goldfish review (egm-design) | ✅ EXECUTED — report delivered |
| EGM Goldfish review (egm-implementation-readiness) | ✅ EXECUTED — report delivered |
| EGM Goldfish review (egm-zoom-out) | ❌ BLOCKED — quota exhausted |
| EGM Goldfish review (egm-structural) | ❌ BLOCKED — quota exhausted |
| Structural analysis (Elephant context) | ✅ EXECUTED — supplemental, not independent |

---

## Artifacts Produced

All artifacts saved under `docs/egm-artifacts/`:

| File | Source | Type |
|---|---|---|
| [`comprehension_report.md`](file:///c:/Users/FLYONZ/Documents/PROJEK/Switchly_Next_Antigravity_Starter/docs/egm-artifacts/comprehension_report.md) | egm-comprehension Goldfish | Independent review |
| [`design_critic_report.md`](file:///c:/Users/FLYONZ/Documents/PROJEK/Switchly_Next_Antigravity_Starter/docs/egm-artifacts/design_critic_report.md) | egm-design Goldfish | Independent review |
| [`implementation_readiness_report.md`](file:///c:/Users/FLYONZ/Documents/PROJEK/Switchly_Next_Antigravity_Starter/docs/egm-artifacts/implementation_readiness_report.md) | egm-implementation-readiness Goldfish | Independent review |
| [`structural_report.md`](file:///c:/Users/FLYONZ/Documents/PROJEK/Switchly_Next_Antigravity_Starter/docs/egm-artifacts/structural_report.md) | Coordinator (Elephant) | Supplemental analysis |
| [`consolidated_review_report.md`](file:///c:/Users/FLYONZ/Documents/PROJEK/Switchly_Next_Antigravity_Starter/docs/egm-artifacts/consolidated_review_report.md) | Coordinator (Elephant) | This report |

---

## Consolidated Finding Counts (Deduplicated)

| Severity | Count |
|----------|-------|
| 🔴 Blocker | 3 (Google Flow undefined, Dola undefined, test harness undefined) |
| 🟡 Major | 28 (deduplicated across all reviewers) |
| 🟢 Nit | 15 |
| **Total** | **46** |

> [!NOTE]
> The Design Critic identified 6 blockers in total, but 3 of those (vault key hierarchy, KDF algorithm, DPAPI recovery) are correctly deferred to M-007, not M-001 blockers. They are tracked as Major findings for M-001 readiness purposes.

---

## Next Steps (Recommended)

1. **Product owner resolves 3 blockers** — clarify Google Flow, Dola, and test harness project type.
2. **Fix copy-paste errors** — remove duplicate paragraphs in prd.md and architecture.md; fix README section numbering.
3. **Add provider compatibility to formal traceability** — create REQ/AC/TEST IDs.
4. **Decide M-001-scoped technology questions** — test framework, profile locking, user-agent strategy.
5. **Begin M-001 TASK-001** — CefSharp/CEF version research can start immediately (no blockers).
6. **Schedule deeper security review** — vault key hierarchy, KDF, archive format before M-007.
