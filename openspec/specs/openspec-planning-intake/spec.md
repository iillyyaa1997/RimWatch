## ADDED Requirements

### Requirement: Standard OpenSpec intake flow
The project SHALL define a single intake flow for non-trivial planning items: capture intent, classify scope, assign priority, and create linked change artifacts.

#### Scenario: New non-trivial request intake
- **WHEN** a new non-trivial request is accepted for planning
- **THEN** a corresponding OpenSpec planning record is created with scope classification and priority

### Requirement: Intake classification gate
The intake process SHALL classify each request as tiny-fix, standard, or complex before execution starts.

#### Scenario: Classification drives process path
- **WHEN** a request is classified during intake
- **THEN** the workflow path (tiny exception or full OpenSpec artifacts) is selected deterministically

### Requirement: Intake traceability
The intake workflow SHALL produce an explicit trace link between planning record, change artifacts, and implementation output.

#### Scenario: End-to-end trace
- **WHEN** a reviewer inspects completed work
- **THEN** they can follow links from intake record to change artifacts and resulting implementation
