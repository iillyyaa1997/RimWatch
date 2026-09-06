## MODIFIED Requirements

### Requirement: Single authoritative roadmap file
The repository SHALL treat OpenSpec planning artifacts as the only authoritative planning source for actionable work, while `ROADMAP.md` remains strategic index.

#### Scenario: Actionable planning source lookup
- **WHEN** contributors need actionable planning status or tasks
- **THEN** they use OpenSpec planning artifacts and not roadmap task lists

### Requirement: No parallel authoritative task lists
The repository SHALL treat actionable task lists outside OpenSpec planning artifacts as non-authoritative.

#### Scenario: Actionable tasks outside OpenSpec
- **WHEN** a task exists only in `ROADMAP.md` or other docs
- **THEN** it is non-authoritative until represented in OpenSpec planning records

### Requirement: Roadmap status synchronization
The project SHALL keep `ROADMAP.md` synchronized as a strategic summary that points to OpenSpec planning entrypoints for actionable state.

#### Scenario: Roadmap strategic sync
- **WHEN** planning migration completes
- **THEN** roadmap stores only strategic summary and links, while actionable planning state is maintained in OpenSpec

### Requirement: Structured roadmap sections
If roadmap remains in repository, it SHALL contain strategic planning summary and migration-safe navigation to OpenSpec, without authoritative executable task sections.

#### Scenario: Roadmap fallback navigation
- **WHEN** a contributor opens retained roadmap file
- **THEN** they are directed to OpenSpec planning index in one hop
