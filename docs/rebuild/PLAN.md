# Beacon rebuild plan

## Gates

- **Gate A** — Portable WinUI 3 technical viability
- **Gate B** — Daily-usable launcher MVP
- **Gate C** — Flow plugin compatibility boundary
- **Gate D** — Portable release approval

## Phase R0 — Audit and rebuild boundary

- Verify Beacon-old project and package graph
- Classify reusable logic and WPF leakage
- Map standard plugins
- Capture current behavior and release artifacts
- Produce audit, dependency map, compatibility table, and initial ADRs

## Phase R1 — Portable technical spike

- Unpackaged WinUI 3 launch
- Self-contained publish
- Clean-machine ZIP execution
- Global hotkey, tray, single instance, and hidden startup
- Dummy PluginHost launch and Named Pipe communication
- Executable-adjacent DataRoot
- Folder relocation and read-only-folder behavior

**Gate A:** proceed only after these are proven on a real release build.

## Phase R2 — Contracts and core boundary

Create versioned serializable search, result, icon, execution, cancellation, and provider contracts with no UI framework references.

## Phase R3 — Windows platform services

Extract or adapt Everything, Windows search, application discovery, Shell icons, process actions, context, data, and logging.

## Phase R4 — WinUI launcher MVP

Implement the collapsed search bar, expanding results, keyboard interaction, IME, themes, DPI, app/file/folder search, calculator, and URL handling.

**Gate B:** approve only when the new app is usable without starting Beacon-old.

## Phase R5 — Desktop integration and stability

Harden startup, tray, activation, focus, monitors, sleep/resume, crash recovery, logging, and performance.

## Phase R6 — Standard providers and ranking

Migrate the most useful providers, unified ranking, history, cancellation, and slow-provider isolation.

## Phase R7 — Flow-compatible PluginHost

Support third-party search results and actions through an isolated host. Clearly report unsupported WPF settings and previews.

**Gate C:** approve the actual compatibility scope.

## Phase R8 — Beacon-specific UX

Add browse modes, query scopes, actions, quick keys, parameter entry, clipboard history, personalization, and a new preview contract.

## Phase R9 — Settings and legacy migration

Import eligible settings, history, and plugins from `%APPDATA%\Beacon` into the portable DataRoot with backup and rollback.

## Phase R10 — Portable release

Build reproducible x64 ZIP artifacts, updater or manual-update flow, rollback, SBOM, license audit, Windows integration cleanup, and iNKORE absence checks.

**Gate D:** approve the first official portable release.

## Phase R11 — Optional MSIX and Store distribution

Consider only after the portable release. This phase must not block or complicate the first release.