## Purpose

Define repeatable repository hygiene rules for triage, archiving, and safe deletion of root documentation.

## Requirements

### Requirement: Root file triage policy
The project SHALL classify each root-level non-code file into one of three states: Keep, Archive, or Delete, before applying cleanup changes.

#### Scenario: Classifying current root docs
- **WHEN** a cleanup pass is started
- **THEN** each root-level markdown and note file is assigned a triage state with rationale

### Requirement: Canonical documentation set
The project SHALL maintain an explicit canonical list of root-level documentation files that represent active and authoritative guidance.

#### Scenario: Authoritative docs are discoverable
- **WHEN** a contributor opens the repository root
- **THEN** they can identify canonical docs from a published canonical list without inspecting historical files

### Requirement: Safe archive workflow
The project SHALL preserve non-canonical but potentially useful historical documents by moving them into a structured archive location instead of deleting by default.

#### Scenario: Historical context is preserved
- **WHEN** a file is marked non-canonical but still informationally useful
- **THEN** it is moved to the archive location with a predictable naming/grouping convention

### Requirement: Controlled deletion criteria
The project SHALL delete documents only after verifying they are obsolete and not referenced by active docs or workflows.

#### Scenario: Preventing broken references
- **WHEN** a file is marked for deletion
- **THEN** link/reference checks are performed and any live references are updated or the deletion is deferred
