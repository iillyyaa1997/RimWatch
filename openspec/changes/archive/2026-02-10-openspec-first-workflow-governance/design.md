## Context

The repository now has roadmap-focused planning direction, but process discipline still depends on personal habit. To "lean into OpenSpec", the team needs a default workflow where change definition quality is enforced before coding starts.

OpenSpec-first does not need to block tiny fixes; however, all medium/large work should have traceable proposal, requirements, and implementation tasks. This design introduces governance rules, quality gates, and exception handling.

## Goals / Non-Goals

**Goals:**
- Make OpenSpec the default planning mechanism for non-trivial work.
- Define clear thresholds for when OpenSpec is mandatory.
- Introduce artifact quality gates before implementation begins.
- Ensure every change has traceability from intent to tasks.
- Keep fast path for tiny fixes while preserving accountability.

**Non-Goals:**
- No gameplay feature development in this change.
- No mandatory bureaucracy for one-line typo fixes.
- No replacement of roadmap workflow; roadmap and OpenSpec can coexist by linkage.
- No external tooling dependencies beyond existing OpenSpec CLI.

## Decisions

### 1) OpenSpec-first by default for non-trivial changes
- **Decision**: Any change above "tiny fix" must start as an OpenSpec change.
- **Rationale**: Prevents ambiguous scope and undocumented decisions.
- **Alternative rejected**: Optional OpenSpec for all work, which keeps planning quality inconsistent.

### 2) Mandatory quality gates before apply
- **Decision**: `proposal`, `design`, `specs`, and `tasks` must be complete for mandatory OpenSpec changes.
- **Rationale**: Ensures "why/what/how" are explicit before code.
- **Alternative rejected**: proposal+tasks only, which often leaves vague requirements.

### 3) Exception path with constraints
- **Decision**: Tiny fixes can skip full OpenSpec but require short trace note in roadmap/changelog and clear rationale.
- **Rationale**: Preserves speed where full process is overkill.
- **Alternative rejected**: zero exceptions, likely to be ignored in urgent cases.

### 4) Link roadmap items to OpenSpec changes
- **Decision**: For planned feature/fix work, roadmap items include OpenSpec change name when applicable.
- **Rationale**: Keeps top-level visibility and detailed artifacts connected.
- **Alternative rejected**: separate disconnected planning tracks.

### 5) Add governance checklist for reviewers/maintainers
- **Decision**: A lightweight checklist validates scope, requirement testability, and task decomposition.
- **Rationale**: Operationalizes process without heavy tooling.
- **Alternative rejected**: relying on informal reviewer memory.

## Risks / Trade-offs

- **[Risk]** Perceived slowdown for contributors → **Mitigation**: tiny-fix exception path + concise templates.
- **[Risk]** Artifact quality may become checkbox-only → **Mitigation**: checklist focuses on concrete acceptance quality.
- **[Trade-off]** More upfront planning effort → **Mitigation**: fewer reworks and clearer execution later.
- **[Risk]** Dual-source confusion (roadmap vs OpenSpec) → **Mitigation**: roadmap links to change IDs; OpenSpec stores details.

## Migration Plan

1. Publish OpenSpec-first policy and mandatory threshold criteria.
2. Add governance checklist and examples of good artifacts.
3. Update contributor docs and roadmap notes with new flow.
4. Run 1-2 pilot changes fully through OpenSpec-first process.
5. Review friction points and refine threshold/checklist.

Rollback:
- If adoption friction is too high, revert to "OpenSpec recommended" while retaining checklist for larger changes.

## Open Questions

- What exact threshold defines "non-trivial": changed files count, risk level, or cross-module scope?
- Should PR template include explicit OpenSpec linkage fields?
- Do we want CI to verify OpenSpec artifact presence for labeled feature/fix PRs?
