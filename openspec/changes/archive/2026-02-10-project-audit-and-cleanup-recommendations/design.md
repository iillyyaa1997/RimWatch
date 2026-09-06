## Context

The repository currently mixes active product documentation with historical session notes, release artifacts, and duplicated roadmap content. This makes onboarding and routine maintenance expensive: contributors do not know which files are authoritative, and planning information drifts across multiple documents.

The change is process-focused: define a durable cleanup model first, then execute deletions/archives safely and incrementally. The repository already uses OpenSpec, so the design aligns planning and cleanup governance around that workflow.

## Goals / Non-Goals

**Goals:**
- Define an explicit triage model for root-level files (keep, archive, delete).
- Establish canonical documentation sources and deprecate competing duplicates.
- Produce a repeatable cleanup workflow that can be used for future hygiene passes.
- Preserve useful historical context without keeping it in the active root.
- Improve discoverability for new contributors with a concise project orientation map.

**Non-Goals:**
- No gameplay feature implementation.
- No refactor of automation code paths in this change.
- No reorganization of runtime assets that could affect RimWorld loading behavior.
- No forced rewrite of all historical docs into new format.

## Decisions

### 1) Use a three-tier document lifecycle (Keep / Archive / Delete)
- **Decision**: Every root document must be classified into one of three states.
- **Rationale**: Prevents ad-hoc removals and clarifies intent.
- **Alternatives considered**:
  - Keep everything and rely on search: rejected due to ongoing sprawl.
  - Delete aggressively without archive step: rejected due to loss of historical debugging context.

### 2) Keep a minimal canonical doc set in root
- **Decision**: Root should keep only stable entrypoint docs and essential project metadata.
- **Rationale**: Root is the first navigation surface and should optimize signal over history.
- **Alternatives considered**:
  - Keep many convenience guides in root: rejected because they become stale fastest.

### 3) Move historical materials to a structured archive area
- **Decision**: Session reports, old release notes, and temporary analysis notes are relocated to archive folders with date/version grouping.
- **Rationale**: Retains context while removing clutter from active workspace.
- **Alternatives considered**:
  - Keep history in git only: rejected because some historical docs are still useful for manual reference.

### 4) Make OpenSpec + selected docs the planning source of truth
- **Decision**: Active planning lives in OpenSpec changes/specs; root roadmap becomes high-level only or is reduced to index status.
- **Rationale**: Reduces contradictory planning artifacts.
- **Alternatives considered**:
  - Continue mixed planning in multiple docs: rejected due to drift already observed.

### 5) Add a project orientation map as a maintained artifact
- **Decision**: A compact map describes code areas, docs locations, and workflow entrypoints.
- **Rationale**: Reduces onboarding time and supports periodic cleanup.
- **Alternatives considered**:
  - Rely on README alone: rejected because README often becomes product-facing and too broad.

## Risks / Trade-offs

- **[Risk]** Deleting files that are still referenced in docs/scripts → **Mitigation**: run reference scan and update links before deletion.
- **[Risk]** Archive grows into another dump → **Mitigation**: enforce naming/date conventions and archive index.
- **[Risk]** Team uncertainty on what is canonical → **Mitigation**: document canonical list and add governance checklist.
- **[Trade-off]** More process overhead for docs changes → **Mitigation**: keep checklist lightweight and scoped to root-level changes.

## Migration Plan

1. Create classification inventory of root files.
2. Mark canonical keep-set and publish it.
3. Move selected historical docs into archive structure.
4. Delete confirmed obsolete files with no references.
5. Update README/ROADMAP links to new locations.
6. Add and publish project orientation map.
7. Validate by running link/reference checks and a quick contributor walkthrough.

Rollback:
- Since cleanup is file-system level, rollback is git-based (restore moved/deleted docs if conflicts or missing references are found).

## Open Questions

- Should archive live under `docs/archive/` or `archive/docs/` for consistency with existing repo conventions?
- Which document should remain as the top-level planning index after OpenSpec alignment (`README.md` section vs compact `ROADMAP.md`)?
- Do we need a periodic (for example monthly) hygiene cadence documented in contributor guidelines?
