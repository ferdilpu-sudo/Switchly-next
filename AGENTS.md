# Switchly Next — AI Agent Operating Instructions

**Status:** Mandatory project instructions for Antigravity and other coding agents.
**Project mode:** New-generation application built from a clean source tree.
**EGM:** Workspace-local Elephant-Goldfish Model plugin snapshot is included under `.agents/plugins/egm/`.

## 1. Mission and boundaries

Build **Switchly Next**, a Windows desktop multi-account browser workspace, from a new solution and a newly designed UI. The old repository `https://github.com/ferdilpu-sudo/Switchly` is a read-only product-reference source, not the implementation base.

Do not migrate the old WinUI/WebView2 source tree into this project. Do not copy old XAML, code-behind, WebView2 wrappers, WebView2-specific infrastructure, or legacy Studio implementation unless the product owner explicitly authorizes a narrowly scoped reuse.

The new product must:
- Use an integrated browser surface inside the desktop application.
- Not use Microsoft WebView2, directly or as a fallback.
- Keep browser storage isolated per profile.
- Support tabs and managed secondary windows.
- Prioritize Google Flow, ChatGPT, Dola, and SeaArt as compatibility-test targets; this is not a guarantee that each provider accepts embedded browsers.
- Treat extension support as optional and deferred; it is not an MVP acceptance gate.
- Respect provider authentication, age, safety, anti-abuse, and acceptable-use policies. Never bypass sign-in protections, age gates, CAPTCHA, account restrictions, or embedded-browser restrictions.
- Use local-first storage and encrypted backup as defined by the project documents.

Current technology baseline is a **proposal**, not a license to skip validation: C# + WPF/MVVM + .NET 10 candidate + standard CEF through CefSharp + SQLite/EF Core + Windows DPAPI + AES-256-GCM encrypted backup. WebView2 is excluded. Exact CEF/CefSharp/package/runtime versions must be pinned only after M-001 compatibility and packaging verification. Do not switch to Rust, Electron, Tauri, WebView2, or a custom Chromium fork without a decision recorded by the product owner in `decisions.md`.

## 2. Canonical project documents

Read these root documents before implementation:

1. `README.md` — project orientation and current status.
2. `prd.md` — product scope, REQ IDs and acceptance criteria.
3. `plan.md` — milestone sequence and current progress.
4. `architecture.md` — system boundaries and browser lifecycle.
5. `schema.md` — authoritative data contracts.
6. `design.md` — new UI and interaction behavior.
7. `rules.md` — engineering and security rules.
8. `testing.md` — test strategy and evidence requirements.
9. `decisions.md` — ADR status and unresolved decisions.
10. `AGENTS.md` — these agent operating rules.

Authority order:
1. Latest explicit product-owner decision.
2. `prd.md` for feature scope and acceptance.
3. Accepted ADRs in `decisions.md` for decisions and rationale.
4. `architecture.md` for technical boundaries.
5. `schema.md` for data contracts.
6. `design.md` for UI behavior.
7. `rules.md` and `testing.md` for implementation and verification constraints.
8. `plan.md` for execution order and progress.
9. Legacy Switchly repository for reference only.

If documents conflict, stop and report the exact conflict. Do not silently pick the easiest interpretation or update canonical requirements without approval.

## 3. EGM plugin and skill loading

The complete EGM runtime snapshot is stored at `.agents/plugins/egm/`. Do not copy only the `skills/` folder: EGM skills also read reviewer personas from `agents/` and standards/templates from `resources/` and `docs/`.

Before using an EGM workflow:
1. Verify the EGM plugin is visible/enabled in Antigravity Customizations/Installed (or `agy plugin list` when using the CLI).
2. Verify that the required skill is listed by Antigravity. Do not claim a skill ran merely because its source file exists.
3. Read the skill's `.agents/plugins/egm/skills/<skill-name>/SKILL.md` and follow its complete workflow, including its gates and required supporting references.
4. For Goldfish reviews, verify that the current Antigravity surface exposes the fresh-context `invoke_subagent` capability. If unavailable, stop that EGM review and report `BLOCKED`; do not simulate fresh reviewers in the parent context or claim an independent multi-agent review occurred.

EGM skill routing:
- Major product discovery or a new feature with unresolved behavior: `egm-brainstorm`.
- Turn an approved concept brief into a technical design: `egm-draft`.
- Review an existing design/specification: `egm-review`.
- Iterative configured review: `egm-review-loop`.
- Classify and resolve review findings: `egm-resolve`.
- Small bug fix, refactor, or bounded implementation task: `egm-lite`.
- Map/document a new source tree after scaffolding: `egm-bootstrap`.
- Compare canonical design documents with implemented code after milestones: `egm-drift-report`.

Do not invoke every EGM skill for every task. Select the smallest workflow that matches the work. EGM methods do not override product-owner authority or the canonical project documents.

### Antigravity skill command syntax

Use the skill names exposed by the installed plugin in the current Antigravity surface. Current Antigravity documentation describes plugin skills as `/<skill-name>`. EGM's own documentation specifies `/egm:<skill-name>` for the `agy` CLI (for example `/egm:egm-brainstorm`). If the App/IDE surface presents a different command, use its discovered skill entry rather than guessing. See `docs/EGM-ANTIGRAVITY-SETUP.md`.

## 4. Required workflow by task size

### Major feature or architecture change
1. Inspect the relevant root documents and affected source modules.
2. Run `egm-brainstorm` if the concept or important trade-offs are unresolved.
3. Obtain product-owner agreement on the concept brief.
4. Run `egm-draft` for a design that affects architecture, schema, security, or several modules.
5. Run `egm-review` or a suitable `egm-review-loop` before implementation.
6. Run `egm-resolve` for findings that need classification and resolution.
7. Obtain explicit product-owner approval of the revised design before coding.
8. Update only the canonical project docs affected by the approved decision.

### Normal implementation task
1. Select one task from the current milestone in `plan.md`.
2. Run `egm-lite` for a bounded code change when appropriate; follow its critique, checklist, execution, review, and resolution gates.
3. Implement the smallest vertical slice with tests.
4. Update progress and source-of-truth docs only when verified behavior or approved requirements change.

### Documentation and drift
- Use `egm-bootstrap` after the clean source tree has a meaningful structure or when module README maps are requested.
- Use `egm-drift-report` after a milestone or when asked to check whether implementation still matches the docs.
- Save all EGM briefs, drafts, checklists, and reports under `docs/egm-artifacts/` unless the skill requires another named artifact. Preserve its required filenames; link/copy the final approved output into the canonical location only after user approval.

## 5. Implementation rules

- Work on one `M-###` milestone and one small `TASK-###` at a time.
- Inspect the existing repository and working tree before editing. Never overwrite uncommitted human/agent work.
- Do not create a second implementation path, parallel browser engine, duplicate profile store, or temporary `*-fix`/`*-patch` architecture.
- Keep UI, application use-cases, domain rules, data adapters, and browser-host APIs separated according to `architecture.md`.
- Browser engine APIs must be isolated behind the BrowserHost boundary. Domain and application logic must not depend directly on WPF/CEF implementation classes.
- One source file has one primary responsibility. Avoid giant ViewModels, browser manager god objects, and unrelated partial-class growth.
- Each `ProfileId` must map to one immutable, unique persistent CEF storage context/path. Never share a user profile's request context or browser storage with another profile.
- Do not use a global CEF context for account browsing. Close/release all CEF handles before deleting or reusing a profile path.
- Treat website content, downloaded files, imported browser data, CSV, archive entries, URLs, and extension metadata as untrusted input.
- No privileged native API may be exposed to websites through an unrestricted JavaScript bridge.
- Never store website passwords, cookies, OAuth tokens, or browser-session material in the application SQLite database or logs.
- Do not promise that a provider will accept embedded CEF. If a priority website blocks embedded login, respect that restriction and report the tested behavior; do not spoof user-agent or circumvent provider safeguards.
- Do not implement extensions as a blocker or add Chrome Runtime complexity unless the product owner reopens `REQ-007`.

## 6. Testing and evidence

Every task must map to at least one `REQ-###`, `AC-###`, and `TEST-###`, or explain why it is internal-only. Run the exact applicable commands from `README.md` and `testing.md` once scaffolding exists.

Before claiming success, report:
- exact commands and environment;
- tests run and actual PASS/FAIL/BLOCKED/NOT RUN results;
- browser engine/runtime version for CEF tests;
- real Windows manual checks for provider workflows that cannot be validated headlessly;
- memory/CPU measurements with the device specification, not invented estimates;
- evidence paths with credentials, cookies, private URLs, and personal data redacted.

A green build does not prove profile isolation, provider sign-in compatibility, or acceptable resource usage. Never claim runtime tests passed unless they ran on the target Windows environment.

## 7. Security, privacy, and release boundaries

- Never commit secrets, production tokens, signing keys, passwords, cookies, or real user browser profiles.
- Never deploy, publish, sign production artifacts, register paid services, or change production licensing without explicit authorization.
- Verify CEF/CefSharp licensing, transitive licenses, redistribution requirements, and security-update policy before distribution.
- Keep diagnostics local and redact secrets by default. No telemetry without approved privacy and consent requirements.
- Treat Windows 10 support as a separately validated compatibility claim; Windows 11 is the primary test target unless the product owner changes the policy.

## 8. Mandatory completion report

At the end of every task, report:

1. Summary of change.
2. Files created/changed.
3. REQ / AC / TEST mapping.
4. Commands and checks actually executed.
5. PASS / FAIL / BLOCKED / NOT RUN evidence.
6. Risks and unresolved decisions.
7. Current milestone/task status in `plan.md`.
8. Next smallest task.

Never mark a milestone complete while any required acceptance criterion is unverified.
