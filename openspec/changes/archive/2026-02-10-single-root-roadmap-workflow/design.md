## Context

The project currently has multiple planning surfaces (historical docs, roadmap variants, OpenSpec artifacts), creating uncertainty about where to pick tasks from. The user explicitly wants one planning entrypoint: a single `ROADMAP.md` in root used every day.

This design keeps OpenSpec optional for structured changes, but not required for routine planning. The practical contract is: if there is a task, it must exist in `ROADMAP.md`; if it is not in `ROADMAP.md`, it is not planned work.

## Goals / Non-Goals

**Goals:**
- Make root `ROADMAP.md` the only authoritative backlog file.
- Define stable roadmap structure that supports short-term execution and long-term planning.
- Provide simple team workflow that works even without opsx/OpenSpec commands.
- Ensure roadmap state stays synchronized with implementation progress.
- Reduce planning friction for day-to-day work.

**Non-Goals:**
- No gameplay/system feature implementation.
- No mandatory migration of all historical OpenSpec artifacts into roadmap.
- No requirement to stop using OpenSpec completely; it becomes optional.
- No deep information architecture redesign outside planning docs.

## Decisions

### 1) Single source of truth = `ROADMAP.md`
- **Decision**: Root `ROADMAP.md` is canonical for all active/planned tasks.
- **Rationale**: Removes ambiguity and eliminates task fragmentation.
- **Alternative rejected**: Keep dual source (`ROADMAP.md` + OpenSpec tasks) due to ongoing drift.

### 2) Roadmap has fixed operational sections
- **Decision**: `ROADMAP.md` uses fixed sections: Active, Next, Later, Done, Decision Log.
- **Rationale**: Predictable structure makes planning fast and audit-friendly.
- **Alternative rejected**: Free-form roadmap text because it quickly becomes narrative instead of actionable.

### 3) Planning-first workflow without mandatory opsx
- **Decision**: Daily flow is `pick from Active -> implement -> move status in ROADMAP`.
- **Rationale**: Works with minimal tooling and lower cognitive overhead.
- **Alternative rejected**: forcing OpenSpec for every small change.

### 4) OpenSpec becomes optional for complex items
- **Decision**: OpenSpec is used only for large, risky, or cross-cutting changes; roadmap still links them.
- **Rationale**: Keeps rigor where needed, speed where possible.
- **Alternative rejected**: banning OpenSpec completely, which removes useful structure for bigger initiatives.

### 5) Sync rule is explicit and enforceable
- **Decision**: Any implementation PR/commit should reference a roadmap item ID; roadmap item status must be updated in same workflow.
- **Rationale**: Prevents stale roadmap and hidden work.
- **Alternative rejected**: manual periodic sync, which is often skipped.

## Risks / Trade-offs

- **[Risk]** Roadmap can still become noisy if unmanaged → **Mitigation**: enforce section limits and monthly pruning.
- **[Risk]** Complex initiatives may be under-specified without OpenSpec → **Mitigation**: require OpenSpec when risk/size threshold is exceeded.
- **[Trade-off]** Less formalism for small tasks may reduce historical detail → **Mitigation**: keep concise Decision Log entries for key choices.
- **[Risk]** Contributors continue creating side task lists → **Mitigation**: document rule that side lists are drafts and must be merged into roadmap.

## Migration Plan

1. Redesign `ROADMAP.md` into fixed operational structure.
2. Consolidate active tasks from scattered docs into roadmap sections.
3. Mark other planning docs as archive/reference-only and remove task authority from them.
4. Update `README.md` to state that roadmap is the primary planning entrypoint.
5. Define optional OpenSpec trigger criteria for complex work.
6. Run one planning cycle using new flow and adjust wording if needed.

Rollback:
- If the new structure fails, restore previous roadmap format from git and keep only minimal source-of-truth statement while iterating.

## Open Questions

- What exact threshold defines “complex enough” to require OpenSpec (file count, risk level, cross-module impact)?
- Should roadmap item IDs be date-based (`RW-2026-02-001`) or section-based (`A-01`, `N-03`)?
- Do we need automation (lint/check) to ensure only one authoritative roadmap file remains?
