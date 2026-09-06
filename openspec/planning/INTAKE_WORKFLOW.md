# OpenSpec Planning Intake Workflow

## Classification

Classify every request before implementation:

- `tiny-fix`: one-file/localized, low-risk, no requirement change
- `standard`: bounded change with requirement impact
- `complex`: cross-cutting/risky/ambiguous or migration-heavy

## Intake Steps

1. Capture request intent and expected outcome.
2. Classify (`tiny-fix` / `standard` / `complex`).
3. Assign `priority` and `owner`.
4. Create/update planning record in `openspec/planning/PLANNING_REGISTRY.md`.
5. For non-tiny: create or update OpenSpec change and artifacts.
6. Link planning record -> change -> capability specs.

## Exception Path (Tiny-Fix)

Tiny-fix MAY skip full change flow only if:

- classification is explicitly `tiny-fix`
- trace note is added to planning registry
- verification evidence is attached

Trace format:
`Tiny-fix exception: <what changed> | reason: <why safe> | evidence: <build/test/manual check>`

## Traceability Rule

Every non-tiny planning record must link:

- planning registry record (`PLN-*`)
- OpenSpec change name (`openspec/changes/<change>/`)
- implementation verification evidence
