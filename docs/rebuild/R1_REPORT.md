# Phase R1 検証報告

- 検証日: 2026-07-17
- 構成: Release / win-x64 / Unpackaged / Self-contained Windows App SDK 2.2.0
- 成果物: artifacts/Beacon-Portable-x64.zip（91,454,235 bytes / 87.22 MiB）

## 検証結果

| 項目 | 結果 | 根拠・注記 |
|---|---|---|
| Unpackaged + Self-contained publish | 成功 | Build-Portable.ps1 完走 |
| ZIP展開後の直接起動 | 成功（クリーン環境含む） | ローカルとGitHub Actions windows-latest（WinAppSDK Runtime未導入）双方でTest-Portable.ps1完走。CI: run 29562716968（2026-07-17） |
| 非表示起動からホットキー表示 | 成功 | 通常スモークで表示ログを確認 |
| トレイ表示・表示/終了 | 成功 | 2026-07-17 手動確認: トレイメニューから表示・終了とも正常動作 |
| 単一インスタンス | 成功 | -UseActivationPipeで2本目が終了コード0、1本目の表示を確認 |
| PluginHost RPC逐次結果・キャンセル | 成功 | 逐次結果2件とキャンセル完了ログを確認 |
| exe隣接Dataへのログ保存 | 成功 | artifacts/smoke-b/Beacon/Data/Logs/beacon.logを確認 |
| flag消失 + Data残存 | 成功 | NUnitテスト成功 |
| 書き込み不能時の明示失敗・無言切替なし | 成功 | NUnitテスト成功 |
| フォルダ移動後の再起動 | 成功 | smoke-aからsmoke-bへ移動後に再起動成功 |
| ダミー更新差し替え・ロールバック | 未実施 | 手動手順の実機確認が残る |
| 旧WPF版との並行起動 | 成功 | 2026-07-17 手動確認: Beacon-oldと同時起動でホットキー競合なし |
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

GitHub Actionsのクリーン環境スモークは成功（run 29562716968）。トレイ手動操作・旧WPF版との並行起動も2026-07-17に確認済み。更新差し替え・ロールバックの実機確認はGate Dまでに行う。

## Gate A判定

**承認（2026-07-17）**。差し戻しB1〜B4はすべて解消し、Portable配布・複数プロセスRPC・ホットキー/トレイの技術成立性を確認した。

## CI失敗と対処（2026-07-17 / run 29561124234）

- 事象: windows-latest上の `Test-Portable.ps1 -UseActivationPipe` がsmoke-b段階で「Second Beacon.Next instance did not exit within 15 seconds.」で失敗。
- 根本原因（**確定・ローカル再現で診断ログにより特定**）: Test-Portable.ps1のスクリプト欠陥。smoke-aのDataフォルダをMove-Itemでsmoke-bへ移すため、beacon.logに前フェーズのマーカーが残存し、smoke-bの「Hotkey and tray registered」待ちが**起動途中の新プロセスを待たずに古いログへ即マッチ**していた。その結果activationインスタンスが1本目のMutex獲得前に起動し、**Mutex獲得レース**でactivation側がプライマリになって常駐→WaitForExit(15000)満了。コールドスタートが遅いCIで顕在化しやすく、ローカルでも再現した。アプリ側の終了処理には問題なし（当初仮説のブートストラップ終了ハングは反証済み）。
- 対処: Test-Beaconが各フェーズ開始時に持ち越しbeacon.logを削除してから起動する。削除前のログは `artifacts\logs\<phase>-beacon.log` へ保全し、CIのartifactへ含める。
- 診断可能化（恒久化）: セカンダリパスが `Secondary instance signaled=<bool>, exiting` をbeacon.logへ記録。Test-Portable.ps1が各フェーズの進行をタイムスタンプ付きで標準出力へ出し、activationタイムアウト時に `HasExited` を出力する。
