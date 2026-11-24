# EGM + Antigravity Setup — Switchly Next

**Purpose:** Ensure Antigravity can discover and invoke the Elephant-Goldfish Model (EGM) skills while following Switchly Next's canonical project instructions.

## 1. What is included

This starter includes a workspace-scoped EGM plugin at:

```text
.agents/plugins/egm/
  plugin.json
  README.md
  LICENSE
  skills/
  agents/
  resources/
  docs/
  assets/
```

This is the complete runtime set described by the uploaded EGM source package. Do not reduce it to `skills/` alone: EGM skills read reviewer personas under `agents/` and document standards under `resources/`.

**EGM source snapshot:** repository `google/elephant-goldfish-model`, plugin version `0.1.1`, source commit recorded in the uploaded archive as `7d1b721e51448518cc9b0336465041230200dd10` (archive file timestamps: 2026-11-21). This is a vendored workspace snapshot, not an automatic updater.

## 2. Open the correct workspace

1. Extract or clone the Switchly Next project folder.
2. Open the project root in Antigravity (the folder containing `.agents/`, `AGENTS.md`, `GEMINI.md`, and `prd.md`).
3. Wait for workspace customizations/plugins to load, or reload the workspace.
4. In Antigravity App, open **Customizations → Installed** and confirm EGM appears. The plugin folder is workspace-scoped, so it is enabled for this project rather than every project on the computer.
5. Confirm the eight EGM skills are discoverable: `egm-brainstorm`, `egm-draft`, `egm-review`, `egm-review-loop`, `egm-resolve`, `egm-lite`, `egm-bootstrap`, and `egm-drift-report`.

Antigravity's documented workspace plugin path is `.agents/plugins/`. Its project instruction discovery includes root `AGENTS.md`, `GEMINI.md`, and the `.agents/` directory.

## 3. Alternative: install EGM globally instead

Choose this only if you want EGM available in all workspaces. Do not intentionally maintain two diverging copies (workspace and global) without a reason.

In an Antigravity CLI terminal:

```powershell
agy plugin install https://github.com/google/elephant-goldfish-model
agy plugin list
```

In the Antigravity App, use the **Customizations** area to inspect/install/manage plugins. A manual global plugin directory is `%USERPROFILE%\.gemini\config\plugins\egm\`.

If using the global copy, you may remove `.agents/plugins/egm/` from the project only after confirming the global plugin is enabled and discoverable. Keeping the workspace copy makes the project more reproducible but means plugin updates must be copied into the repository deliberately.

## 4. Skill invocation

Antigravity's general plugin documentation describes invoking skills as `/<skill-name>`. Use the exact skill name surfaced by your installed Antigravity version, for example:

```text
/egm-brainstorm
/egm-draft
/egm-review prd.md
/egm-review-loop
/egm-resolve
/egm-lite
/egm-bootstrap
/egm-drift-report .
```

The EGM source documentation specifies the namespace form for the **Antigravity CLI (`agy`)**:

```text
/egm:egm-brainstorm
/egm:egm-draft
/egm:egm-review prd.md
```

The EGM source snapshot says the exact command form for Antigravity App and Antigravity for IDEs was not separately verified. If the examples do not appear in the skill picker, inspect Antigravity's discovered skills and use the command names it displays. Do not claim a skill ran if the host did not invoke it.

## 5. EGM workflow for this project

| Work type | EGM skill | Switchly gate |
|---|---|---|
| New feature with unresolved requirements | `egm-brainstorm` | Save brief under `docs/egm-artifacts/`; product-owner agreement required |
| Turn agreed concept into architecture/design | `egm-draft` | Update affected canonical docs only after review |
| Review PRD/architecture/design/plan | `egm-review` | Fresh-context reviewer capability must be available |
| Iterative custom review | `egm-review-loop` | Specify target file, reviewers, standards, and context |
| Classify and resolve review findings | `egm-resolve` | Product owner chooses changes; do not auto-accept all findings |
| Small bug fix/refactor | `egm-lite` | Follow critique/checklist/execute/review/resolution stages |
| Map a built codebase | `egm-bootstrap` | Run after the new source tree exists; do not recreate canonical project docs without approval |
| Compare docs with implementation | `egm-drift-report` | Use after a milestone or on request |

EGM artifacts (briefs, drafts, review reports, checklists) should be written to `docs/egm-artifacts/` unless the skill's required artifact name/path is explicit. Preserve source skill gates and filenames; place approved changes into canonical project documents after user approval.

## 6. Fresh-context reviewer requirement

EGM Goldfish reviews depend on fresh-context subagents. The EGM source snapshot documents `invoke_subagent` for Antigravity and says the standalone Antigravity IDE surface was not supported for the required review subagent capability at the snapshot date. Before `egm-brainstorm`, `egm-review`, or review loops:

- check that the current Antigravity surface actually exposes `invoke_subagent`;
- if unavailable, stop that workflow and report `BLOCKED`;
- do not simulate independent reviewers in the main conversation;
- do not describe a same-context critique as an EGM Goldfish review.

Antigravity App and Antigravity for IDEs are the documented EGM installation targets in the uploaded source guide. Re-check current EGM documentation before relying on standalone IDE support.

## 7. Project authority and safety

- `AGENTS.md` is the operational authority for this workspace.
- The nine other canonical project documents own their respective product/design/architecture/schema/decision/rules/testing/plan/quickstart subjects.
- EGM is a methodology and reviewer tool. It does not override product-owner decisions or silently rewrite approved scope.
- The legacy Switchly WebView2 repository is reference-only. Switchly Next is built from a clean source tree.
- WebView2 is explicitly excluded. Do not introduce it as a fallback.
- CEF (Chrome Runtime) via CefSharp is the current candidate; exact versions and compatibility remain gated by M-001.
- Extensions are optional, not MVP-critical.
- Respect provider policies and applicable age/eligibility requirements; do not attempt to bypass them.

## 8. Quick verification checklist

- [ ] The Antigravity workspace root is the Switchly Next project root.
- [ ] `AGENTS.md` and `GEMINI.md` are visible to the agent.
- [ ] `.agents/plugins/egm/plugin.json` exists.
- [ ] `skills/`, `agents/`, `resources/`, and `docs/` exist under `.agents/plugins/egm/`.
- [ ] EGM is visible/enabled in Antigravity Customizations/Installed (or `agy plugin list`).
- [ ] Required skill appears in Antigravity's discovered skill list.
- [ ] `invoke_subagent` is available before claiming Goldfish review was completed.
- [ ] An EGM artifact is written under `docs/egm-artifacts/` for the first workflow.

**No EGM workflow or Antigravity session has been executed as part of creating this starter pack.** This document is the configuration and verification guide; confirm plugin discovery in your actual Antigravity installation.
