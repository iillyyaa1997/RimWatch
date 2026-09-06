## MODIFIED Requirements

### Requirement: Lightweight planning flow
The project SHALL allow lightweight planning without full artifact creation only for explicitly classified tiny fixes, while still recording the planning record in OpenSpec.

#### Scenario: Tiny-fix lightweight path
- **WHEN** a maintainer classifies work as tiny fix
- **THEN** they may use lightweight path with mandatory trace note

### Requirement: Optional OpenSpec for complex work
The workflow SHALL require OpenSpec planning artifacts for standard and complex work, and SHALL NOT treat OpenSpec as optional for authoritative planning records.

#### Scenario: Standard work planning
- **WHEN** a maintainer plans small, medium, or complex non-tiny work
- **THEN** they create and track it through OpenSpec planning artifacts

### Requirement: Minimal operational checklist
The planning process SHALL define a repeatable OpenSpec-centric checklist: intake, classify, create/refresh artifacts, implement, verify, close.

#### Scenario: Consistent OpenSpec execution cycle
- **WHEN** a contributor starts non-trivial work
- **THEN** they can execute the full cycle using OpenSpec artifacts only

### Requirement: Decision logging in roadmap
The process SHALL record authoritative planning decisions in OpenSpec artifacts; roadmap decision entries MAY keep strategic context summaries with links to OpenSpec records.

#### Scenario: Planning decision placement
- **WHEN** maintainers make planning decisions
- **THEN** full decision rationale and status updates are stored in OpenSpec, and roadmap may include strategic summary references
