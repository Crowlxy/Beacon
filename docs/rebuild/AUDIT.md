# AUDIT — Beacon-old 現状監査（2026-07-16 Claude調査 / Phase R0で完成させる）

本書は**A部=Beacon-oldで確認した事実**（実コードを読んで確認したもの）と、**B部=新Beaconに採用済みの構成**（本リポジトリの決定）を明確に分ける。「未確認」はPhase R0でCodexが埋める。推測で埋めない。

調査対象のBeacon-oldローカルチェックアウト: `C:\Users\ha.takaku\Desktop\Project\Beacon`（フォルダ名はBeaconだが中身はBeacon-old。origin修正済み）

---

# A部: Beacon-oldで確認した事実

## A1. ソリューション構成（確認済み）

`Flow.Launcher.sln` / .NET 9 / 17プロジェクト:

| プロジェクト | 種別 | UI依存 | 備考 |
|---|---|---|---|
| Flow.Launcher | WinExe (WPF) | WPF | AssemblyName=Beacon → `Output/Debug/Beacon.exe` |
| Flow.Launcher.Core | Library | UseWindowsForms=true | プラグイン基盤・テーマ・更新(squirrel) |
| Flow.Launcher.Infrastructure | Library | WPF (NHotkey.Wpf, SharpVectors.Wpf, ImageSource) | 検索基盤・設定・画像・Win32 |
| Flow.Launcher.Plugin | Library | UseWPF=true | 公開プラグインAPI (`Flow.Launcher.Plugin.dll`) |
| Plugins/* ×12 | Library | 個別に要調査（R0） | 標準プラグイン |
| Flow.Launcher.Test | NUnit 4.4 + Moq | — | テスト基盤の先例 |

## A2. 公開APIへのWPF型漏れ（確認済み・隔離が必須である根拠）

### `Flow.Launcher.Plugin/Result.cs`
- `IconDelegate` = `Func` が **`System.Windows.Media.ImageSource`** を返す（`Icon` / `BadgeIcon` / `PreviewInfo.PreviewDelegate`）
- `PreviewPanel` = **`Lazy<System.Windows.Controls.UserControl>`**
- `Action` / `AsyncAction` = **プロセス内デリゲート**（`Func<ActionContext, bool>`）
- `ContextData` = 任意`object`（シリアライズ不可）
- 一方、`Title/SubTitle/IcoPath/Score/CopyText/AutoCompleteText/RecordKey` 等の**データ部分はシリアライズ可能に設計済み**（JSON-RPCプラグインが既に利用）

### `Flow.Launcher.Plugin/Interfaces/`
- `IPublicAPI`: `using System.Windows`（`MessageBoxResult`等）、`LoadImageAsync → ValueTask<ImageSource>`、テーマAPI（`ThemeData`）
- `ISettingProvider.CreateSettingPanel()` → **`System.Windows.Controls.Control`**

→ `Flow.Launcher.Plugin.dll` はWPF汚染されており、WinUIプロセスへ直接ロードできない（→ B部: PluginHost隔離）。

## A3. 移植候補資産の所在（確認済み）

| 資産 | Beacon-old内の場所 | UI依存 | 見立て |
|---|---|---|---|
| ポータブルデータ機構 | `Infrastructure/UserSettings/DataLocation.cs`（exe隣接`UserData` + `.dead`指標） | なし | 方式のみ継承。実装は新規（ADR-0004） |
| JSON-RPCプラグイン基盤 | `Core/Plugin/JsonRPCPluginV2.cs`, `ProcessStreamPluginV2.cs` 他 | 低 | **StreamJsonRpc 2.22.11の実運用実績**。PluginHostプロトコルの下敷き |
| グローバルキーボードフック | `Infrastructure/Hotkey/GlobalHotkey.cs`（CsWin32 / WH_KEYBOARD_LL） | なし | 移植可 |
| ホットキー登録 | `Flow.Launcher/Helper/HotKeyMapper.cs`（NHotkey.Wpf + ChefKeys） | WPF | 書き直し（RegisterHotKey直叩き） |
| 単一インスタンス | `Flow.Launcher/Helper/SingleInstance.cs`（Mutex + NamedPipe、WPF Application前提） | WPF | 方式流用・実装書き直し |
| Everything連携 | `Plugins/...Explorer/Search/Everything/`（P/Invoke + 同梱`Everything.dll`） | なし（API層） | 移植可。ARM64対応は未確認 |
| 画像・アイコン | `Infrastructure/Image/ImageLoader.cs`, `ThumbnailReader.cs` | ImageSource返却 | ロジック移植・出力型を`IconDescriptor`へ |
| Win32統合 | `Infrastructure/Win32Helper.cs`, `DialogJump/` | CsWin32中心・WPF混在度**未確認** | R0で分類 |
| 設定 | `Infrastructure/UserSettings/Settings.cs` | 低（データ） | スキーマは新設計、移行元として利用 |
| 履歴・スコア | 保存形式**未確認** | 未確認 | R0で分類 |

## A4. 配布・更新の現状（確認済み）

- squirrel.windows 1.9.0 インストーラ（`Scripts/post_build.ps1`）
- Self-contained publishプロファイル既存（`Net9.0-SelfContained.pubxml`）
- **`post_build.ps1` のPortable ZIP生成はリブランド後に壊れている**（`$env:LocalAppData\FlowLauncher` を作成しつつ `$env:LocalAppData\Beacon` を圧縮するパス不一致）
- CI: appveyor.yml + GitHub workflows（いずれも上流Flow Launcher向け）

## A5. データ保存先（確認済み）

- 通常: `%APPDATA%\Beacon\`（Settings/Logs/Cache/Plugins/Themes）。ログはNLogで `Logs\1.0.0\`
- ポータブル: `<exe隣接>\UserData\`

## A6. リポジトリ・ブランチ状態（2026-07-16確認）

- `Crowlxy/Beacon-old`: `beacon`（旧WPF版、旧計画Phase 1が**未コミットのまま**）/ `dev`（上流同期）— **旧Phase 1作業はBeacon-old側にのみ保存し、新Beaconへ持ち込まない**（ユーザー決定）
- `Crowlxy/Beacon`（本リポジトリ）: `main` + `feature/rebuild-r0-audit`。ドキュメントと設定のみ、ソース未生成

## A7. ライセンス監査（Beacon-old側 2026-07-16実施済み、詳細はBeacon-oldの`LICENSE.md`）

- **iNKORE.UI.WPF.Modern 0.10.1 = 独自ライセンス。商用は書面許可が必要** → 新Beaconは一切参照・移植しない
- `Resources/SegoeFluentIcons.ttf` = 再配布権不明 → 新Beaconへ同梱しない
- Everything SDK（Everything.dll）= MIT系（原文確認済み）
- Flow Launcher / Wox 本体 = MIT → 移植時に著作権表記維持

## A8. Phase R0で埋める未確認事項（推測禁止）

- [ ] 標準12プラグインそれぞれのWPF依存箇所 → COMPATIBILITY.md §2を根拠付きで確定
- [ ] `Win32Helper.cs` / `DialogJump/` のWPF結合度
- [ ] ChefKeysのUIフレームワーク依存有無とライセンス
- [ ] Droplex / FSharp.Core / InputSimulator / System.Drawing.Common の参照元と用途
- [ ] 履歴・TopMostRecord・UserSelectedRecordの保存形式とUI依存
- [ ] Everything.dllのアーキテクチャ（x64のみか）
- [ ] `Flow.Launcher.Localization`（ソースジェネレータ）の生成物とWinUI利用可否
- [ ] Beacon-old Release配布物のDLL実測一覧
- [ ] 現行UIのスクリーンショット/動画ベースライン（`docs/rebuild/baseline/`）

---

# B部: 新Beaconに採用済みの構成（本リポジトリの決定）

| 項目 | 決定 | 根拠 |
|---|---|---|
| リポジトリ | `Crowlxy/Beacon` 独立。Beacon-oldへProject Reference / Submodule接続しない。選択的移植のみ（由来+MIT表記維持） | ADR-0001（承認済み） |
| ソリューション | 新規 `Beacon.sln`、`src/` + `tests/`（R1で生成） | ADR-0001 / ARCHITECTURE §1 |
| UI境界 | Contracts/CoreにUI参照禁止（ビルド+CI強制）。WinUI本体へWPF持ち込み禁止 | ADR-0001 |
| プラグイン互換 | `Beacon.PluginHost.exe` 隔離 + versioned JSON-RPC over Named Pipe（第一候補StreamJsonRpc、R1で確定） | ADR-0003（承認済み） |
| 配布 | Portable ZIP一次（Unpackaged + Self-contained）。MSIXはR11任意 | ADR-0002 |
| DataRoot | `portable.flag` + exe隣接 `Data\`。`%LOCALAPPDATA%\Beacon\Data` は将来の非Portable/MSIX用予約。**無言の保存先切替禁止** | ADR-0004（承認済み） |
| ブランチ | `main` 統合、`feature/rebuild-rN-*` からPR。永続統合ブランチなし | SPEC §9（承認済み） |
| 依存追加 | 最小限+寛容ライセンス原文確認+文書化 | SPEC §8 / DEPENDENCY_MAP §3 |
| 開発中識別子 | `Beacon.Next.exe` / `.Next` サフィックスで旧版と並行起動 | SPEC §9 |
