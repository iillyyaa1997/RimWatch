## 1. Establish Single Source Of Truth

- [x] 1.1 Redesign root `ROADMAP.md` with fixed sections: Active, Next, Later, Done, Decision Log
- [x] 1.2 Add roadmap item ID format and status markers for consistent tracking
- [x] 1.3 Add explicit statement in `ROADMAP.md` that it is the only authoritative task backlog

## 2. Remove Planning Ambiguity

- [x] 2.1 Audit root planning docs and identify conflicting/duplicate task lists
- [x] 2.2 Update `README.md` planning section to point to `ROADMAP.md` as primary entrypoint
- [x] 2.3 Mark non-roadmap planning docs as reference/archive-only (no active task authority)

## 3. Define Lightweight Workflow

- [x] 3.1 Document minimal cycle: pick task from Active, define acceptance, implement, verify, update roadmap
- [x] 3.2 Define when OpenSpec is optional vs required for complex changes
- [x] 3.3 Add one concrete example in docs showing roadmap-only planning flow

## 4. Synchronization Rules

- [x] 4.1 Define rule that implementation commits/PRs reference roadmap item IDs
- [x] 4.2 Define rule that roadmap status update happens in the same work cycle as implementation
- [x] 4.3 Add a small checklist for maintainers to verify roadmap/implementation sync

## 5. Rollout Validation

- [x] 5.1 Run one full planning cycle using only `ROADMAP.md` for task selection
- [x] 5.2 Validate that a contributor can find current task in under one minute
- [x] 5.3 Record initial decisions and adjustments in roadmap Decision Log
