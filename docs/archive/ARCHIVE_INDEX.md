# Archive Index

This folder stores historical and non-authoritative documents moved out of repository root.

## Convention

- Location: `docs/archive/<year>/<topic-or-release>/`
- Naming: keep original file name whenever possible.
- Source note: every moved file should include original root path in this index.

## Index Entry Format

`- <original-path> -> <archive-path> | status: archived|deleted | reason: <short rationale>`

## Current Entries

- `README.md` legacy roadmap links -> `docs/archive/ARCHIVE_INDEX.md` | status: archived-reference | reason: linked historical files were removed from root cleanup pass.
- `ROADMAP.md` legacy release-note links -> `docs/archive/ARCHIVE_INDEX.md` | status: archived-reference | reason: historical note files were removed; legacy section preserved for context.

## Notes

- If a historical doc is intentionally deleted (not archived), keep a short entry with `status: deleted`.
- Active planning authority is `ROADMAP.md`; OpenSpec artifacts are under `openspec/changes/`.
