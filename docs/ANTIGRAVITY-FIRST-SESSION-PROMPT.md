# Antigravity — First Session Prompt for Switchly Next

Use this prompt after opening the Switchly Next project root in Antigravity and confirming the EGM plugin is enabled.

```text
You are starting work on Switchly Next. Before changing any files:

1. Read GEMINI.md and AGENTS.md.
2. Read all nine canonical project documents: README.md, prd.md, plan.md, architecture.md, schema.md, design.md, rules.md, testing.md, and decisions.md.
3. Verify the workspace-local EGM plugin exists at .agents/plugins/egm/ and is discoverable/enabled in Antigravity.
4. Confirm the current Antigravity surface exposes the EGM skills needed for this task. If a Goldfish workflow is requested, verify the fresh-context invoke_subagent capability before claiming it can run.
5. Treat the old ferdilpu-sudo/Switchly repository as a read-only product reference. This project must be built from a clean source tree. Do not copy WinUI or WebView2 implementation code.

Run a specification-readiness review only. Do not create application source code yet. Identify contradictions, open decisions, traceability gaps, and any discrepancy between the agreed product and the documents. Use EGM review workflows where available, and save artifacts under docs/egm-artifacts/. Do not rewrite canonical documents without showing the proposed changes and obtaining product-owner approval.

At the end, report:
- documents and EGM skills actually read/invoked;
- whether invoke_subagent was available;
- confirmed product decisions;
- open decisions and technical risks;
- whether M-001 is ready to begin;
- commands/tests not run.

Do not claim a review or test passed unless it was actually executed.
```
```
