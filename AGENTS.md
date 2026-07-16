# Beacon — implementation agent instructions

You are implementing Beacon, a new WinUI 3 portable Windows launcher.

## Before editing

Read `CLAUDE.md`, `docs/rebuild/PLAN.md`, the current phase prompt, relevant ADRs, and `docs/rebuild/LESSONS.md`.

## Hard constraints

1. Do not reference or copy `iNKORE.UI.WPF.Modern`, iNKORE XAML, styles, templates, images, or fonts.
2. Do not copy the legacy WPF repository wholesale.
3. `Beacon.Contracts` and `Beacon.Core` must not reference WPF or WinUI UI types.
4. Do not introduce `System.Windows.*` into the new WinUI process.
5. Flow-compatible WPF plugin APIs must be isolated in `Beacon.PluginHost`.
6. Portable ZIP is the primary distribution. Do not make MSIX a prerequisite.
7. Persistent portable data belongs beneath the resolved Beacon `DataRoot` next to the executable unless an approved ADR says otherwise.
8. Do not add a dependency until its need, license, version, and release impact are documented.
9. Do not use `Spotlight` in code, XAML, identifiers, resource keys, logs, filenames, branch names, or UI strings.
10. Record failed approaches in `docs/rebuild/LESSONS.md` before retrying.

## Implementation quality

- Keep changes limited to the active phase.
- Preserve cancellation and asynchronous streaming in search paths.
- Validate all process-boundary inputs.
- Never serialize delegates, UI objects, or arbitrary plugin objects.
- Add tests for contracts, migrations, failure recovery, and changed behavior.
- Summarize files changed, tests run, failures encountered, and remaining risks.
