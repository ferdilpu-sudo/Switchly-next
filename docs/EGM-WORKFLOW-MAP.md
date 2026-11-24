# EGM Workflow Map — Switchly Next

This is a project-specific index, not a replacement for the EGM skill instructions. Before invoking a skill, read its complete `SKILL.md` under `.agents/plugins/egm/skills/` and follow its gates.

## Suggested feature lifecycle

1. `egm-brainstorm` — clarify user value, goals/non-goals, constraints, alternatives, and decisions. Save the agreed brief in `docs/egm-artifacts/`.
2. `egm-draft` — produce a design document covering behavior, architecture, data changes, failure states, security, rollout, and tests.
3. `egm-review` — run independent fresh-context reviewers on the design. Verify `invoke_subagent` exists first.
4. `egm-resolve` — classify findings as accepted, contested, proposed, or declined according to the skill's protocol; require product-owner approval for material requirement changes.
5. Implement in one milestone/task from `plan.md`.
6. `egm-lite` — use its bounded implementation workflow for each small code change where applicable.
7. Run tests and update progress with evidence.
8. `egm-drift-report` — compare canonical docs against code after the milestone.

## What each root document owns

- `prd.md`: why/what, user flows, REQ and AC.
- `design.md`: UX, navigation, screens, states, accessibility.
- `architecture.md`: components, dependencies, runtime and trust boundaries.
- `schema.md`: entities, fields, data ownership and migrations.
- `decisions.md`: ADR rationale and open decisions.
- `rules.md`: enforceable engineering rules.
- `testing.md`: TEST cases and release gates.
- `plan.md`: milestone order, tasks, dependencies and status.
- `README.md`: setup and orientation.
- `AGENTS.md`: agent behavior and project authority.

## Required artifact hygiene

Store EGM working artifacts in `docs/egm-artifacts/`. Never replace canonical documents with an unreviewed EGM draft. After approval, merge only the accepted content into the relevant canonical file and update traceability across REQ/AC/TEST/Milestone IDs.
