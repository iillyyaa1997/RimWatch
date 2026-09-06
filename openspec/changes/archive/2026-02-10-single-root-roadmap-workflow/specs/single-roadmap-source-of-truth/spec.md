## ADDED Requirements

### Requirement: Single authoritative roadmap file
The repository SHALL use root-level `ROADMAP.md` as the only authoritative source for active and planned tasks.

#### Scenario: Task authority
- **WHEN** contributors plan new work
- **THEN** they MUST add or update the task in `ROADMAP.md` before implementation begins

### Requirement: No parallel authoritative task lists
The repository SHALL treat other planning documents as reference-only and not as authoritative task backlogs.

#### Scenario: Conflicting tasks in secondary docs
- **WHEN** a task appears in another document but not in `ROADMAP.md`
- **THEN** the task is considered non-authoritative until it is represented in `ROADMAP.md`

### Requirement: Roadmap status synchronization
The project SHALL synchronize roadmap item status with implementation progress.

#### Scenario: Task completion update
- **WHEN** implementation of a roadmap task is completed
- **THEN** the corresponding roadmap item status is updated in the same work cycle

### Requirement: Structured roadmap sections
The roadmap SHALL provide fixed sections for execution and tracking: Active, Next, Later, Done, and Decision Log.

#### Scenario: Roadmap readability
- **WHEN** a contributor opens `ROADMAP.md`
- **THEN** they can immediately identify what to do now, what is queued, and what was completed
