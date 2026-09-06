# Migration Outcomes (2026-02-10)

## Outcomes

- OpenSpec planning registry introduced as authoritative planning source.
- Roadmap converted to strategic/index role; actionable authority removed.
- Intake workflow, mapping baseline, and safety report published.
- Capability specs synced to main `openspec/specs/`.

## Friction Points

- Legacy roadmap includes large historical checklist volume, making direct one-by-one migration noisy.
- Previously published docs used mixed authority language and required harmonization.
- Change-history archives still contain earlier roadmap-authority language (expected for historical snapshot).

## Follow-up Automation Opportunities

- Add script to regenerate baseline metrics and compare to mapping coverage.
- Add CI lint for forbidden wording (e.g., "ROADMAP is authoritative backlog").
- Add CI check requiring `PLN-*` link for non-tiny changes before merge.
