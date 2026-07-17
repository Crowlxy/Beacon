# AUDIT — Beacon-old 現状監査（Phase R0、2026-07-16実測）

本書はA部=Beacon-oldで確認した事実、B部=新Beaconに採用済みの構成を分ける。調査対象は`C:\Users\ha.takaku\Desktop\Project\Beacon-old`。コード移植や旧リポジトリのソース変更は行っていない。

# A部: Beacon-oldで確認した事実

## A1. ソリューション構成

`Flow.Launcher.sln` / .NET 9 / 17プロジェクト。

| プロジェクト | 種別 | UI依存 | 備考 |
|---|---|---|---|
| Flow.Launcher | WinExe | WPF | AssemblyName=Beacon、`Output/Debug/Beacon.exe` |
| Flow.Launcher.Core | Library | WinForms有効 | プラグイン基盤・更新 |
| Flow.Launcher.Infrastructure | Library | WPF | NHotkey.Wpf、SharpVectors.Wpf、ImageSource、Win32 |
| Flow.Launcher.Plugin | Library | WPF | 公開互換API |
| Plugins/* ×12 | Library | 個別に確認済み | 詳細はA9とCOMPATIBILITY.md §2 |
| Flow.Launcher.Test | NUnit 4.4 + Moq | WPF本体参照 | 328テスト |

## A2. Flow.Launcher.Plugin公開APIへのWPF型漏れ（全抽出）

`Flow.Launcher.Plugin.csproj`自体が`UseWPF=true`。C#全ファイルを`System.Windows`とWPF型名で検索し、公開シグネチャに現れる箇所を次のとおり確定した。

| ファイル | 公開API | 漏れるWPF型 |
|---|---|---|
| `Flow.Launcher.Plugin/ActionContext.cs:46` | `ActionContext.ToModifierKeys()` | `System.Windows.Input.ModifierKeys` |
| `Flow.Launcher.Plugin/EventHandler.cs:25` | `ResultItemDropEventHandler(Result, IDataObject, DragEventArgs)` | `System.Windows.IDataObject`, `System.Windows.DragEventArgs` |
| `Flow.Launcher.Plugin/EventHandler.cs:64-74` | `FlowLauncherKeyDownEventArgs.keyEventArgs` | `System.Windows.Input.KeyEventArgs` |
| `Flow.Launcher.Plugin/Result.cs:153` | `Result.IconDelegate` | 戻り値`System.Windows.Media.ImageSource`。`Icon`、`BadgeIcon`、`PreviewInfo.PreviewDelegate`で公開 |
| `Flow.Launcher.Plugin/Result.cs:272` | `Result.PreviewPanel` | `Lazy<System.Windows.Controls.UserControl>` |
| `Flow.Launcher.Plugin/Interfaces/ISettingProvider.cs:14` | `CreateSettingPanel()` | 戻り値`System.Windows.Controls.Control` |
| `Flow.Launcher.Plugin/Interfaces/IPublicAPI.cs:442` | `ShowMsgBox(...)` | `MessageBoxResult`, `MessageBoxButton`, `MessageBoxImage` |
| `Flow.Launcher.Plugin/Interfaces/IPublicAPI.cs:537` | `LoadImageAsync(...)` | `ValueTask<ImageSource>` |
| `Flow.Launcher.Plugin/SharedCommands/FilesFolders.cs:27,84,117,283,314` | `CopyAll`、`VerifyBothFolderFilesEqual`、`RemoveFolderIfExists`、`OpenPath`、`OpenFile` | 引数`Func<string, MessageBoxResult>` |
| `Flow.Launcher.Plugin/SharedModels/MonitorInfo.cs:156,164` | `Bounds`、`WorkingArea` | `System.Windows.Rect` |

`Result.Action` / `AsyncAction`はWPF型ではないがプロセス内デリゲート、`ContextData`は任意`object`でありRPC越しに渡せない。`Title`、`SubTitle`、`Score`、`CopyText`、`AutoCompleteText`、`RecordKey`、文字列アイコンパスなどのデータ部分だけをDTO写像対象にする。旧`Flow.Launcher.Plugin.dll`はWinUI本体へロードせず、PluginHost内の互換層として扱う。

## A3. 移植候補資産の所在

| 資産 | Beacon-old内パス | 実測 | 判定 |
|---|---|---|---|
| ポータブルデータ | `Flow.Launcher.Infrastructure/UserSettings/DataLocation.cs` | exe隣接`UserData`と`.dead` | 方式参考、DataRootResolverは書き直し |
| JSON-RPC基盤 | `Flow.Launcher.Core/Plugin/JsonRPCPluginV2.cs`; `ProcessStreamPluginV2.cs`; `JsonRPCV2Models/JsonRPCExecuteResponse.cs`; `JsonRPCPublicAPI.cs`; `JsonRPCQueryRequest.cs` | StreamJsonRpc 2.22.11 | PluginHostへアダプト |
| 低レベルホットキー | `Flow.Launcher.Infrastructure/Hotkey/GlobalHotkey.cs` | CsWin32 / WH_KEYBOARD_LL、UI型なし | Windows層へアダプト |
| WPFホットキー | `Flow.Launcher/Helper/HotKeyMapper.cs` | NHotkey.Wpf + ChefKeys | 書き直し |
| 単一インスタンス | `Flow.Launcher/Helper/SingleInstance.cs` | Mutex + NamedPipe、WPF Application前提 | 方式参考、書き直し |
| Everything | `Plugins/Flow.Launcher.Plugin.Explorer/Search/Everything/*` | P/Invoke層。x64/x86 DLLあり | Windows層へ選択移植 |
| 画像・サムネイル | `Flow.Launcher.Infrastructure/Image/ImageLoader.cs`; `ThumbnailReader.cs` | WPF ImageSource/BitmapSource返却 | Shell取得ロジックだけアダプト |
| Win32統合 | `Flow.Launcher.Infrastructure/Win32Helper.cs` | CsWin32とWPF Window/HwndSourceが同居 | A10の単位で分割 |
| DialogJump | `Flow.Launcher.Infrastructure/DialogJump/*` | HWND/COM中心、DispatcherTimerとUIデリゲートあり | Windows層へアダプト、初回要件外 |
| 検索・一致 | `Flow.Launcher.Infrastructure/StringMatcher.cs`; `DiacriticsNormalizer.cs`; `PinyinAlphabet.cs` | UI型なし | Coreへアダプト |
| 履歴・スコア | `Flow.Launcher/Storage/*` | JSON、ただしApp/APIと旧Resultへ結合 | Coreモデルとして書き直し寄りのアダプト |

## A4. 配布・ビルド・テストの現状

- squirrel.windows 1.9.0と`Scripts/post_build.ps1`を使用。
- Self-contained publish profile `Net9.0-SelfContained.pubxml`あり。
- Portable ZIP生成は`FlowLauncher`を作り`Beacon`を圧縮するパス不一致があり、新Beaconへ移植しない。
- 2026-07-16 Debugベースライン: `dotnet build Flow.Launcher.sln`、37.66秒、MSBuild集計2743警告、0エラー。単純grepは警告再掲を含み5486行。
- 2026-07-16 Release: `dotnet build Flow.Launcher.sln -c Release`、20.60秒、159警告、0エラー。
- 2026-07-16 test: `dotnet test Flow.Launcher.sln --no-build`、328合格、0失敗、0スキップ、約5秒。
- ビルド前に`Output/Debug`を使用するBeaconプロセスがないことを確認した。

### Output/Release DLL一覧

相対パス、重複パス除外後`236`件。名称が同じでも配置先が異なるものは別行で残す。

```text
Beacon.dll
Ben.Demystifier.dll
BitFaster.Caching.dll
ChefKeys.dll
CommunityToolkit.Mvvm.dll
DeltaCompressionDotNet.dll
DeltaCompressionDotNet.MsDelta.dll
DeltaCompressionDotNet.PatchApi.dll
Droplex.dll
EverythingSDK\x64\Everything.dll
EverythingSDK\x86\Everything.dll
Fizzler.dll
Flow.Launcher.Core.dll
Flow.Launcher.Infrastructure.dll
Flow.Launcher.Localization.Attributes.dll
Flow.Launcher.Plugin.dll
FSharp.Core.dll
HtmlAgilityPack.dll
ICSharpCode.AvalonEdit.dll
INIFileParser.dll
iNKORE.UI.WPF.dll
iNKORE.UI.WPF.Modern.Controls.dll
iNKORE.UI.WPF.Modern.dll
JetBrains.Annotations.dll
MdXaml.AnimatedGif.dll
MdXaml.dll
MdXaml.Html.dll
MdXaml.Plugins.dll
MdXaml.Svg.dll
MemoryPack.Core.dll
MessagePack.Annotations.dll
MessagePack.dll
Meziantou.Framework.Win32.Jobs.dll
Microsoft.Extensions.Configuration.Abstractions.dll
Microsoft.Extensions.Configuration.Binder.dll
Microsoft.Extensions.Configuration.CommandLine.dll
Microsoft.Extensions.Configuration.dll
Microsoft.Extensions.Configuration.EnvironmentVariables.dll
Microsoft.Extensions.Configuration.FileExtensions.dll
Microsoft.Extensions.Configuration.Json.dll
Microsoft.Extensions.Configuration.UserSecrets.dll
Microsoft.Extensions.DependencyInjection.Abstractions.dll
Microsoft.Extensions.DependencyInjection.dll
Microsoft.Extensions.Diagnostics.Abstractions.dll
Microsoft.Extensions.Diagnostics.dll
Microsoft.Extensions.FileProviders.Abstractions.dll
Microsoft.Extensions.FileProviders.Physical.dll
Microsoft.Extensions.FileSystemGlobbing.dll
Microsoft.Extensions.Hosting.Abstractions.dll
Microsoft.Extensions.Hosting.dll
Microsoft.Extensions.Logging.Abstractions.dll
Microsoft.Extensions.Logging.Configuration.dll
Microsoft.Extensions.Logging.Console.dll
Microsoft.Extensions.Logging.Debug.dll
Microsoft.Extensions.Logging.dll
Microsoft.Extensions.Logging.EventLog.dll
Microsoft.Extensions.Logging.EventSource.dll
Microsoft.Extensions.Options.ConfigurationExtensions.dll
Microsoft.Extensions.Options.dll
Microsoft.Extensions.Primitives.dll
Microsoft.IO.RecyclableMemoryStream.dll
Microsoft.NET.StringTools.dll
Microsoft.Toolkit.Uwp.Notifications.dll
Microsoft.VisualStudio.Threading.dll
Microsoft.VisualStudio.Validation.dll
Microsoft.Win32.TaskScheduler.dll
Microsoft.Windows.SDK.NET.dll
Mono.Cecil.dll
Mono.Cecil.Mdb.dll
Mono.Cecil.Pdb.dll
Mono.Cecil.Rocks.dll
Nerdbank.Streams.dll
Newtonsoft.Json.dll
NHotkey.dll
NHotkey.Wpf.dll
NLog.dll
NLog.OutputDebugString.dll
NuGet.Squirrel.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\Acornima.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\CommunityToolkit.Mvvm.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\ExCSS.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\Flow.Launcher.Localization.Attributes.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\Flow.Launcher.Plugin.BrowserBookmark.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\HarfBuzzSharp.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\JetBrains.Annotations.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\Jint.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\Microsoft.Data.Sqlite.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\Microsoft.Windows.SDK.NET.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\runtimes\win-x64\native\e_sqlite3.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\runtimes\win-x64\native\libHarfBuzzSharp.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\runtimes\win-x64\native\libSkiaSharp.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\runtimes\win-x86\native\e_sqlite3.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\runtimes\win-x86\native\libHarfBuzzSharp.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\runtimes\win-x86\native\libSkiaSharp.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\ShimSkiaSharp.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\SkiaSharp.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\SQLitePCLRaw.batteries_v2.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\SQLitePCLRaw.core.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\SQLitePCLRaw.provider.e_sqlite3.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\Svg.Animation.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\Svg.Custom.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\Svg.JavaScript.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\Svg.Model.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\Svg.SceneGraph.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\Svg.Skia.dll
Plugins\Flow.Launcher.Plugin.BrowserBookmark\WinRT.Runtime.dll
Plugins\Flow.Launcher.Plugin.Calculator\Flow.Launcher.Localization.Attributes.dll
Plugins\Flow.Launcher.Plugin.Calculator\Flow.Launcher.Plugin.Calculator.dll
Plugins\Flow.Launcher.Plugin.Calculator\JetBrains.Annotations.dll
Plugins\Flow.Launcher.Plugin.Calculator\Mages.Core.dll
Plugins\Flow.Launcher.Plugin.Calculator\Microsoft.Windows.SDK.NET.dll
Plugins\Flow.Launcher.Plugin.Calculator\WinRT.Runtime.dll
Plugins\Flow.Launcher.Plugin.Explorer\CommunityToolkit.Mvvm.dll
Plugins\Flow.Launcher.Plugin.Explorer\Droplex.dll
Plugins\Flow.Launcher.Plugin.Explorer\EverythingSDK\x64\Everything.dll
Plugins\Flow.Launcher.Plugin.Explorer\EverythingSDK\x86\Everything.dll
Plugins\Flow.Launcher.Plugin.Explorer\Flow.Launcher.Localization.Attributes.dll
Plugins\Flow.Launcher.Plugin.Explorer\Flow.Launcher.Plugin.Explorer.dll
Plugins\Flow.Launcher.Plugin.Explorer\JetBrains.Annotations.dll
Plugins\Flow.Launcher.Plugin.Explorer\Microsoft.Search.Interop.dll
Plugins\Flow.Launcher.Plugin.Explorer\Microsoft.Windows.SDK.NET.dll
Plugins\Flow.Launcher.Plugin.Explorer\runtimes\win\lib\net9.0\System.Data.OleDb.dll
Plugins\Flow.Launcher.Plugin.Explorer\System.Data.OleDb.dll
Plugins\Flow.Launcher.Plugin.Explorer\System.Linq.Async.dll
Plugins\Flow.Launcher.Plugin.Explorer\WinRT.Runtime.dll
Plugins\Flow.Launcher.Plugin.Explorer\YamlDotNet.dll
Plugins\Flow.Launcher.Plugin.PluginIndicator\Flow.Launcher.Localization.Attributes.dll
Plugins\Flow.Launcher.Plugin.PluginIndicator\Flow.Launcher.Plugin.PluginIndicator.dll
Plugins\Flow.Launcher.Plugin.PluginIndicator\JetBrains.Annotations.dll
Plugins\Flow.Launcher.Plugin.PluginIndicator\Microsoft.Windows.SDK.NET.dll
Plugins\Flow.Launcher.Plugin.PluginIndicator\WinRT.Runtime.dll
Plugins\Flow.Launcher.Plugin.PluginsManager\Flow.Launcher.Plugin.PluginsManager.dll
Plugins\Flow.Launcher.Plugin.PluginsManager\ICSharpCode.SharpZipLib.dll
Plugins\Flow.Launcher.Plugin.PluginsManager\JetBrains.Annotations.dll
Plugins\Flow.Launcher.Plugin.PluginsManager\Microsoft.Windows.SDK.NET.dll
Plugins\Flow.Launcher.Plugin.PluginsManager\WinRT.Runtime.dll
Plugins\Flow.Launcher.Plugin.ProcessKiller\Flow.Launcher.Localization.Attributes.dll
Plugins\Flow.Launcher.Plugin.ProcessKiller\Flow.Launcher.Plugin.ProcessKiller.dll
Plugins\Flow.Launcher.Plugin.ProcessKiller\JetBrains.Annotations.dll
Plugins\Flow.Launcher.Plugin.ProcessKiller\Microsoft.Windows.SDK.NET.dll
Plugins\Flow.Launcher.Plugin.ProcessKiller\WinRT.Runtime.dll
Plugins\Flow.Launcher.Plugin.Program\Flow.Launcher.Plugin.Program.dll
Plugins\Flow.Launcher.Plugin.Program\INIFileParser.dll
Plugins\Flow.Launcher.Plugin.Program\JetBrains.Annotations.dll
Plugins\Flow.Launcher.Plugin.Program\MemoryPack.Core.dll
Plugins\Flow.Launcher.Plugin.Program\Microsoft.Extensions.Caching.Abstractions.dll
Plugins\Flow.Launcher.Plugin.Program\Microsoft.Extensions.Caching.Memory.dll
Plugins\Flow.Launcher.Plugin.Program\Microsoft.Extensions.DependencyInjection.Abstractions.dll
Plugins\Flow.Launcher.Plugin.Program\Microsoft.Extensions.Logging.Abstractions.dll
Plugins\Flow.Launcher.Plugin.Program\Microsoft.Extensions.Options.dll
Plugins\Flow.Launcher.Plugin.Program\Microsoft.Extensions.Primitives.dll
Plugins\Flow.Launcher.Plugin.Program\Microsoft.Windows.SDK.NET.dll
Plugins\Flow.Launcher.Plugin.Program\NLog.dll
Plugins\Flow.Launcher.Plugin.Program\WinRT.Runtime.dll
Plugins\Flow.Launcher.Plugin.Shell\Flow.Launcher.Localization.Attributes.dll
Plugins\Flow.Launcher.Plugin.Shell\Flow.Launcher.Plugin.Shell.dll
Plugins\Flow.Launcher.Plugin.Shell\JetBrains.Annotations.dll
Plugins\Flow.Launcher.Plugin.Shell\Microsoft.Windows.SDK.NET.dll
Plugins\Flow.Launcher.Plugin.Shell\WindowsInput.dll
Plugins\Flow.Launcher.Plugin.Shell\WinRT.Runtime.dll
Plugins\Flow.Launcher.Plugin.Sys\Flow.Launcher.Localization.Attributes.dll
Plugins\Flow.Launcher.Plugin.Sys\Flow.Launcher.Plugin.Sys.dll
Plugins\Flow.Launcher.Plugin.Sys\JetBrains.Annotations.dll
Plugins\Flow.Launcher.Plugin.Sys\Microsoft.Windows.SDK.NET.dll
Plugins\Flow.Launcher.Plugin.Sys\WinRT.Runtime.dll
Plugins\Flow.Launcher.Plugin.Url\Flow.Launcher.Localization.Attributes.dll
Plugins\Flow.Launcher.Plugin.Url\Flow.Launcher.Plugin.Url.dll
Plugins\Flow.Launcher.Plugin.Url\JetBrains.Annotations.dll
Plugins\Flow.Launcher.Plugin.Url\Microsoft.Windows.SDK.NET.dll
Plugins\Flow.Launcher.Plugin.Url\WinRT.Runtime.dll
Plugins\Flow.Launcher.Plugin.WebSearch\Flow.Launcher.Plugin.WebSearch.dll
Plugins\Flow.Launcher.Plugin.WebSearch\iNKORE.UI.WPF.dll
Plugins\Flow.Launcher.Plugin.WebSearch\iNKORE.UI.WPF.Modern.Controls.dll
Plugins\Flow.Launcher.Plugin.WebSearch\iNKORE.UI.WPF.Modern.dll
Plugins\Flow.Launcher.Plugin.WebSearch\JetBrains.Annotations.dll
Plugins\Flow.Launcher.Plugin.WebSearch\Microsoft.Windows.SDK.NET.dll
Plugins\Flow.Launcher.Plugin.WebSearch\WinRT.Runtime.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\ar-SA\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\cs-CZ\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\cs\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\da-DK\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\de-DE\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\de\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\es-419\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\es\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\Flow.Launcher.Plugin.WindowsSettings.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\fr-FR\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\fr\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\he-IL\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\hu\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\it-IT\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\it\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\ja-JP\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\ja\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\JetBrains.Annotations.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\ko-KR\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\ko\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\Microsoft.Windows.SDK.NET.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\nb-NO\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\nl-NL\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\pl-PL\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\pl\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\pt-BR\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\pt-PT\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\ru-RU\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\ru\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\sk-SK\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\sr-CS\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\sv\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\tr-TR\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\tr\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\uk-UA\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\vi-VN\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\WinRT.Runtime.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\zh-cn\Flow.Launcher.Plugin.WindowsSettings.resources.dll
Plugins\Flow.Launcher.Plugin.WindowsSettings\zh-TW\Flow.Launcher.Plugin.WindowsSettings.resources.dll
runtime.osx.10.10-x64.CoreCompat.System.Drawing.dll
SemanticVersioning.dll
SharpCompress.dll
SharpVectors.Converters.Wpf.dll
SharpVectors.Core.dll
SharpVectors.Css.dll
SharpVectors.Dom.dll
SharpVectors.Model.dll
SharpVectors.Rendering.Wpf.dll
SharpVectors.Runtime.Wpf.dll
Splat.dll
Squirrel.dll
StreamJsonRpc.dll
Svg.dll
ToolGood.Words.Pinyin.dll
VirtualizingWrapPanel.dll
WindowsInput.dll
WinRT.Runtime.dll
WpfAnimatedGif.dll
YamlDotNet.dll
```

## A5. データ保存先と保存形式

通常は`%APPDATA%\Beacon`、ポータブルは`<exe>\UserData`。`FlowLauncherJsonStorage<T>`は`<DataDirectory>\Settings\<T名>.json`を使用する。

| データ | 実ファイル | 型・形式 | UI依存 |
|---|---|---|---|
| 実行履歴 | `Settings/History.json` | `History.Items`（旧）と`History.LastOpenedHistoryItems`、最大300。`System.Text.Json`の整形JSON | 保存処理はUI非依存だが`History`が`App.API`、`PluginManager`、`Localize`、旧`Result`へ結合 |
| 選択回数 | `Settings/UserSelectedRecord.json` | query+resultとresult単体の安定ハッシュをキーにした`Dictionary<int,int>` | 型自体にWPFなし。入力が旧`Query`/`Result` |
| 最上位固定 | `Settings/MultipleTopMostRecord.json` | query文字列→`Record`キュー。専用JsonConverterでListへ変換 | 型自体にWPFなし。入力が旧`Result` |
| 旧最上位固定 | `Settings/TopMostRecord.json` | query文字列→単一`Record`。新形式へ移行後削除 | 同上 |

`JsonStorage<T>`は`System.Text.Json`、`WriteIndented=true`、`.tmp`へ書いて`File.Replace`、`.bak`バックアップを使用する。履歴の`LastOpenedHistoryResult`は旧Result由来の実行デリゲートを持つがJsonIgnore対象。新Beaconでは保存DTOをUI・デリゲートから分離する。

## A6. リポジトリ状態

- Beacon-old: `beacon`が現在ブランチ、`dev`が上流同期用。
- Phase R0開始時からBeacon-oldには旧Phase作業の未コミット差分と未追跡ファイルが存在する。今回の無変更確認は開始時と終了時の`git status --short`一致で行う。
- 本リポジトリは`main`統合、R0は文書だけを変更する。

## A7. ライセンスと依存用途

- ChefKeys 0.1.2: nupkg nuspecとNuGetライセンス本文でApache-2.0を確認。`Microsoft.WindowsDesktop.App.WPF` FrameworkReference必須。
- Droplex 1.7.0: Python/Node/Everything展開。nuspecはMIT。
- FSharp.Core 9.0.303: 直接PackageReferenceだがC#型参照0件。F#プラグイン許可との関係以上は未確認。nuspecはMIT。
- InputSimulator 1.0.4: DialogJumpとShellのキー送信。nuspecにSPDX expressionがなく失効license URLのみのため、新規採用しない。
- System.Drawing.Common 7.0.0: ImageLoaderの関連アイコン抽出。nupkgにMIT原文同梱。
- EverythingSDK: x64/x86とも既存監査でMIT系原文確認済み。

詳細と新Beaconでの採否はDEPENDENCY_MAP.md。

## A8. Phase R0未確認事項の解消状況

- [x] 標準12プラグインのWPF依存とA〜E分類: A9 / COMPATIBILITY.md §2
- [x] `Win32Helper.cs` / `DialogJump/`の結合度: A10
- [x] ChefKeys UI依存とライセンス: A7 / DEPENDENCY_MAP.md
- [x] Droplex / FSharp.Core / InputSimulator / System.Drawing.Commonの参照元と用途: A7 / DEPENDENCY_MAP.md
- [x] 履歴・TopMostRecord・UserSelectedRecordの保存形式とUI依存: A5
- [x] Everything.dllアーキテクチャ: x64とx86。ARM64なし
- [ ] Flow.Launcher.LocalizationのWinUI実利用可否: 生成物まではA11で確認。WinUIプロジェクトがR0には存在しないため未確認・要R1実験
- [x] Beacon-old Release配布物DLL実測一覧: A4
- [ ] 現行UI画像: 非対話シェルでは表示・キャプチャを検証不能。`docs/rebuild/baseline/README.md`へ取得手順と依頼事項を記録

## A9. 標準12プラグインのWPF依存ファイル

| プラグイン | 直接依存ファイル |
|---|---|
| BrowserBookmark | `Main.cs`; `Views/SettingsControl.xaml(.cs)`; `Views/CustomBrowserSetting.xaml(.cs)`; csprojのUseWPF/UseWindowsForms |
| Calculator | `Main.cs`; `Views/CalculatorSettings.xaml(.cs)`; csprojのUseWPF |
| Explorer | `Main.cs`; `Search/ResultManager.cs`; `Views/PreviewPanel.xaml(.cs)`; `Views/ExplorerSettings.xaml(.cs)`; `Views/QuickAccessLinkSettings.xaml.cs`; `ViewModels/SettingsViewModel.cs`; `ContextMenu.cs`; `Helper/ShellContextMenuDisplayHelper.cs`; `EverythingDownloadHelper.cs`; csprojのUseWPF/UseWindowsForms |
| PluginIndicator | 直接WPF型0件。旧`IPublicAPI`、`PluginPair`、`IHomeQuery`への意味的結合あり |
| PluginsManager | `Main.cs`; `PluginsManager.cs`; `Views/PluginsManagerSettings.xaml`; csprojのUseWPF/UseWindowsForms |
| ProcessKiller | `Main.cs`; `Views/SettingsControl.xaml(.cs)`; csprojのUseWPF |
| Program | `Main.cs`; `Views/ProgramSetting.xaml(.cs)`; `ProgramSuffixes.xaml.cs`; `AddProgramSource.xaml.cs`; `SuffixesConverter.cs`; `Programs/Win32.cs`; `Programs/UWPPackage.cs`; `ViewModels/AddProgramSourceViewModel.cs`; csprojのUseWindowsForms |
| Shell | `Main.cs`; `Views/ShellSetting.xaml(.cs)`; `Converters/LeaveShellOpenOrCloseShellAfterPressEnabledConverter.cs`; csprojのUseWindowsForms |
| Sys | `Main.cs`; `SysSettings.xaml(.cs)`; `CommandKeywordSetting.xaml.cs`; csprojのUseWindowsForms |
| Url | `Main.cs`; `SettingsControl.xaml(.cs)`; `Converters/BoolToVisibilityConverter.cs`; `Converters/InverseBoolConverter.cs`; csprojのUseWPF |
| WebSearch | `Main.cs`; `SettingsControl.xaml(.cs)`; `SearchSourceSetting.xaml(.cs)`; `SearchSourceViewModel.cs`; csprojのUseWPFとiNKORE参照 |
| WindowsSettings | `Helper/ContextMenuHelper.cs`; csprojのUseWindowsForms。本体`WindowsSettings.json`とRESXはUI非依存 |

`ISettingProvider`は10件、WPF `PreviewPanel`はExplorerだけ、`ImageSource`直接利用はExplorerとWebSearch。確定分類と判断理由はCOMPATIBILITY.md §2。

## A10. Win32HelperとDialogJumpのWPF結合度

### Win32Helper

WPF `Window`を公開引数にするAPI:

- `DWMSetCloakForWindow`
- `DWMSetBackdropForWindow`
- `DWMSetDarkModeForWindow`
- `DWMSetCornerPreferenceForWindow`
- `SetForegroundWindow(Window)`
- `IsForegroundWindow(Window)`
- `HideFromAltTab(Window)`
- `ShowInAltTab(Window)`
- `DisableControlBox(Window)`

ほかに`TransformPixelsToDIP(Visual, ...)`がWPF `Visual`/`Point`と`PresentationSource`を使い、fallbackで`HwndSource`を生成する。`GetWindowHandle(Window)`は`WindowInteropHelper`、`GetMainWindowHandle()`は`Application.Current.MainWindow`前提。フォント取得は`SystemFonts.MessageFontFamily`、XAML変換には`System.Windows.Markup`が残る。

一方、HWNDを直接受けるforeground判定、フルスクリーン判定、キーボードレイアウト、プロセス名、STA、Shell/COMなどはCsWin32中心。ファイル全体コピーはせず、HWNDベース部分だけをBeacon.Platform.Windowsへ分離する。

### DialogJump

`DialogJump/Models/WindowsDialog.cs`と`WindowsExplorer.cs`の公開APIは`IntPtr`、文字列、boolとFlow互換interfaceで、WPF `Window`/`HwndSource`を要求しない。実装はWin32 HWND、Shell COM、STAスレッド中心。ただし`DialogJump.cs`はWPF `DispatcherTimer`を10ms更新に使用し、`ShowDialogJumpWindowAsync`、`UpdateDialogJumpWindow`、`ResetDialogJumpWindow`、`HideDialogJumpWindow`の静的デリゲートで表示層へ密結合する。WindowsDialogはInputSimulatorでAlt+D/Enterを送る。

結論: 型レベルのWPF結合はWin32Helperが高い。DialogJumpはWPF型結合はDispatcherTimerだけだが、UIライフサイクル結合が中程度。どちらもそのままCoreへ移さない。

## A11. Flow.Launcher.Localization生成物

nupkg 0.0.6には`Flow.Launcher.Localization.SourceGenerators.dll`、`Analyzers.dll`、`Shared.dll`、`Attributes.dll`、`build/Flow.Launcher.Localization.props`がある。propsは`Languages/en.xaml`をAdditionalFilesへ追加する。Releaseアセンブリ反射で次を確認した。

| アセンブリ | 生成型 | 静的メソッド数 |
|---|---|---:|
| Flow.Launcher.Core.dll | `Flow.Launcher.Core.Localize` | 618 |
| Flow.Launcher.Infrastructure.dll | `Flow.Launcher.Infrastructure.Localize` | 618 |
| BrowserBookmark | `Flow.Launcher.Plugin.BrowserBookmark.Localize` | 24 |
| Calculator | `Flow.Launcher.Plugin.Calculator.Localize` | 14 |
| Explorer | `Flow.Launcher.Plugin.Explorer.Localize` | 187 |
| PluginIndicator | `Flow.Launcher.Plugin.PluginIndicator.Localize` | 3 |
| ProcessKiller | `Flow.Launcher.Plugin.ProcessKiller.Localize` | 7 |
| Shell | `Flow.Launcher.Plugin.Shell.Localize` | 16 |
| Sys | `Flow.Launcher.Plugin.Sys.Localize` | 69 |
| Url | `Flow.Launcher.Plugin.Url.Localize` | 12 |

生成公開メソッドは`string`を返す。入力が旧WPF ResourceDictionary形式のXAMLであり、WinUIでのコンパイル、リソース切替、実行時ロードはR0で検証できない。結論は「未確認・要R1実験」。

## A12. 再利用 / アダプト / 書き直し / 廃止ファイルマップ

| 行き先 | 分類 | Beacon-old内パス | 方針 |
|---|---|---|---|
| Beacon.Contracts | 書き直し | `Flow.Launcher.Plugin/Query.cs`; `Result.cs`; `ActionContext.cs`; `GlyphInfo.cs`; `SharedModels/MatchResult.cs` | データ項目だけを参照し、DTO、ExecutionToken、IconDescriptorを新規定義。旧ソースはコピーしない |
| Beacon.Core | アダプト | `Flow.Launcher.Infrastructure/StringMatcher.cs`; `DiacriticsNormalizer.cs`; `IAlphabet.cs`; `PinyinAlphabet.cs` | UI参照を入れず検索・一致ロジックだけ移す |
| Beacon.Core | 書き直し寄りアダプト | `Flow.Launcher/Storage/QueryHistory.cs`; `HistoryItem.cs`; `UserSelectedRecord.cs`; `TopMostRecord.cs` | App/API/PluginManager/旧ResultをDTOへ置換し、DataRoot配下の新スキーマにする |
| Beacon.Core | アダプト | `Plugins/Flow.Launcher.Plugin.Calculator/MainRegexHelper.cs`; `DecimalSeparator.cs`; `Main.cs`の計算部分; `Settings.cs` | WPF設定画面とResult生成を除き、計算providerへ移す |
| Beacon.Core | アダプト | `Plugins/Flow.Launcher.Plugin.Url/Main.cs`のURL判定部分; `Settings.cs` | UI convertersとSettingsControlは移さない |
| Beacon.Core | アダプト | `Plugins/Flow.Launcher.Plugin.WebSearch/SearchSource.cs`; `SuggestionSources/Baidu.cs`; `Bing.cs`; `DuckDuckGo.cs`; `Google.cs`; `SuggestionSource.cs` | iNKORE/WPF設定UIから分離しWeb providerへ移す |
| Beacon.Core | アダプト | `Plugins/Flow.Launcher.Plugin.BrowserBookmark/Commands/BookmarkLoader.cs`; `ChromiumBookmarkLoader.cs`; `FirefoxBookmarkLoader.cs`; `IBookmarkLoader.cs`; `Models/Bookmark.cs` | ブックマーク読取だけ移す。設定画面は書き直す |
| Beacon.Platform.Windows | アダプト | `Flow.Launcher.Infrastructure/Hotkey/GlobalHotkey.cs`; `Hotkey/RegisteredHotkeyData.cs` | WH_KEYBOARD_LL/CsWin32部分を移す |
| Beacon.Platform.Windows | 再利用候補 | `Plugins/Flow.Launcher.Plugin.Explorer/Search/Everything/EverythingApiDllImport.cs`; `EverythingAPI.cs`; `EverythingSearchOption.cs`; `EverythingSortOption.cs`; `Exceptions/CreateThreadException.cs`; `CreateWindowException.cs`; `InvalidCallException.cs`; `InvalidIndexException.cs`; `IPCErrorException.cs`; `MemoryErrorException.cs`; `RegisterClassExException.cs` | P/Invokeとenum/例外をWPFなしで移す。由来とMIT表記を維持 |
| Beacon.Platform.Windows | アダプト | `Plugins/Flow.Launcher.Plugin.Explorer/Search/Everything/EverythingSearchManager.cs` | Result返却をIconDescriptor/DTOへ変更しキャンセル契約を維持 |
| Beacon.Platform.Windows | 再利用候補 | `Plugins/Flow.Launcher.Plugin.Explorer/EverythingSDK/x64/Everything.dll` | x64 Portable配布へ同梱候補。MIT表記を維持 |
| Beacon.Platform.Windows | 廃止 | `Plugins/Flow.Launcher.Plugin.Explorer/EverythingSDK/x86/Everything.dll` | 初回x64配布には同梱しない |
| Beacon.Platform.Windows | 廃止 | `Plugins/Flow.Launcher.Plugin.Explorer/Search/Everything/EverythingDownloadHelper.cs` | Droplex自動展開はPortable配布へ持ち込まない |
| Beacon.Platform.Windows | アダプト | `Plugins/Flow.Launcher.Plugin.Program/Programs/ShellLinkReader.cs`; `ShellLinkReadResult.cs`; `ShellLocalization.cs`; `IProgram.cs`; `Win32.cs`; `UWPPackage.cs` | WPF Input/BitmapImageとWinForms選択UIを除去し、列挙とShellLink処理を移す |
| Beacon.Platform.Windows | アダプト | `Flow.Launcher.Infrastructure/Image/ImageLoader.cs`; `ThumbnailReader.cs` | Shell取得処理だけ移し、ImageSourceを返さない |
| Beacon.Platform.Windows | 分割アダプト | `Flow.Launcher.Infrastructure/Win32Helper.cs` | A10のHWND/CsWin32部分だけをサービス別に移す |
| Beacon.Platform.Windows | 保留 | `Flow.Launcher.Infrastructure/DialogJump/DialogJump.cs`; `DialogJumpPair.cs`; `DialogJump/Models/WindowsDialog.cs`; `WindowsExplorer.cs` | 初回要件外。採用時はDispatcherTimer/UIデリゲート/InputSimulatorを置換 |
| Beacon.PluginHost | 互換隔離 | `Output/Release/Flow.Launcher.Plugin.dll`（公開面の根拠ソースはA2の各ファイル） | 旧公開API互換バイナリはHost内だけ。WinUI/Contracts/Coreへ参照させない |
| Beacon.PluginHost | アダプト | `Flow.Launcher.Core/Plugin/PluginAssemblyLoader.cs`; `PluginsLoader.cs`; `PluginManager.cs`; `ExecutablePluginV2.cs`; `JsonRPCPluginV2.cs`; `ProcessStreamPluginV2.cs`; `JsonRPCV2Models/JsonRPCExecuteResponse.cs`; `JsonRPCPublicAPI.cs`; `JsonRPCQueryRequest.cs` | 検出・ロード・stdio実績を利用し、Named Pipe RPCとExecutionToken境界へ接続 |
| Beacon.WinUI | 書き直し | `Flow.Launcher/*.xaml`; `Flow.Launcher/SettingPages/**/*.xaml`; `Plugins/*`の設定XAML | 旧WPF/iNKORE UIはコピーせずWinUIで新規作成 |
| Beacon.Distribution | 廃止 | `Scripts/post_build.ps1`; squirrel関連コード; `Net9.0-SelfContained.pubxml` | 旧配布処理は移植せず、R1以降にPortable publish/ZIPを新規作成 |

# B部: 新Beaconに採用済みの構成

| 項目 | 決定 | 根拠 |
|---|---|---|
| リポジトリ | `Crowlxy/Beacon`独立。Beacon-oldへProject Reference / Submodule接続しない | ADR-0001 |
| ソリューション | 新規`Beacon.sln`、`src/` + `tests/`。R1で生成 | ADR-0001 / ARCHITECTURE §1 |
| UI境界 | Contracts/CoreにUI参照禁止。WinUI本体へWPF持ち込み禁止 | ADR-0001 |
| プラグイン互換 | `Beacon.PluginHost.exe` + versioned JSON-RPC over Named Pipe | ADR-0003 |
| 配布 | Portable ZIP一次。MSIXはR11任意 | ADR-0002 |
| DataRoot | `portable.flag` + exe隣接`Data`。無言切替禁止 | ADR-0004 |
| ブランチ | `main`統合、`feature/rebuild-rN-*`からPR | SPEC §9 |
| 依存追加 | 最小限、ライセンス原文確認、事前文書化 | SPEC §8 / DEPENDENCY_MAP |
| 開発中識別子 | `Beacon.Next.exe` / `.Next`サフィックス | SPEC §9 |