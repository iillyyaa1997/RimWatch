## ADDED Requirements

### Requirement: Migration parity baseline
Before roadmap authority transition, the project SHALL generate a parity baseline inventory of roadmap planning content with stable item identifiers.

#### Scenario: Baseline generation
- **WHEN** migration starts
- **THEN** maintainers have a complete itemized inventory used as migration source baseline

### Requirement: No-loss mapping verification
The migration workflow SHALL require 100% mapping coverage for baseline planning items, with explicit unresolved-item list if coverage is incomplete.

#### Scenario: Coverage gate
- **WHEN** migration validation runs
- **THEN** any unmapped baseline item fails the migration gate

### Requirement: Semantic fidelity check
The migration workflow SHALL verify that meaning-critical fields (status, priority, owner intent, and decision rationale) are preserved in OpenSpec destination records.

#### Scenario: Fidelity review
- **WHEN** a mapped item is reviewed
- **THEN** reviewer can confirm semantic parity between source entry and OpenSpec destination

### Requirement: Authority-transition safety report
Roadmap planning authority transition SHALL require a final migration report containing coverage summary, unresolved count, and link integrity result.

#### Scenario: Authority transition approval
- **WHEN** maintainers decide to remove roadmap actionable planning authority
- **THEN** they approve based on the final no-loss migration report
