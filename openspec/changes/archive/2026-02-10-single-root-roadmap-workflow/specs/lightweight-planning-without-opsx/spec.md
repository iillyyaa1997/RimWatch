## ADDED Requirements

### Requirement: Lightweight planning flow
The project SHALL support a lightweight planning flow that does not require opsx/OpenSpec commands for routine tasks.

#### Scenario: Routine task planning
- **WHEN** a maintainer plans a small or medium task
- **THEN** they can plan, execute, and close the task using `ROADMAP.md` only

### Requirement: Optional OpenSpec for complex work
The workflow SHALL allow OpenSpec usage as optional escalation for complex, high-risk, or cross-cutting changes.

#### Scenario: Escalating complexity
- **WHEN** a roadmap item is identified as complex or ambiguous
- **THEN** maintainers may create an OpenSpec change and link it from the roadmap item

### Requirement: Minimal operational checklist
The planning process SHALL define a short repeatable checklist: select task, define acceptance, implement, verify, update roadmap.

#### Scenario: Consistent execution cycle
- **WHEN** a contributor starts work from the roadmap
- **THEN** they can follow the checklist without additional tooling instructions

### Requirement: Decision logging in roadmap
The process SHALL record important planning decisions in roadmap Decision Log entries.

#### Scenario: Capturing planning decisions
- **WHEN** maintainers change priority, scope, or approach
- **THEN** they add a concise decision entry in `ROADMAP.md`
