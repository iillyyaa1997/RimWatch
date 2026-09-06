# Repository Hygiene Report

Date: 2026-02-10

## 1) Root-Level Inventory

### Code and runtime
- `Source/`, `Defs/`, `Languages/`, `About/`, `Tests/`

### Workflow/process
- `openspec/`, `.cursor/`, `CONTRIBUTING.md`, `DEVELOPMENT_GUIDELINES.md`, `ROADMAP.md`

### Product/docs
- `README.md`, `QUICK_START.md`, `STORYTELLERS_GUIDE.md`, `CHANGELOG.md`, `HOTKEY.md`

### Build/config/metadata
- `Makefile`, `Dockerfile`, `docker-compose.yml`, `build.sh`, `Directory.Build.props`, `.editorconfig`, `.gitignore`, `LICENSE`, `.env.example`, `stylecop.json`

## 2) Duplicate/Conflict Pairs

- `README.md` roadmap narrative vs OpenSpec actionable planning artifacts.
- `README.md` process snippets vs `CONTRIBUTING.md` / `DEVELOPMENT_GUIDELINES.md`.
- Historical release-note links in `README.md` and `ROADMAP.md` pointing to removed root files.

## 3) Files Referenced By Core Docs

- From `README.md`: `ROADMAP.md`, `CHANGELOG.md`, `QUICK_START.md`, `STORYTELLERS_GUIDE.md`, `DEVELOPMENT_GUIDELINES.md`.
- From `ROADMAP.md`: internal planning sections + legacy context.
- From developer docs: `README.md`, `ROADMAP.md`, `docs/OPENSPEC_WORKFLOW.md`.

## 4) Root Non-Code Classification (Keep / Archive / Delete)

- Keep:
  - `README.md` (entrypoint)
  - `ROADMAP.md` (strategic index + historical context)
  - `CONTRIBUTING.md` (contribution process)
  - `DEVELOPMENT_GUIDELINES.md` (coding/logging rules)
  - `CHANGELOG.md` (release history)
  - `QUICK_START.md`, `STORYTELLERS_GUIDE.md`, `HOTKEY.md` (user docs)
- Archive:
  - legacy release notes and temporary analysis docs previously kept in root (now treated as archive-only references)
- Delete:
  - obsolete duplicate planning snapshots and temporary session-status markdown files

## 5) Canonical Documentation Set

- `README.md`
- `ROADMAP.md`
- `CONTRIBUTING.md`
- `DEVELOPMENT_GUIDELINES.md`
- `docs/OPENSPEC_WORKFLOW.md`
- `docs/PROJECT_ORIENTATION.md`

## 6) Archive Convention

- Archive location: `docs/archive/<year>/<topic-or-release>/`
- Index: `docs/archive/ARCHIVE_INDEX.md`
- Entry format:
  - `<original-path> -> <archive-path> | status: archived|deleted | reason: <text>`

## 7) Cleanup Execution Summary

- Broken links to removed root history files were replaced with archive reference (`docs/archive/ARCHIVE_INDEX.md`) or current canonical docs.
- Planning authority aligned to OpenSpec planning artifacts; roadmap retained as strategic index.
- Orientation map added (`docs/PROJECT_ORIENTATION.md`).

## 8) Post-Cleanup Validation Checklist

- [x] Root navigation points to canonical docs.
- [x] Planning source of truth is explicit.
- [x] Broken historical links in core docs are remapped.
- [x] Build/test instructions discoverable from root in <=2 hops.
- [x] OpenSpec workflow discoverable from contributor docs.

## 9) Hygiene Cadence And Ownership

- Cadence: monthly lightweight hygiene pass.
- Owner: maintainer handling roadmap triage for that cycle.
- Gate: before release branch cut, run quick link and canonical-doc validation.
