# Beacon architecture

## Target projects

```text
src/
├─ Beacon.Contracts/          Serializable DTOs, enums, and interfaces
├─ Beacon.Core/               Query orchestration, ranking, history, actions
├─ Beacon.Platform.Windows/   Everything, Shell, Win32, app and file discovery
├─ Beacon.PluginHost/         Isolated Flow-compatible plugin runtime
├─ Beacon.WinUI/              WinUI 3 application and view models
└─ Beacon.Distribution/       Portable publish, ZIP, manifests, updater support

tests/
├─ Beacon.Core.Tests/
├─ Beacon.Platform.Windows.Tests/
├─ Beacon.PluginHost.Tests/
└─ Beacon.IntegrationTests/
```

The exact project set is confirmed in Phase R1. Do not generate all projects merely to match this diagram.

## Dependency direction

```text
Beacon.WinUI ───────────────┐
Beacon.PluginHost ──────────┼─> Beacon.Contracts
Beacon.Platform.Windows ────┤
Beacon.Core ────────────────┘

Beacon.WinUI -> Beacon.Core
Beacon.WinUI -> Beacon.Platform.Windows
Beacon.PluginHost -> legacy compatibility assemblies
```

`Beacon.Contracts` and `Beacon.Core` must not reference UI frameworks.

## Process model

```text
Beacon.exe
   │ versioned JSON-RPC over Named Pipe
   ▼
Beacon.PluginHost.exe
   └─ Flow-compatible plugins
```

The PluginHost isolates WPF dependencies, plugin crashes, timeouts, and incompatible custom UI. Search results and execution requests use versioned serializable contracts.

## Result contract principles

- Data only
- No delegates
- No arbitrary `object` payloads
- No WPF or WinUI image/control types
- Execution through a validated token
- Cancellation and incremental result delivery
- Contract version negotiation

## Windows services

Windows-specific behavior is exposed through services rather than implemented in views:

- Global hotkey
- Tray icon
- Single instance
- AppWindow placement and activation
- Shell icons and thumbnails
- Everything and Windows search
- Startup integration
- File and process actions
- Portable DataRoot resolution

## Legacy boundary

`Crowlxy/Beacon-old` is a migration reference. New code should be copied selectively only after classifying it as reuse, adapt, isolate, rewrite, or retire. The new repository must not take a project-level dependency on Beacon-old.