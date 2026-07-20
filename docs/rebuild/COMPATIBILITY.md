# COMPATIBILITY — Flow Launcher互換レベル

「完全対応」とは書かない。互換はTierで段階管理する。

> **2026-07-20 ユーザー決定**: 第三者プラグイン対応（旧R7 PluginHost本実装＝Tier 2〜4とIPublicAPI分類表）は**実装対象外**。Tier 1（標準プラグイン相当機能）のみ統合R5の内蔵プロバイダーとして実装する。本文中の「R7」「R8以降」該当行は対象外項目。ADR-0003は将来再開時の設計として維持。

## 1. 互換Tier

| Tier | 対象 | 方針 | 対応Phase |
|---|---|---|---|
| 1 | 標準プラグインの検索結果・アクション | 内蔵プロバイダーとして移植（PluginHost不要）またはHost経由。MVPで対応 | R4/R6 |
| 2 | 第三者プラグインの検索結果・アクション | PluginHost経由（ADR-0003）。ExecutionTokenで実行 | R7 |
| 3 | プラグイン独自設定 | JSON/標準設定（Beacon-oldのJsonRPCPluginSettings形式）はWinUIで描画。WPF専用設定UI（`ISettingProvider.CreateSettingPanel`）は非対応 | R7 |
| 4 | WPFカスタムプレビュー（`Result.PreviewPanel`） | 初期版では非対応。`PreviewInfo`のデータ部分（画像パス・説明・FilePath）のみ利用 | R8以降 |
| 5 | Beaconネイティブ拡張API | 新API。Flow互換とは別に後から設計 | Gate D後 |

原則: 非対応機能は黙って壊さず、UI上で「このプラグインの設定/プレビューは旧版専用」と理由を表示する。

## 2. 標準プラグイン分類（Phase R0確定）

分類: A=UI非依存のまま移植可 / B=UI・Flow API境界を少量アダプトして移植 / C=PluginHostへ隔離 / D=WinUI向けに書き直し / E=廃止候補。

| プラグイン | 確定 | WPF・UI依存の実測 | 根拠ファイルと移植判断 |
|---|---:|---|---|
| Program | B | `ISettingProvider`、WPF `Control`/`UserControl`、`System.Windows.Input`、`BitmapImage`、WinFormsのフォルダ選択 | `Plugins/Flow.Launcher.Plugin.Program/Main.cs`; `Views/ProgramSetting.xaml(.cs)`; `Programs/Win32.cs`; `Programs/UWPPackage.cs`; `ViewModels/AddProgramSourceViewModel.cs`。検索・列挙ロジックを内蔵プロバイダーへ移し、設定・入力・画像出力を置換する |
| Explorer | B | `ISettingProvider`、WPF設定画面、WinFormsダイアログ、`ImageSource`、Shellコンテキストメニュー、`Result.PreviewPanel`を2箇所で生成 | `Plugins/Flow.Launcher.Plugin.Explorer/Main.cs`; `Search/ResultManager.cs:156,357`; `Views/PreviewPanel.xaml(.cs)`; `Views/ExplorerSettings.xaml(.cs)`; `Helper/ShellContextMenuDisplayHelper.cs`; `ViewModels/SettingsViewModel.cs`。Everything/Index/列挙部分だけを選択移植し、設定・プレビュー・メニューは別実装にする |
| Calculator | B | 計算処理本体はUI非依存だが、`ISettingProvider`とWPF `CalculatorSettings`を同一プラグインが公開 | `Plugins/Flow.Launcher.Plugin.Calculator/Main.cs:14,426`; `Views/CalculatorSettings.xaml(.cs)`。計算エンジンを移植し設定UIだけを書き換える |
| Url | B | `ISettingProvider`、WPF `SettingsControl`、`Visibility`/binding converters | `Plugins/Flow.Launcher.Plugin.Url/Main.cs:10,277`; `SettingsControl.xaml(.cs)`; `Converters/BoolToVisibilityConverter.cs`; `Converters/InverseBoolConverter.cs`。判定・実行ロジックを移し設定UIを置換する |
| WebSearch | B | `ISettingProvider`、WPF `UserControl`/`ImageSource`、設定UIがiNKOREを直接参照 | `Plugins/Flow.Launcher.Plugin.WebSearch/Main.cs:12,193`; `SettingsControl.xaml(.cs)`; `SearchSourceSetting.xaml(.cs)`; `SearchSourceViewModel.cs:61`; `Flow.Launcher.Plugin.WebSearch.csproj:53`。`SearchSource`とsuggestion処理のみ選択移植し、旧UI/iNKORE部分は廃止する |
| Sys | D | `ISettingProvider`、WPF `Application`と`Control`、設定画面に加え終了・設定・テーマなど旧本体固有操作を直接実行 | `Plugins/Flow.Launcher.Plugin.Sys/Main.cs:8-18,60`; `SysSettings.xaml(.cs)`; `CommandKeywordSetting.xaml.cs`; `ThemeSelector.cs`。WinUI本体のActionプロバイダーとしてコマンド表と実行を再設計する |
| WindowsSettings | B | 本体はデータ/RESX駆動。コンテキストメニューで`System.Windows`、プロジェクトはWinForms有効 | `Plugins/Flow.Launcher.Plugin.WindowsSettings/Main.cs`; `Helper/JsonSettingsListHelper.cs`; `WindowsSettings.json`; `Helper/ContextMenuHelper.cs:3`; `Flow.Launcher.Plugin.WindowsSettings.csproj:4`。JSON・RESX・起動URIを移しUI操作を置換する |
| BrowserBookmark | B | `ISettingProvider`、WPF設定画面、WinFormsファイル選択。ブックマーク読取はUI非依存 | `Plugins/Flow.Launcher.Plugin.BrowserBookmark/Main.cs:17,219`; `Views/SettingsControl.xaml(.cs)`; `Views/CustomBrowserSetting.xaml(.cs)`; `Commands/BookmarkLoader.cs`; `ChromiumBookmarkLoader.cs`; `FirefoxBookmarkLoader.cs`。loaderを移し設定UIを置換する |
| Shell | B | `ISettingProvider`、WPF設定画面、WinForms `Keys`、InputSimulatorによるキー入力 | `Plugins/Flow.Launcher.Plugin.Shell/Main.cs:12-17,449`; `Views/ShellSetting.xaml(.cs)`; `Converters/LeaveShellOpenOrCloseShellAfterPressEnabledConverter.cs`; `Flow.Launcher.Plugin.Shell.csproj:62`。コマンド解析・起動を移し、キー送信と設定をWindows層へ分離する |
| ProcessKiller | B | `ISettingProvider`とWPF `SettingsControl`。列挙・終了はCsWin32中心 | `Plugins/Flow.Launcher.Plugin.ProcessKiller/Main.cs:10,217`; `Views/SettingsControl.xaml(.cs)`; `Flow.Launcher.Plugin.ProcessKiller.csproj:56-59`。プロセス処理を移し設定UIを置換する |
| PluginsManager | C | `ISettingProvider`、WPF `Control`/`UserControl`、`System.Windows`ダイアログ、旧Flowマニフェスト/APIへ密結合 | `Plugins/Flow.Launcher.Plugin.PluginsManager/Main.cs:11,23`; `Views/PluginsManagerSettings.xaml`; `PluginsManager.cs:8`; `Flow.Launcher.Plugin.PluginsManager.csproj:4-5`。初期版はPluginHost内に隔離する |
| PluginIndicator | C | 直接の`System.Windows`、`UserControl`、`ImageSource`、設定、プレビューは0件。ただし`IPublicAPI.GetAllPlugins`、`ChangeQuery`、`IHomeQuery`と旧プラグイン一覧へ全面依存 | `Plugins/Flow.Launcher.Plugin.PluginIndicator/Main.cs`。Flow互換機能としてPluginHostに置き、本体ネイティブ一覧へはそのまま移植しない |

全12件で`PreviewPanel`を設定するのはExplorerだけ。`ImageSource`を直接使うのはExplorerとWebSearch。`ISettingProvider`実装はProgram、Explorer、Calculator、Url、WebSearch、Sys、BrowserBookmark、Shell、ProcessKiller、PluginsManagerの10件。PluginIndicatorとWindowsSettingsの2件は実装しない。

## 3. 禁止事項

- プラグイン互換のためにWinUI本体プロセスへWPF/WinForms参照を追加しない
- `Flow.Launcher.Plugin.dll`互換層の公開APIへ破壊的変更を入れない（追加は省略可能メンバーのみ）
- WPF `UserControl` / `ImageSource`をプロセス境界（RPC）越しに渡さない
- 移植時にFlow Launcher / WoxのMIT著作権表記を落とさない（ADR-0001 §3）

## 4. IPublicAPI の互換方針（Phase R7で表を完成）

PluginHost内の`IPublicAPI`実装は各メソッドを次のいずれかに分類する。

1. Host内で完結（FuzzySearch、ログ、HTTPダウンロードなど）
2. 本体へRPC委譲（ShowMsgBox→WinUIダイアログ、ChangeQuery、ReQuery、OpenSettingDialogなど）
3. 非対応（no-op + 警告ログ + 互換性表示。テーマAPI、`LoadImageAsync`のImageSource返却などWPF前提のもの）