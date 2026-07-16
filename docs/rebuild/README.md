# Beacon rebuild documentation

This directory is the source of truth for the WinUI 3 portable rebuild.

## Document priority

When documents conflict, use this order:

1. `SPEC.md`
2. Approved ADRs in `adr/`
3. `ARCHITECTURE.md`
4. `DISTRIBUTION.md`
5. `PLAN.md`
6. Phase implementation prompt

Report unresolved contradictions instead of choosing silently.

## Current phase

The repository is being initialized for **Phase R0: audit and rebuild boundary**. No product source project should be generated until the R1 technical-spike decisions are documented and approved.

## Planned documents

- `SPEC.md` — product requirements and non-goals
- `ARCHITECTURE.md` — project and process boundaries
- `PLAN.md` — phases R0–R11 and release gates
- `AUDIT.md` — verified facts from Beacon-old
- `DEPENDENCY_MAP.md` — legacy dependencies and their destinations
- `COMPATIBILITY.md` — Flow plugin compatibility tiers
- `DISTRIBUTION.md` — portable ZIP, DataRoot, update, and optional MSIX
- `MIGRATION.md` — settings, history, plugin, and data migration
- `TEST_MATRIX.md` — automated, integration, and release tests
- `RISK_REGISTER.md` — major risks and mitigations
- `PROMPTS.md` — approved phase-specific Codex prompts
- `LESSONS.md` — failed approaches and recurrence prevention

The corresponding legacy WPF planning documents remain in `Beacon-old` and do not control this repository.