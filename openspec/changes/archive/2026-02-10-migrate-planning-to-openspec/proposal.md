## Why

Planning ownership is still fragmented, and `ROADMAP.md` can drift from OpenSpec planning artifacts. We need OpenSpec as authoritative planning system while keeping `ROADMAP.md` as a concise strategic index with links and status summary.

## What Changes

- Move planning authority and actionable planning content from `ROADMAP.md` into OpenSpec artifacts.
- Define a single OpenSpec planning intake workflow for creating, prioritizing, and tracking work.
- Introduce migration rules to convert roadmap actionable planning items into OpenSpec-tracked records with no-loss verification.
- Define governance for planning metadata (priority, owner, status, trace links) in OpenSpec.
- Keep `ROADMAP.md` as strategic navigation/index document and remove its actionable planning authority. **BREAKING**

## Capabilities

### New Capabilities
- `openspec-planning-registry`: Canonical OpenSpec-based registry for active/planned tasks with required metadata.
- `openspec-planning-intake`: Standardized intake workflow for creating and prioritizing new planning items directly in OpenSpec.
- `roadmap-to-openspec-migration`: Controlled migration procedure for moving actionable roadmap planning content into OpenSpec while preserving traceability and semantic fidelity.
- `planning-content-integrity-verification`: Verification workflow that proves migration completeness and detects content loss or unmapped items.

### Modified Capabilities
- `single-roadmap-source-of-truth`: Redefine roadmap role as strategic index while authoritative planning records live in OpenSpec.
- `lightweight-planning-without-opsx`: Restrict lightweight path to tiny fixes only; all planning records remain in OpenSpec.
- `openspec-first-governance`: Tighten policy from "OpenSpec-first for non-trivial" to "OpenSpec-only for authoritative planning records and planning history."

## Impact

- Affected systems: planning workflow, contributor onboarding, maintainer triage cadence, planning history retention.
- Affected files: `ROADMAP.md`, `README.md`, `CONTRIBUTING.md`, workflow docs, OpenSpec specs/tasks, and migration evidence artifacts.
- Affected process: all actionable planning creation/status moves to OpenSpec authority; roadmap remains strategic summary/index.
- Dependencies: no runtime/gameplay changes; documentation/process migration only.
