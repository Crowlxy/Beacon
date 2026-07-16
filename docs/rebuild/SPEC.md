# Beacon rebuild specification

## Goal

Build an independent WinUI 3 Windows launcher that opens as a search bar, expands results while typing, and provides fast access to applications, files, folders, settings, and actions.

## Approved product requirements

- WinUI 3 interface built specifically for Beacon
- Portable ZIP as the first and primary distribution
- Unpackaged, self-contained Windows App SDK deployment
- Immediate keyboard focus after the global hotkey
- Collapsed search-bar-only empty state
- Results expand beneath the query in the same panel
- Maximum of eight visible results as the initial target
- System, light, and dark appearance modes only
- Everything integration with a Windows search fallback
- Local-first history and ranking
- Flow-compatible plugin search/action support through a separate PluginHost
- Japanese IME and mixed-DPI multi-monitor support

## Non-goals for the first release

- Migrating the legacy WPF UI or theme system
- Shipping iNKORE UI components or resources
- Full support for WPF plugin setting panels and custom previews
- AI search or cloud processing
- Microsoft Store or MSIX as a release blocker
- User-created visual themes

## UI-independent boundary

Contracts and core logic must not reference WPF or WinUI UI types. Search results crossing a process boundary contain serializable data and execution tokens, never delegates, arbitrary objects, controls, or image-source objects.

## Compatibility

Flow Launcher and Wox-derived logic may be migrated only with applicable copyright and license notices. Legacy plugin APIs that require WPF remain isolated in the PluginHost process.

## Naming rule

Do not use `Spotlight` in source code, XAML, identifiers, resources, filenames, logs, branches, or user-visible strings. It may be used only in design discussion documents as a UX reference.
