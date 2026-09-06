## ADDED Requirements

### Requirement: Full roadmap planning migration mapping
The migration process SHALL map each roadmap actionable planning-relevant item (tasks, priorities, operational decisions, and planning notes) to corresponding OpenSpec records before roadmap authority transition.

#### Scenario: Mapping planning entries
- **WHEN** maintainers migrate roadmap planning content
- **THEN** each planning-relevant item receives a mapping entry to a concrete OpenSpec destination

### Requirement: Controlled roadmap de-authoritization
The project SHALL remove roadmap actionable planning authority only after migration mapping and trace validation are complete.

#### Scenario: Safe authority switch
- **WHEN** migration mapping reaches completion criteria
- **THEN** roadmap remains strategic summary/index without losing planning traceability

### Requirement: Migration validation checklist
Migration SHALL include validation for link integrity, record completeness, semantic parity, and contributor discoverability.

#### Scenario: Post-migration verification
- **WHEN** migration batch is completed
- **THEN** maintainers run checklist and confirm no actionable task became unreachable
