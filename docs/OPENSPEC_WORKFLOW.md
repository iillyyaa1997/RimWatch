# OpenSpec-First Workflow

This project uses an OpenSpec-first process for non-trivial changes.

## 1) Policy

### Default rule
- Non-trivial work MUST start with an OpenSpec change.
- Required artifacts before implementation: `proposal`, `design`, `specs`, `tasks`.

### Tiny-fix exception
- Tiny fixes MAY skip full OpenSpec.
- Tiny fix criteria:
  - affects one file or a very small localized edit;
  - no behavior/architecture change;
  - no requirement/spec change;
  - low rollback risk.
- Tiny-fix trace note is REQUIRED in OpenSpec planning registry.

Trace note format:
`Tiny-fix exception: <what changed> | reason: <why safe> | evidence: <build/test/manual check>`

## 2) Roadmap Linkage Rule

- `ROADMAP.md` is strategic index only.
- Authoritative actionable planning record must exist in `openspec/planning/PLANNING_REGISTRY.md`.
- Each non-tiny planning record must link to corresponding OpenSpec change name.

## 3) Quality Gates

### Proposal gate
- Motivation is explicit ("why now").
- Scope boundaries are explicit.
- Capability mapping is present.

### Design gate
- Key decisions are explicit and justified.
- Risks/trade-offs are listed.
- Migration or rollback strategy is described.

### Specs gate
- Requirements are normative (SHALL/MUST).
- Every requirement has at least one testable scenario.
- Scenarios use clear WHEN/THEN outcomes.

### Tasks gate
- Tasks are actionable and bounded.
- Dependency order is clear.
- Completion condition is observable.

## 4) Operational Checklist

1. Create OpenSpec change.
2. Complete proposal/design/specs/tasks.
3. Verify quality gates.
4. Implement tasks and mark task checkboxes in change tasks file.
5. Update OpenSpec planning record status in same work cycle.
6. If all tasks are done, archive change.

## 5) Examples

### Example A: Mandatory OpenSpec change (medium)
- Change: add policy-driven roadmap governance.
- Scope: multiple docs and workflow behavior.
- Action: create OpenSpec change and complete all four artifacts before implementation.

### Example B: Tiny-fix exception
- Change: typo in `README.md`.
- Scope: one-line text fix, no behavior change.
- Action: apply fix directly + add tiny-fix trace note in OpenSpec planning registry.

## 6) Reviewer Quick Validation

- Is this non-trivial? If yes, linked OpenSpec change exists.
- Are all required artifacts complete and coherent?
- Do specs contain testable scenarios?
- Does implementation map to checked tasks?
- Is OpenSpec planning status updated in same cycle?
