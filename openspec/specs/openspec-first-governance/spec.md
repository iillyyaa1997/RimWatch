## MODIFIED Requirements

### Requirement: Mandatory OpenSpec for non-trivial work
The project SHALL require OpenSpec artifacts for all non-tiny actionable planning records before implementation begins.

#### Scenario: Starting non-tiny work
- **WHEN** a contributor proposes standard or complex change work
- **THEN** OpenSpec planning artifacts are required prior to implementation

### Requirement: Defined exception path for tiny fixes
The project SHALL allow tiny-fix exceptions only with explicit classification and trace note linking the exception to verification evidence.

#### Scenario: Tiny-fix trace evidence
- **WHEN** a tiny-fix exception is used
- **THEN** the record includes rationale and concrete verification evidence

### Requirement: Roadmap-to-OpenSpec linkage
The planning process SHALL avoid roadmap-based actionable planning authority and SHALL represent planning references in OpenSpec planning registry and change artifacts, with optional strategic references in roadmap.

#### Scenario: OpenSpec-native planning trace
- **WHEN** a reviewer inspects planning lineage
- **THEN** actionable planning references are resolvable within OpenSpec artifacts, and roadmap references (if present) are directional only

### Requirement: Governance checklist enforcement
The process SHALL enforce checklist validation for planning metadata completeness, artifact readiness, and traceability before implementation.

#### Scenario: Pre-implementation governance validation
- **WHEN** a change is marked ready for apply
- **THEN** checklist confirms metadata, artifacts, and trace links are complete
