# OpenSpec Planning Registry

This file is the authoritative registry for actionable planning items.

## Metadata Schema

Each planning record MUST include:

- `id`: stable planning ID (format `PLN-YYYYMMDD-XX`)
- `title`: concise actionable title
- `type`: `tiny-fix` | `standard` | `complex`
- `priority`: `P0` | `P1` | `P2` | `P3`
- `status`: `proposed` | `accepted` | `in-progress` | `verified` | `closed`
- `owner`: maintainer/assignee
- `change`: OpenSpec change name (or `n/a` for tiny-fix)
- `capabilities`: affected spec capabilities
- `verification`: link to evidence (build/test/manual notes)
- `source`: optional link to migrated source item(s)

## Active Planning Records

- `PLN-20260210-01` | title: migrate planning authority to OpenSpec | type: complex | priority: P0 | status: in-progress | owner: maintainer | change: `migrate-planning-to-openspec` | capabilities: `openspec-planning-registry`, `openspec-planning-intake`, `roadmap-to-openspec-migration`, `planning-content-integrity-verification` | verification: pending | source: `ROADMAP.md` planning sections

- `PLN-20260210-02` | title: maintain OpenSpec-first governance baseline | type: standard | priority: P1 | status: closed | owner: maintainer | change: `openspec-first-workflow-governance` (archived) | capabilities: `openspec-first-governance`, `openspec-quality-gates` | verification: archived change tasks complete | source: historical

- `PLN-20260210-03` | title: maintain repository hygiene governance baseline | type: standard | priority: P2 | status: closed | owner: maintainer | change: `project-audit-and-cleanup-recommendations` (archived) | capabilities: `repo-hygiene-governance`, `project-orientation-map` | verification: archived change tasks complete | source: historical

## Tiny-Fix Trace Notes

Use format:
`Tiny-fix exception: <what changed> | reason: <why safe> | evidence: <build/test/manual check>`

Current entries:
- None.

## Authority Rule

- Actionable planning status MUST be updated here (and/or in linked change tasks) in the same work cycle.
- `ROADMAP.md` remains strategic index and historical context; it is not authoritative task storage.
