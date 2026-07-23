# DEPENDENCY_MAP — 依存の事実（Beacon-old）と新Beaconでの採否

本書はA部=Beacon-oldで確認した事実、B部=新Beaconに採用する構成を分ける。新Beaconは独立リポジトリのため、「継続」も新規依存として追加し直す。追加時は必要性・ライセンス原文・バージョン・リリース影響を記録する（SPEC §8）。

# A部: Beacon-oldの依存（Phase R0実測）

## A1. プロジェクト参照グラフ

```text
Flow.Launcher (WPF exe)
 ├─ Flow.Launcher.Core ──┬─ Flow.Launcher.Infrastructure ── Flow.Launcher.Plugin
 │                        └─ Flow.Launcher.Plugin
 ├─ Flow.Launcher.Infrastructure
 └─ Flow.Launcher.Plugin
Plugins/* → Flow.Launcher.Plugin
```

標準プラグインの一部は追加でWPF、WinForms、CsWin32、個別NuGetを参照する。詳細はCOMPATIBILITY.md §2。

## A2. NuGet依存と実参照

| パッケージ | バージョン | 実参照元・用途 | NuGet原文で確認した事項 |
|---|---:|---|---|
| iNKORE.UI.WPF.Modern | 0.10.1 | `Flow.Launcher`と`Plugins/Flow.Launcher.Plugin.WebSearch`のWPF UI | 独自ライセンス。商用は書面許可が必要。新Beaconへ移植禁止 |
| MdXaml群 / VirtualizingWrapPanel / NHotkey.Wpf / SharpVectors.Wpf | 複数 | 旧WPF表示、Markdown、SVG、ホットキー | WPF専用 |
| ChefKeys | 0.1.2 | `Flow.Launcher/Helper/HotKeyMapper.cs`でWinキーを含むホットキーを扱う | nupkgのnuspecは`Apache-2.0`。依存グループは空だが`Microsoft.WindowsDesktop.App.WPF` FrameworkReferenceを明記。NuGetライセンス本文もApache-2.0と照合済み |
| CommunityToolkit.Mvvm | 8.4.0 | `Flow.Launcher`、Infrastructure、複数プラグイン | MIT。UI非依存 |
| Microsoft.Extensions.Hosting / DI | 9.0.9 | `Flow.Launcher`のホスト・DI | MIT |
| Microsoft.Toolkit.Uwp.Notifications | 7.1.3 | 旧通知 | R1でWinAppSDK AppNotificationとの置換可否を確認 |
| TaskScheduler | 2.12.2 | 旧スタートアップ登録 | Portable版はStartupフォルダ`.lnk`を優先 |
| Fody + PropertyChanged.Fody | 複数 | IL weaving | 新BeaconではMvvmソースジェネレータで代替 |
| Flow.Launcher.Localization | 0.0.6 | SourceGenerator/Analyzer。`build/Flow.Launcher.Localization.props`が`Languages/en.xaml`をAdditionalFilesへ追加し、各アセンブリへ`Localize`静的型を生成 | nuspecはMIT。生成物は`string`返却メソッド。Release反射でCore/Infrastructureと8標準プラグインに生成型を確認。入力がWPF ResourceDictionary XAMLであり、WinUIプロジェクトでのビルド・実行可否は未確認・要R1実験 |
| StreamJsonRpc | 2.22.11 | CoreのJsonRPCPluginV2 / ProcessStreamPluginV2 | MIT。旧版で実運用 |
| squirrel.windows | 1.9.0 | 旧更新・インストーラ | NU1701。Portable方針と不整合 |
| Droplex | 1.7.0 | `Core/ExternalPlugins/Environments/PythonEnvironment.cs`でPython埋込環境、`TypeScriptEnvironment.cs`/`TypeScriptV2Environment.cs`でNode環境、Explorerの`EverythingDownloadHelper.cs`でEverythingを展開 | nuspecはMIT |
| FSharp.Core | 9.0.303 | `Flow.Launcher.Core.csproj`の直接PackageReference。C#ソースからFSharp.Core型への参照は0件。`Flow.Launcher.Plugin/AllowedLanguage.cs`はF#プラグインを.NET言語として許可 | nuspecはMIT。直接依存が実行時に必須かはソースだけでは確定不能 |
| InputSimulator | 1.0.4 | Infrastructureの`DialogJump/Models/WindowsDialog.cs`でAlt+D/Enterを送信、Shellの`Main.cs`でキーボード入力を送信 | nuspecにはSPDX expressionがなく、失効したCodePlexのlicense URLのみ。Beacon-old `LICENSE.md`はMITと記録するが、今回のNuGet原文だけでは再検証不能。新規採用しない |
| Meziantou.Framework.Win32.Jobs | 3.4.5 | Coreのプロセス管理 | MIT |
| Microsoft.Windows.CsWin32 | 0.3.298 | Infrastructure、Plugin、Program、ProcessKiller、Sys | MIT・ソースジェネレータ |
| NLog | 6.x | ログ | BSD-3-Clause |
| MemoryPack | 1.21.4 | Programキャッシュ、Infrastructure BinaryStorage | MIT |
| System.Drawing.Common | 7.0.0 | `Infrastructure/Image/ImageLoader.cs:353`の`System.Drawing.Icon.ExtractAssociatedIcon`。ExplorerのShellContextMenuはFramework由来の`System.Drawing`も使用 | nupkgはMIT expressionと`LICENSE.TXT`を同梱 |
| NUnit + Moq | 4.4 / 4.20 | `Flow.Launcher.Test` | テスト基盤 |

## A3. 同梱バイナリ・素材

| 対象 | 実測結果 |
|---|---|
| `Plugins/Flow.Launcher.Plugin.Explorer/EverythingSDK/x64/Everything.dll` | PE Machine `0x8664` = x64 |
| `Plugins/Flow.Launcher.Plugin.Explorer/EverythingSDK/x86/Everything.dll` | PE Machine `0x014C` = x86 |
| Everything ARM64 | フォルダ・csproj・Release出力に存在しない |
| `Flow.Launcher/Resources/SegoeFluentIcons.ttf` | Microsoftフォント。再配布権不明。新Beaconへ同梱しない |
| iNKORE由来のXAML/スタイル/画像/フォント | Flow.LauncherとWebSearchに存在。新Beaconへ移植しない |

# B部: 新Beaconでの採否

## B1. 採用候補（追加時にライセンス原文確認と記録が必要）

| パッケージ | 行き先 | 状態 |
|---|---|---|
| Microsoft.WindowsAppSDK | Beacon.WinUIほか | R1で安定版選定・原文確認後にADR-0002へ記録 |
| StreamJsonRpc | Beacon.WinUI / Beacon.PluginHost / Contracts境界 | 第一候補。R1で確定 |
| Microsoft.Windows.CsWin32 | Beacon.Platform.Windows | 0.3.298をR3で採用。Win32/Shell APIの型安全なソース生成に使用。nupkg nuspecのMIT式とMicrosoft/CsWin32公式LICENSE原文を確認。開発時専用source generator（PrivateAssets=all）で、ランタイムDLLはPortable ZIPへ入らない |
| CommunityToolkit.Mvvm | Beacon.WinUI / Beacon.Core | 採用予定。Fodyは使わない |
| Microsoft.Extensions.Hosting / DI | Beacon.WinUI | 採用予定 |
| NLog | Core / 各exe | DataRoot対応を確認して採用判断 |
| Meziantou.Framework.Win32.Jobs | Beacon.WinUI | PluginHost管理に必要なら採用 |
| NUnit + Moq | tests | 旧版と同じ基盤を継続候補 |
| Everything.dll x64 | Beacon.Platform.Windows | MIT系原文確認済み。x64一次配布で採用候補 |

### R1で採用した直接依存

| パッケージ | バージョン | 用途・配布影響 | NuGet原文で確認した事項 |
|---|---:|---|---|
| Microsoft.WindowsAppSDK | 2.2.0 | Beacon.WinUI。Unpackaged + self-contained WinUIとWindows App SDKランタイムをZIPへ同梱 | nupkg内`license.txt`: **MICROSOFT SOFTWARE LICENSE TERMS / MICROSOFT WINDOWS APP SDK**。MITではない。§3(a)(i)でNuGetがbinplaceしたframework-dependent/self-containedファイルの再頒布を許可。§3(b)の条件あり |
| StreamJsonRpc | 2.25.29 | Beacon.WinUI / Beacon.PluginHostのNamed Pipe JSON-RPC。推移依存を下表のとおり同梱 | nuspec SPDX `MIT`、公式リポジトリ[`microsoft/vs-streamjsonrpc`](https://github.com/microsoft/vs-streamjsonrpc)のLICENSE原文と一致 |
| System.Data.OleDb | 9.0.15 | Beacon.Platform.Windows。Everything不可時にOSのWindows Search IndexへSQL接続するため必要。Portable ZIPへmanaged assemblyを同梱し、OS標準Search.CollatorDSO providerを利用 | NuGet公式ページのMIT表記とdotnet/runtime公式`LICENSE.TXT`（.NET Foundation and Contributors、MIT原文）を照合済み |

### R1 Portableの推移依存（`Beacon.Next.deps.json`実測）

版数は2026-07-17生成の`artifacts/portable/Beacon/Beacon.Next.deps.json`を正とする。MIT表記は各nupkgのnuspec SPDX式と、nuspec `repository` が指す公式リポジトリのLICENSE原文を照合した。Microsoft独自条項は各nupkg内のライセンス本文を開いて確認した。

| パッケージ | バージョン | 経路・用途 | ライセンス原文確認 |
|---|---:|---|---|
| MessagePack / MessagePack.Annotations | 2.5.302 | StreamJsonRpcのシリアライズ依存 | MIT（MessagePack-CSharp公式LICENSE） |
| Microsoft.NET.StringTools | 18.4.0 | MessagePackの推移依存 | MIT（dotnet/msbuild公式LICENSE。nupkg内ThirdPartyNoticesも確認） |
| Microsoft.VisualStudio.Threading.Only | 17.14.15 | StreamJsonRpc / Nerdbank.Streamsの非同期処理 | MIT（microsoft/vs-threading公式LICENSE）。このパッケージの推移的WPF FrameworkReferenceはB1でRuntimePackコピーのみ除外 |
| Microsoft.VisualStudio.Validation | 17.13.22 | Microsoft.VisualStudio.Threadingの入力検証 | MIT（microsoft/vs-validation公式LICENSE） |
| Nerdbank.MessagePack | 1.2.4 | StreamJsonRpcのMessagePack formatter | MIT（AArnott/Nerdbank.MessagePack公式LICENSE） |
| Nerdbank.Streams | 2.13.16 | StreamJsonRpcのストリーム処理 | MIT（AArnott/Nerdbank.Streams公式LICENSE） |
| Newtonsoft.Json | 13.0.3 | StreamJsonRpcのJSON formatter | MIT（nupkg内`LICENSE.md`原文） |
| PolyType | 1.3.1 | Nerdbank.MessagePackの型メタデータ | MIT（eiriktsarpalis/PolyType公式LICENSE） |
| System.IO.Pipelines | 8.0.0 | StreamJsonRpcのPipe処理 | MIT（nupkg内`LICENSE.TXT`原文） |
| System.Memory | 4.6.3 | Microsoft.NET.StringToolsの推移依存 | MIT（dotnet/maintenance-packages公式LICENSE） |
| System.Numerics.Tensors | 9.0.0 | Windows App SDK AI系列の推移依存 | MIT（nupkg内`LICENSE.TXT`原文） |
| System.Runtime.CompilerServices.Unsafe | 6.1.2 | System.Memoryの推移依存 | MIT（dotnet/maintenance-packages公式LICENSE） |
| Microsoft.Web.WebView2 | 1.0.3719.77 | Windows App SDK WinUIの推移依存 | nupkg内`LICENSE.txt`: BSD-3-Clause相当。binary再頒布時はcopyright・条件・免責の再掲が必要 |
| Microsoft.WindowsAppSDK.AI | 2.2.3 | Windows App SDK集約パッケージの推移依存 | nupkg内`license.txt`: Microsoft Software License Terms |
| Microsoft.WindowsAppSDK.Base | 2.0.4 | Windows App SDK基盤 | 同上 |
| Microsoft.WindowsAppSDK.DWrite | 2.1.0 | DWrite基盤 | 同上 |
| Microsoft.WindowsAppSDK.Foundation | 2.1.0 | AppLifecycle / Notifications等のprojection | 同上 |
| Microsoft.WindowsAppSDK.InteractiveExperiences | 2.0.15 | interactive experiences projection | 同上 |
| Microsoft.WindowsAppSDK.ML | 2.1.70 | Windows ML集約依存 | nupkg内`license.txt`: Microsoft Software License Terms。ThirdPartyNoticesも確認 |
| Microsoft.WindowsAppSDK.Runtime | 2.2.0 | self-contained Windows App SDK runtime | **MICROSOFT SOFTWARE LICENSE TERMS / MICROSOFT WINDOWS APP SDK**。binplace済みファイルは再頒布可 |
| Microsoft.WindowsAppSDK.Widgets | 2.0.5 | Widgets projection | nupkg内`license.txt`: Microsoft Software License Terms |
| Microsoft.WindowsAppSDK.WinUI | 2.2.1 | WinUI runtime / projection | nupkg内`license.txt`: Microsoft Software License Terms |
| Microsoft.Windows.AI.MachineLearning | 2.1.70 | Windows App SDK MLの推移依存 | nupkg内`license.txt`: **Microsoft Windows ML Runtime**。WindowsAppSDKがbinplaceしたファイルの再頒布を許可。ThirdPartyNoticesも確認 |
| Microsoft.Windows.SDK.BuildTools | 10.0.26100.4654 | Windows SDK build/publish資産。deps記載 | nupkgのlicense URLとSDK同梱条項を**Microsoft Windows Software Development Kit**条項で確認 |
| Microsoft.Windows.SDK.BuildTools.MSIX | 1.7.251221100 | MSIX build資産。deps記載 | nupkg内`sdk_license.txt`: Microsoft Windows Software Development Kit条項 |

表示義務はリリースへ同梱する`attribution.md`へ反映した。パッケージ追加・更新は行っていない。

### R8で採用した移植ソース

新規パッケージ依存はない。`src/Beacon.Core/FuzzyMatcher.cs` と `DiacriticsNormalizer.cs` は `Beacon-old/Flow.Launcher.Infrastructure/StringMatcher.cs`、`DiacriticsNormalizer.cs`、`Flow.Launcher.Plugin/SharedModels/MatchResult.cs` の検索ロジックをUI・IoC・設定・Pinyin依存なしでアダプトした。Flow Launcher / WoxのMIT表記を各ファイル冒頭と `attribution.md` に維持する。

## B2. 不採用

| 対象 | 理由 |
|---|---|
| iNKORE.UI.WPF.Modern（iNKORE.UI.WPF、Fizzlerを含む旧UI依存） | 独自ライセンス・商用制限。Gate Dで不在を監査 |
| NHotkey.Wpf / SharpVectors.Wpf / MdXaml群 / VirtualizingWrapPanel | WPF専用 |
| ChefKeys | Apache-2.0だがWPF FrameworkReference必須。GlobalHotkey/CsWin32方式が既にあり、新Beaconへ追加する必要がない |
| InputSimulator 1.0.4 | .NET Framework時代のNU1701依存で、NuGet原文からライセンスを再検証できない。必要な入力はCsWin32で実装する |
| squirrel.windows | Portable方針と不整合・老朽。更新はDISTRIBUTION.mdの方式で新規実装 |
| Fody / PropertyChanged.Fody | Mvvmソースジェネレータで代替 |
| `SegoeFluentIcons.ttf` | 再配布権不明。OSフォント参照のみ |
| Everything.dll x86 | 初回リリースはx64。x86配布要件なし |
| Droplex | ランタイム/Everything自動展開は初回Portable配布の責務に含めず、必要ファイルを配布物へ固定する |
| FSharp.Core | 新Beacon側でF#プラグイン実績が確認されるまで追加しない。先回り依存を避ける |
| System.Drawing.Common | 旧用途はアイコン抽出1箇所。Beacon.Platform.WindowsのShell API/CsWin32経路で置換する |

## B3. 保留

| 対象 | 判断材料 |
|---|---|
| Flow.Launcher.Localization | R1の最小WinUIプロジェクトで`Languages/en.xaml`入力、生成、実行時翻訳切替を実験。失敗時はWinUI標準resw/resxへ切替 |
| Microsoft.Toolkit.Uwp.Notifications | WinAppSDK AppNotificationで代替できるかR1で確認 |
| TaskScheduler | 管理者起動の明示要件が出た場合だけ再検討 |
| ToolGood.Words.Pinyin / BitFaster.Caching / MemoryPack | 移植対象ロジックが実際に要求した時点で追加判断 |
| Everything ARM64 | Beacon-oldにバイナリなし。ARM64配布を決めた後に公式SDK原文と実機で確認 |

## B4. 原則

- 数行で書けるものにパッケージを足さない。トレイ、ホットキー、単一インスタンスはCsWin32と標準ライブラリを第一候補にする
- Beacon-oldに存在することは採用理由にならない
- 追加のたびにB1へ必要性、バージョン、配布影響を追記し、表示義務があれば`attribution.md`と第三者ライセンス本文を更新する
