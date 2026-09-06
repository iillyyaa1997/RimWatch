## 1. Planning Authority Transition

- [x] 1.1 Define OpenSpec planning registry metadata schema (priority, owner, status, links, verification note)
- [x] 1.2 Update governance docs to state OpenSpec as authoritative planning source for non-tiny work
- [x] 1.3 Keep `ROADMAP.md` as strategic index and remove its actionable planning authority

## 2. Intake Workflow Standardization

- [x] 2.1 Document intake flow: classify request (tiny/standard/complex), assign priority, create planning record
- [x] 2.2 Define tiny-fix exception trace format and required verification evidence
- [x] 2.3 Add contributor guidance for linking intake record -> change artifacts -> implementation output

## 3. Roadmap To OpenSpec Migration

- [x] 3.1 Build full inventory of roadmap planning-relevant items (tasks, priorities, decisions, planning notes)
- [x] 3.2 Create mapping table from roadmap source items to OpenSpec destinations
- [x] 3.3 Migrate all mapped planning items into OpenSpec records and convert roadmap planning sections into strategic summaries + links

## 4. Capability Spec Alignment

- [x] 4.1 Sync modified capabilities in `openspec/specs/` (`single-roadmap-source-of-truth`, `lightweight-planning-without-opsx`, `openspec-first-governance`)
- [x] 4.2 Sync new capabilities in `openspec/specs/` (`openspec-planning-registry`, `openspec-planning-intake`, `roadmap-to-openspec-migration`, `planning-content-integrity-verification`)
- [x] 4.3 Validate requirement/scenario completeness after sync (each requirement has at least one testable scenario)

## 5. Integrity Verification

- [x] 5.1 Generate migration parity baseline with stable source item identifiers
- [x] 5.2 Run no-loss mapping coverage check (target: 100% mapped baseline items)
- [x] 5.3 Produce authority-transition safety report (coverage, unresolved items, link integrity)

## 6. Verification And Rollout

- [x] 6.1 Run traceability check from root docs to authoritative OpenSpec planning records
- [x] 6.2 Pilot one standard planning cycle fully in OpenSpec (intake -> apply -> verification -> closure)
- [x] 6.3 Record migration outcomes, friction points, and follow-up automation opportunities
