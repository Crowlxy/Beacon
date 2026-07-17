# Phase R1 検証報告

- 検証日: 2026-07-17
- 構成: Release / win-x64 / Unpackaged / Self-contained Windows App SDK 2.2.0
- 成果物: artifacts/Beacon-Portable-x64.zip（91,454,235 bytes / 87.22 MiB）

## 検証結果

| 項目 | 結果 | 根拠・注記 |
|---|---|---|
| Unpackaged + Self-contained publish | 成功 | Build-Portable.ps1 完走 |
| ZIP展開後の直接起動 | ローカル成功 / クリーン環境未実施 | Test-Portable.ps1 完走。GitHub Actions windows-latest（Server系）での確認はpush後に実施 |
| 非表示起動からホットキー表示 | 成功 | 通常スモークで表示ログを確認 |
| トレイ表示・表示/終了 | 実装済み / 手動操作未実施 | 登録ログとスモーク終了時のプロセス停止を確認。コンテキストメニュー操作は未実施 |
| 単一インスタンス | 成功 | -UseActivationPipeで2本目が終了コード0、1本目の表示を確認 |
| PluginHost RPC逐次結果・キャンセル | 成功 | 逐次結果2件とキャンセル完了ログを確認 |
| exe隣接Dataへのログ保存 | 成功 | artifacts/smoke-b/Beacon/Data/Logs/beacon.logを確認 |
| flag消失 + Data残存 | 成功 | NUnitテスト成功 |
| 書き込み不能時の明示失敗・無言切替なし | 成功 | NUnitテスト成功 |
| フォルダ移動後の再起動 | 成功 | smoke-aからsmoke-bへ移動後に再起動成功 |
| ダミー更新差し替え・ロールバック | 未実施 | 手動手順の実機確認が残る |
| 旧WPF版との並行起動 | 未実施 | 本検証ではBeacon-oldを起動していない |
| iNKORE・禁止フォント不在 | 成功 | portable-dlls.txtとソース監査で一致0件 |
| WPF配布ファイル不在 | 成功 | Presentation系、WindowsBase、System.Windows系、wpfgfx、UIAutomation、ReachFramework、System.Printingの一致0件 |
| 将来MSIX化 | 阻害要因なし | package identity非依存のWin32ホットキー/トレイとプロセス境界を使用。MSIX固有検証はR11 |

## B1〜B4

- B1: WPF RuntimePack由来ファイルを配布ステージから除外。ZIPは112.19 MiBから87.22 MiBへ24.97 MiB縮小。
- B2: Windows App SDK版数、最小OS、サイズ、ライセンス、Unpackaged既知問題をADR-0002へ記録。
- B3: 採用依存・推移依存とライセンス原文確認結果をDEPENDENCY_MAP.md、表示義務をattribution.mdへ記録。
- B4: pull request / workflow_dispatch用Portable smokeを追加。CIはactivation pipeを使用。

CI成功判定のログマーカーは Hotkey and tray registered、Hotkey or activation pipe displayed the AppWindow、RPC incremental result、RPC cancellation confirmed。ERROR / Exceptionは0件であること。

## 実行結果

- dotnet build Beacon.sln -c Release: 警告0、エラー0
- Build-Portable.ps1: 成功
- Test-Portable.ps1: 成功
- Test-Portable.ps1 -UseActivationPipe: 成功
- dotnet test Beacon.sln -c Release --no-build: 4件成功
- 終了後のBeacon.Next残存プロセス: 0

## 残リスク

Gate A承認前に、GitHub Actionsのクリーン環境スモーク、トレイメニューの手動操作、更新差し替え・ロールバック、旧WPF版との並行起動を確認する。
