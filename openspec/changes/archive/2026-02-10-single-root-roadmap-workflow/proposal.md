## Why

Current planning is split across many documents and OpenSpec artifacts, which makes day-to-day task selection unclear. A single authoritative roadmap file in repository root is needed so all work starts from one visible source of truth.

## What Changes

- Establish `ROADMAP.md` in repository root as the only authoritative task backlog.
- Define governance rules so no parallel task lists are maintained in other root docs.
- Introduce a lightweight workflow that allows planning and execution without mandatory OpenSpec usage.
- Standardize roadmap sections: Active, Next, Later, Done, and Decision Log.
- Define sync rule between implementation progress and roadmap updates.

## Capabilities

### New Capabilities
- `single-roadmap-source-of-truth`: Defines repository behavior where one root roadmap controls task planning and execution priorities.
- `lightweight-planning-without-opsx`: Defines an optional, minimal planning workflow for users who do not want to rely on opsx commands.

### Modified Capabilities
- None.

## Impact

- Affected files: `ROADMAP.md`, `README.md`, and process documentation references.
- Affected systems: planning workflow and contributor onboarding.
- Dependencies: no runtime/gameplay dependency changes; documentation and process changes only.
