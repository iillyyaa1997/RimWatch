## ADDED Requirements

### Requirement: OpenSpec planning registry authority
The project SHALL treat OpenSpec planning artifacts as the only authoritative source for actionable planned work.

#### Scenario: Authoritative planning lookup
- **WHEN** a contributor needs to determine what work is actionable now
- **THEN** they MUST use OpenSpec planning records and not root roadmap task lists

### Requirement: Required planning metadata
Each authoritative planning record SHALL include minimum metadata: priority, owner, status, linked implementation artifact, and verification note.

#### Scenario: Record completeness check
- **WHEN** a maintainer reviews a planning record
- **THEN** they can validate all required metadata fields without reading external notes

### Requirement: Planning status lifecycle
The planning registry SHALL support consistent lifecycle states for work tracking: proposed, accepted, in-progress, verified, and closed.

#### Scenario: Lifecycle transition
- **WHEN** implementation and verification complete for a planning record
- **THEN** the record transitions to closed with a linked verification outcome
