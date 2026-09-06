## Why

Planning discipline is still inconsistent because OpenSpec is used only partially and ad-hoc. A stronger OpenSpec-first process is needed now to make scope, decisions, and tasks traceable before implementation starts.

## What Changes

- Define OpenSpec as default planning workflow for all non-trivial changes.
- Introduce explicit entry criteria for when a change must start with OpenSpec artifacts.
- Define artifact quality gates (proposal/design/specs/tasks) before implementation.
- Add lightweight exception path for very small fixes while still requiring traceability.
- Align root documentation so contributors can consistently follow OpenSpec-first flow.

## Capabilities

### New Capabilities
- `openspec-first-governance`: Defines policy and decision rules for OpenSpec-first planning and execution.
- `openspec-quality-gates`: Defines minimum artifact completeness and acceptance criteria before implementation.

### Modified Capabilities
- None.

## Impact

- Affected systems: planning process, contributor workflow, documentation governance.
- Affected files: OpenSpec artifacts, contributor docs, and references from root docs.
- Dependencies: no runtime/gameplay code dependencies; workflow/documentation change only.
