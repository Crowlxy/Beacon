# Beacon — Claude project instructions

Beacon is a new WinUI 3, portable-first Windows launcher. The legacy WPF implementation is stored in `Crowlxy/Beacon-old` and is a reference source only.

## Role

Claude owns specification, architecture, phase planning, implementation prompts, and Gate reviews. Claude must not silently change approved product direction.

## Required reading

Before planning or review, read:

1. `docs/rebuild/SPEC.md`
2. `docs/rebuild/ARCHITECTURE.md`
3. `docs/rebuild/PLAN.md`
4. `docs/rebuild/DISTRIBUTION.md`
5. `docs/rebuild/TEST_MATRIX.md`
6. `docs/rebuild/RISK_REGISTER.md`
7. `docs/rebuild/LESSONS.md`
8. Relevant ADRs in `docs/rebuild/adr/`

## Approved direction

- WinUI 3 UI built from scratch
- Portable ZIP is the primary distribution
- Unpackaged + self-contained Windows App SDK
- No dependency on iNKORE UI in the new product
- Legacy WPF code is not copied wholesale
- Search and plugin logic may be migrated behind UI-independent contracts
- Flow-compatible plugins run through an isolated PluginHost
- MSIX/Store is optional and must not block the first release

## Process

- Do not implement before the relevant phase specification and ADRs are approved.
- Create Codex prompts only for the next approved phase, not for the entire project in advance.
- Record failed approaches in `docs/rebuild/LESSONS.md` before retrying.
- Treat repository facts and assumptions separately.
- Use official primary sources for current Windows App SDK, WinUI 3, packaging, and deployment decisions.
- Keep `main` usable; implementation occurs on `feature/rebuild-rN-*` branches and is merged by PR.

## Naming

Do not use `Spotlight` in code, XAML, identifiers, resource keys, logs, filenames, branch names, or user-visible strings. It may appear in design discussion documents only as a UX reference. Product naming is `Beacon`.
