# Project Orientation Map

## What Lives Where

- Code: `Source/RimWatch/` (automation, UI, settings, utilities).
- Game definitions: `Defs/`.
- Localization: `Languages/`.
- Tests: `Tests/`.
- Workflow/process artifacts: `openspec/changes/`, `openspec/specs/`, `openspec/planning/`.
- Documentation:
  - Product and onboarding: `README.md`, `QUICK_START.md`
  - Strategic roadmap index: `ROADMAP.md`
  - Authoritative planning source: `openspec/planning/PLANNING_REGISTRY.md`
  - Process rules: `CONTRIBUTING.md`, `docs/OPENSPEC_WORKFLOW.md`, `DEVELOPMENT_GUIDELINES.md`
  - Archive index: `docs/archive/ARCHIVE_INDEX.md`

## Workflow Entrypoints

- Pick work: `openspec/planning/PLANNING_REGISTRY.md`.
- Non-trivial work: create/use OpenSpec change in `openspec/changes/`.
- Tiny fix: apply directly + add trace note in OpenSpec planning registry.
- Build/test commands: `README.md` -> "Быстрые команды".

## Canonical Docs (Root)

- `README.md`: project overview and user/dev entrypoint.
- `ROADMAP.md`: strategic priorities and historical context.
- `CONTRIBUTING.md`: contributor process and standards.
- `DEVELOPMENT_GUIDELINES.md`: coding and logging rules.

## Two-Hop Discoverability Check

- Build/test/run path: `README.md` -> "Быстрые команды" (1 hop from root).
- OpenSpec process path: `CONTRIBUTING.md` -> `docs/OPENSPEC_WORKFLOW.md` -> `openspec/planning/PLANNING_REGISTRY.md` (3 hops).
- Planning path: root -> `README.md` -> `openspec/planning/PLANNING_REGISTRY.md` (2 hops).
