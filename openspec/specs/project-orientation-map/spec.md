## Purpose

Provide a compact orientation map that makes code, docs, workflow, and planning entry points discoverable.

## Requirements

### Requirement: Project orientation map
The project SHALL provide a concise orientation map that explains where core code, active docs, archives, and workflow artifacts are located.

#### Scenario: New contributor orientation
- **WHEN** a new contributor needs to understand repository structure
- **THEN** they can find code, documentation, and workflow entry points from a single orientation map

### Requirement: Ownership of source-of-truth documents
The project SHALL identify which documents are source-of-truth for planning, onboarding, and development workflow.

#### Scenario: Planning source is unambiguous
- **WHEN** a contributor needs current implementation planning status
- **THEN** they can determine the primary source-of-truth document/workflow without conflicting guidance

### Requirement: Cleanup recommendations with priorities
The project SHALL provide cleanup recommendations grouped by priority and action type (keep, archive, delete).

#### Scenario: Actionable cleanup plan
- **WHEN** maintainers review repository hygiene recommendations
- **THEN** they receive a prioritized action list that can be executed incrementally

### Requirement: Validation checklist after cleanup
The project SHALL define a post-cleanup validation checklist that includes link integrity and basic navigation sanity checks.

#### Scenario: Cleanup verification
- **WHEN** a cleanup batch is completed
- **THEN** maintainers can run a checklist and confirm no critical docs/workflows became unreachable
