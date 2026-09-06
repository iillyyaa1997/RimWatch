## ADDED Requirements

### Requirement: Mandatory OpenSpec for non-trivial work
The project SHALL require an OpenSpec change for all non-trivial planned changes before implementation begins.

#### Scenario: Starting a medium feature
- **WHEN** a contributor proposes a medium or large change
- **THEN** an OpenSpec change is created and used as the implementation contract

### Requirement: Defined exception path for tiny fixes
The project SHALL allow tiny fixes to bypass full OpenSpec workflow only when explicitly marked as exception and trace-noted.

#### Scenario: Tiny fix exception
- **WHEN** a contributor applies a one-line typo or similarly tiny fix
- **THEN** they record an exception note with rationale in project tracking docs

### Requirement: Roadmap-to-OpenSpec linkage
The planning process SHALL link roadmap items to corresponding OpenSpec change names when OpenSpec is required.

#### Scenario: Traceable planned work
- **WHEN** a roadmap item enters active implementation
- **THEN** it includes a reference to the associated OpenSpec change identifier

### Requirement: Governance checklist enforcement
The process SHALL provide a maintainer checklist for validating OpenSpec artifacts before implementation starts.

#### Scenario: Pre-implementation review
- **WHEN** a change is marked ready for apply
- **THEN** maintainer checklist confirms minimum governance criteria are met
