# Third-Party Notices

Beacon is an independent project. This file records third-party code and assets that are actually included in Beacon source or release artifacts.

## R1 Portable dependencies

The R1 Portable ZIP includes Microsoft Windows App SDK 2.2.0 and the following direct or transitive components:

- Microsoft Windows App SDK packages: Microsoft.WindowsAppSDK 2.2.0, Runtime 2.2.0, WinUI 2.2.1, AI 2.2.3, Base 2.0.4, DWrite 2.1.0, Foundation 2.1.0, InteractiveExperiences 2.0.15, ML 2.1.70, and Widgets 2.0.5.
- Microsoft Windows ML Runtime: Microsoft.Windows.AI.MachineLearning 2.1.70.
- Microsoft Windows SDK build packages recorded by the dependency manifest: Microsoft.Windows.SDK.BuildTools 10.0.26100.4654 and Microsoft.Windows.SDK.BuildTools.MSIX 1.7.251221100.
- Microsoft WebView2 1.0.3719.77.
- MIT-licensed packages: StreamJsonRpc 2.25.29; MessagePack and MessagePack.Annotations 2.5.302; Microsoft.NET.StringTools 18.4.0; Microsoft.VisualStudio.Threading.Only 17.14.15; Microsoft.VisualStudio.Validation 17.13.22; Nerdbank.MessagePack 1.2.4; Nerdbank.Streams 2.13.16; Newtonsoft.Json 13.0.3; PolyType 1.3.1; System.IO.Pipelines 8.0.0; System.Memory 4.6.3; System.Numerics.Tensors 9.0.0; and System.Runtime.CompilerServices.Unsafe 6.1.2.

Microsoft Windows App SDK is licensed under **MICROSOFT SOFTWARE LICENSE TERMS / MICROSOFT WINDOWS APP SDK**, not MIT. Its NuGet license permits redistribution of files binplaced with an application for both framework-dependent and self-contained deployment, subject to its distribution requirements. Microsoft Windows ML Runtime and Microsoft Windows SDK components are governed by their respective Microsoft Software License Terms.

### MIT License

Copyright holders include Microsoft Corporation, Yoshifumi Kawai and contributors, Andrew Arnott, James Newton-King, and the respective package contributors.

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

### Microsoft WebView2 license notice

Copyright (C) Microsoft Corporation. All rights reserved.

Redistribution and use in source and binary forms, with or without modification, are permitted provided that source redistributions retain the copyright notice, conditions, and disclaimer; binary redistributions reproduce them in documentation or other materials; and Microsoft or contributor names are not used to endorse derived products without prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE, ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE.

## Migration policy

## R3 Windows platform components

Beacon includes selectively adapted source derived from Flow Launcher and Wox under the MIT License, plus the x64 Everything SDK client DLL. Migrated source files identify their legacy paths. Microsoft.Windows.CsWin32 0.3.298 is MIT-licensed and runs only as a build-time source generator; its runtime DLL is not distributed.

The Windows Index query guard and ItemUrl conversion are selectively adapted from `Beacon-old/Plugins/Flow.Launcher.Plugin.Explorer/Search/WindowsIndex/WindowsIndex.cs` and `QueryConstructor.cs` (Flow Launcher/Wox, MIT).

System.Data.OleDb 9.0.15 is distributed under the .NET runtime MIT License, copyright .NET Foundation and Contributors.

Flow Launcher and Wox contributors retain their original copyrights. The MIT License text above applies to their migrated portions and Microsoft.Windows.CsWin32.

## R5 standard providers

Beacon includes the following selectively migrated Flow Launcher/Wox MIT-licensed data and parsing logic:

- `src/Beacon.Platform.Windows/Data/WindowsSettings.json` from `Beacon-old/Plugins/Flow.Launcher.Plugin.WindowsSettings/WindowsSettings.json`.
- `src/Beacon.Platform.Windows/Data/WindowsSettings.ja-JP.xml` from `Beacon-old/Plugins/Flow.Launcher.Plugin.WindowsSettings/Properties/Resources.ja-JP.resx` (extension-only rename for raw resource loading).
- Chromium bookmark JSON traversal adapted from `Beacon-old/Plugins/Flow.Launcher.Plugin.BrowserBookmark/ChromiumBookmarkLoader.cs`.
- Firefox bookmark discovery and query adapted from `Beacon-old/Plugins/Flow.Launcher.Plugin.BrowserBookmark/FirefoxBookmarkLoader.cs`; the new implementation uses the Windows inbox `winsqlite3.dll` and adds no package.

Flow Launcher and Wox contributors retain their original copyrights. The MIT License text above applies to these migrated portions.

## R8 fuzzy matching

`src/Beacon.Core/FuzzyMatcher.cs` and `DiacriticsNormalizer.cs` selectively adapt MIT-licensed matching logic from `Beacon-old/Flow.Launcher.Infrastructure/StringMatcher.cs`, `DiacriticsNormalizer.cs`, and `Flow.Launcher.Plugin/SharedModels/MatchResult.cs`. UI, IoC, settings, and Pinyin dependencies were not migrated. Flow Launcher and Wox contributors retain their original copyrights.

## Bundled fonts

Beacon bundles `ZenKakuGothicNew-Regular.ttf`, `ZenKakuGothicNew-Medium.ttf`, and `ZenKakuGothicNew-Bold.ttf` from the Zen Kaku Gothic New project, copyright 2022 The Zen Project Authors. Beacon also bundles `InterVariable.ttf` from the Inter project, copyright 2016 The Inter Project Authors. Both font families are distributed under the SIL Open Font License 1.1; the complete license and copyright notices are included at `fonts/OFL-1.1.txt` and in the Portable output under `Assets/Fonts/OFL-1.1.txt`.

- Zen Kaku Gothic New source: https://github.com/googlefonts/zen-kakugothic
- Inter source: https://github.com/rsms/inter

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
