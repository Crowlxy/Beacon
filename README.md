# Beacon

Beacon is an independent, portable Windows launcher being rebuilt with **WinUI 3**.

The project focuses on a fast, keyboard-first search experience for applications, files, folders, settings, and actions. The first distribution target is a **self-contained portable ZIP** that runs without an installer or a preinstalled Windows App SDK runtime.

## Project status

Beacon is currently in the architecture and technical-spike stage. The previous WPF implementation is preserved separately in [`Crowlxy/Beacon-old`](https://github.com/Crowlxy/Beacon-old) and is used only as a migration and compatibility reference.

The new repository must not copy or depend on iNKORE UI resources. Search and plugin-compatibility components may be migrated from Flow Launcher and Wox under their original licenses and attribution requirements.

## Product direction

- WinUI 3 interface designed specifically for Beacon
- Portable-first distribution
- Application, file, folder, setting, and action search
- Everything integration with Windows Search fallback
- Flow Launcher plugin search/action compatibility through an isolated plugin host
- System, light, and dark appearance modes
- Local-first history, actions, clipboard history, and ranking

## Repository structure

```text
src/                 Product source projects
tests/               Automated and integration tests
docs/rebuild/        Formal specification and rebuild plan
docs/rebuild/adr/    Architecture decision records
```

The source projects are intentionally not generated until the Phase R1 technical spike confirms the WinUI 3 unpackaged, self-contained, portable architecture.

## Development workflow

- `main` must remain usable and documented.
- Work is performed on `feature/rebuild-rN-*` branches.
- Each phase is merged through a pull request.
- Claude owns specification, planning, and gate review.
- Codex owns implementation and focused verification.
- Read `CLAUDE.md`, `AGENTS.md`, and `docs/rebuild/LESSONS.md` before changing the project.

## License

Beacon is licensed under the MIT License. Components migrated from Flow Launcher, Wox, Everything, or other third parties retain their original copyright and license notices. See `LICENSE` and `attribution.md`.