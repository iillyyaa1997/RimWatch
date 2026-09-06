## Context

The repository recently introduced strong planning governance but still has legacy planning volume in `ROADMAP.md`. This creates dual maintenance, drift risk, and unclear historical authority boundaries.

This change promotes OpenSpec from "primary for non-trivial execution" to "single authoritative planning system", while retaining `ROADMAP.md` as strategic index and navigation entrypoint. Stakeholders are maintainers and contributors who need deterministic intake, complete historical trace, and no-loss migration confidence.

## Goals / Non-Goals

**Goals:**
- Make OpenSpec the authoritative system for actionable planning records.
- Migrate all roadmap planning content to OpenSpec with explicit mapping and parity checks.
- Define a stable intake and tracking flow for planning items in OpenSpec.
- Keep `ROADMAP.md` as strategic index after migration verification.
- Reduce planning drift and duplicated status maintenance.

**Non-Goals:**
- No gameplay/runtime code changes.
- No forced refactor of non-planning product documentation beyond required links.
- No CI enforcement in this change (policy first, automation later).
- No removal of historical content without a verified OpenSpec destination.

## Decisions

### 1) OpenSpec planning registry as source of truth
- **Decision**: Planning authority moves to OpenSpec capabilities and change/task artifacts.
- **Rationale**: One operational source reduces contradictions and status skew.
- **Alternative rejected**: Keep dual authority (`ROADMAP.md` + OpenSpec), because it already produced drift.

### 2) Keep roadmap as strategic index, not task authority
- **Decision**: `ROADMAP.md` keeps strategic context and navigation links, but actionable planning authority is migrated to OpenSpec.
- **Rationale**: Preserves quick root-level orientation without duplicating operational planning.
- **Alternative rejected**: Remove roadmap file entirely; rejected because root-level strategic orientation remains useful for contributors.

### 3) Introduce structured intake metadata in OpenSpec
- **Decision**: New planning records require minimal metadata: priority, owner, status, linked change, and verification note.
- **Rationale**: Reviewers need quick consistency checks without parsing narrative text.
- **Alternative rejected**: Free-form intake notes; rejected due to weak comparability and poor triage.

### 4) Phased migration with no-loss verification
- **Decision**: Migrate roadmap content in batches with explicit mapping table, parity checklist, and unmapped-item gate.
- **Rationale**: Prevents silent loss and enables deterministic review.
- **Alternative rejected**: Big-bang rewrite without verification; rejected for unacceptable information-loss risk.

### 5) Tiny-fix exception remains, but planning record rules tighten
- **Decision**: Tiny fixes may still bypass full change artifacts, but authoritative planning records for non-trivial work must be in OpenSpec.
- **Rationale**: Keeps speed for trivial edits while hardening real planning discipline.
- **Alternative rejected**: No exceptions; rejected as impractical for maintenance micro-fixes.

## Risks / Trade-offs

- **[Risk]** Contributors continue editing roadmap as backlog out of habit  
  **Mitigation**: keep roadmap sections explicit as non-authoritative and link every actionable section to OpenSpec entrypoints.
- **[Risk]** Migration misses roadmap planning details  
  **Mitigation**: two-pass inventory, mapping table, and explicit unmatched-lines report.
- **[Risk]** OpenSpec artifact volume becomes noisy  
  **Mitigation**: metadata conventions and monthly hygiene review.
- **[Trade-off]** Higher upfront migration effort  
  **Mitigation**: fewer sync conflicts and clearer auditability.

## Migration Plan

1. Publish OpenSpec planning authority policy and intake schema.
2. Create/modify specs for planning registry, intake, migration, and integrity verification.
3. Create roadmap planning inventory with section and item-level IDs.
4. Migrate all planning-relevant roadmap content into OpenSpec records with one-to-one mapping entries.
5. Produce unmatched-content report and resolve all unresolved entries.
6. Replace roadmap actionable planning sections with strategic summary + OpenSpec entrypoint references.
7. Validate discoverability, traceability, and parity evidence from root docs.
8. Run one pilot cycle: intake -> implementation -> verification -> closure using OpenSpec only.

Rollback strategy:
- If parity checks fail, restore roadmap from git and keep migrated OpenSpec records with `draft` status until remapping is complete.

## Open Questions

- Should roadmap include minimal strategic status snapshots or only links to OpenSpec planning index?
- Do we require automated parity checks (scripted) before final roadmap retirement?
- Which OpenSpec location should hold migrated long-form historical planning notes for best discoverability?
