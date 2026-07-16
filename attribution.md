# Third-Party Notices

Beacon is an independent project. This file records third-party code and assets that are actually included in Beacon source or release artifacts.

## Current repository state

The repository currently contains only project documentation and configuration. No Flow Launcher, Wox, Everything, iNKORE, or Windows App SDK binaries or source files have been imported yet.

## Migration policy

When code is migrated from the legacy implementation, retain all applicable copyright and license notices. At minimum, review:

- Flow Launcher — MIT License
- Wox — MIT License
- Everything SDK — applicable permissive notices
- Windows App SDK and all added NuGet packages

## Prohibited carry-over

The new WinUI 3 product must not copy or distribute:

- `iNKORE.UI.WPF.Modern` binaries
- iNKORE XAML, styles, templates, images, or fonts
- `SegoeFluentIcons.ttf` from the legacy repository
- Apple-specific assets such as SF Symbols

Before every release, compare this file and `LICENSE` against the actual Portable ZIP contents. A dependency that exists only in `Beacon-old` must not be listed as though it ships with the new Beacon.