# PLAN — 再構築フェーズ計画 (Phase R0〜R11, Gate A〜D)

実装はCodex（`codex exec --model gpt-5.6-sol "<PROMPTS.mdのプロンプト>"`、指示書は `AGENTS.md`）。
各フェーズは `feature/rebuild-rN-*` ブランチで実装し **`main` へPR**（永続的な統合ブランチは使わない）。作業前に [LESSONS.md](LESSONS.md) を読む。
**Codexプロンプトは先に作り切らない**: 現時点で存在するのはR0とR1のみ（[PROMPTS.md](PROMPTS.md)）。各Gate通過後にClaudeが次を作成する。

ローカル環境の前提（2026-07-16時点）:
- 本リポジトリ（Crowlxy/Beacon）: `C:\Users\ha.takaku\Desktop\Project\Beacon-winui`
- Beacon-oldのチェックアウト: `C:\Users\ha.takaku\Desktop\Project\Beacon`（**フォルダ名はBeaconだが中身はBeacon-old**。読み取り専用の参照元として扱う）

| Phase | 内容 | Gate |
|---|---|---|
| R0 | 現状監査と再構築の凍結線（Beacon-old調査 → 本リポジトリへ文書化） | — |
| R1 | WinUI 3 / Portable技術スパイク（Beacon.sln生成） | **A: レビュー** |
| R2 | ContractsとCoreの境界確立 | — |
| R3 | Windowsプラットフォームサービス抽出（移植） | — |
| R4 | WinUIランチャーMVP | **B: レビュー** |
| R5 | デスクトップ統合と安定化 | — |
| R6 | 標準プロバイダーとランキング移植 | — |
| R7 | Flow互換PluginHost | **C: レビュー** |
| R8 | Beacon固有UX | — |
| R9 | 設定・データ移行 | — |
| R10 | Portable配布・切替・ライセンスゲート | **D: リリース承認** |
| R11 | MSIX / Store版（任意・Gate D後） | — |

Gate以外のフェーズはビルド+起動確認のみ。**各Gateでは実際のRelease ZIPをクリーン環境へ展開して確認する。**

## Phase R0: 現状監査と再構築の凍結線

目的: Beacon-oldの再利用可能ロジックとWPF依存を事実ベースで分類し、本リポジトリの [AUDIT.md](AUDIT.md) / [DEPENDENCY_MAP.md](DEPENDENCY_MAP.md) / [COMPATIBILITY.md](COMPATIBILITY.md) の「未確認」欄をすべて埋める。**コード実装はしない。**

作業: 標準12プラグインの分類確定 / WPF型漏れ公開APIの全抽出 / Win32依存一覧 / データ保存形式（履歴・選択記録）確認 / ビルド・テスト・起動ベースライン記録 / 現行UIスクリーンショット / Release配布物DLL一覧 / 再利用・アダプト・書き直し・廃止のファイルマップ / 上流取り込み手順の文書化。

完了条件:
- UI非依存化・移植の対象がファイル・型単位で分かる
- 未確認事項が「推測」で埋められていない（確認不能なら「未確認」のまま理由を書く）
- Beacon-old側のコードを一切変更していない

## Phase R1: WinUI 3 / Portable技術スパイク

目的: **最も危険な項目を先に証明する** — Unpackaged起動・Self-contained・ウィンドウ制御・複数プロセス・ホットキー。UIの見た目は対象外。本リポジトリに `Beacon.sln` と最小プロジェクトを生成する。

作業: Beacon.sln + 最小構成（WinUI=Beacon.Next.exe / PluginHostダミー / Contracts最小DTO / Distribution）/ Unpackaged起動 / Self-contained publish / クリーン環境（WinAppSDK Runtime未導入）でZIP展開→直接起動 / 非表示起動 / AppWindow表示・位置制御 / 単一インスタンス / グローバルホットキー / トレイ / PluginHostダミー起動 + Named Pipe疎通 / exe隣接 `Data\` 読み書き（ADR-0004の解決規則）/ フォルダ移動後起動 / 読み取り専用フォルダでの明確な失敗 / x64 Portable ZIP生成 / ダミー更新差し替え+ロールバック / WinAppSDKバージョン・ライセンス確認 → ADR-0002追記 / 将来MSIX化を阻害しないか確認。

完了条件:
- クリーン環境でZIP展開後に `Beacon.Next.exe` を起動できる（Runtime事前導入なし）
- PluginHostダミーを同一配布フォルダから起動しRPC往復・キャンセルできる
- ホットキーでWinUIウィンドウを表示できる / トレイから表示・終了できる
- 設定・ログが `<BeaconRoot>\Data` へ保存され、フォルダ移動後も起動する
- 旧WPF版Beaconと並行起動できる（識別子衝突なし）
- Portable成果物にiNKOREが含まれない

**Gate A**: Portable配布・複数プロセス・ホットキーの技術成立性を承認する。**失敗した場合、UI実装へ進まず設計を修正する。**

## Phase R2: ContractsとCoreの境界確立

作業: `Beacon.Contracts` 本設計（SearchRequest / SearchResultDto / ResultKind / IconDescriptor / ExecutionToken / QuerySession・cancellation / Provider contract / ContractVersion）/ シリアライズ往復テスト / Contracts・CoreへのUI参照をビルド+CIで禁止（ADR-0001 §5）/ 旧Result→DTO変換方針の確定。

完了条件: Contracts/CoreにWPF・WinUI参照がない（CIで検証）/ 逐次結果・キャンセル・実行要求をテストで再現できる / Result→DTO写像表が確定。

## Phase R3: Windowsプラットフォームサービス抽出

作業: Beacon-oldから選択的移植（由来・MIT表記維持）— Everything P/Invoke / Windows Index / アプリ検索 / Shellアイコン・サムネイル（出力は`IconDescriptor`。**WPF ImageSourceを返さない**）/ プロセス起動 / ファイル操作 / Active Window / Explorerパス / クリップボード / DataRootResolver（ADR-0004）。

完了条件: コンソールハーネスまたはCoreテストでアプリ・ファイル検索が動く / Everythingあり・なし両方で動く / WPFを起動せず結果を取得できる。

## Phase R4: WinUIランチャーMVP

作業: 未入力=ピル検索バーのみ / 入力後展開 / 結果リスト / キーボード操作 / IME / System・Light・Dark / Shellアイコン表示 / アプリ・ファイル・フォルダ・電卓・URL・Web fallback（内蔵プロバイダー）/ DesignTokens.xaml固定デザイン（SPEC §3.3）/ AppWindow位置・DPI / Mica・Acrylic / 参照画像の取り込み。

完了条件: 旧版を起動しなくても検索・実行できる / 100〜200% DPIで破綻しない / 日本語IMEで入力・変換・確定できる / 入力全削除でバーだけへ戻る / Everything検索がWinUI結果へ流れる。

**Gate B**: 日常利用できる検索MVPとして承認する。

## Phase R5: デスクトップ統合と安定化

作業: スタートアップ登録・解除（オプトイン）/ トレイ常駐 / 単一インスタンス / フォーカス・アクティブ化 / 複数モニター / DPI変更中の再配置 / スリープ復帰 / 管理者プロセス境界 / クラッシュ復旧 / ログローテーション / パフォーマンス計測。

完了条件: 再起動後の常駐起動 / ホットキー100回反復で表示失敗なし / PluginHost停止時も本体が落ちない / メモリ・ハンドルリークが許容範囲。

## Phase R6: 標準プロバイダーとランキング移植

優先順: 1. Program → 2. Explorer/Everything → 3. Calculator → 4. URL → 5. Web Search → 6. System/Settings → 7. Browser bookmarks → 8. その他。

作業: 統合ランキング（SPEC §4スコア表）/ 使用履歴 / 最上位固定の代替 / 遅いプロバイダー隔離 / Web候補の降格 / 使用統計。

完了条件: 主要標準検索が旧版と同等以上 / 遅いプロバイダーがUIを止めない / キャンセルが正しく機能 / 比較テストで大きな結果欠落がない。

## Phase R7: Flow互換PluginHost

作業: PluginHost本実装（ADR-0003）/ プラグイン検出 / JSON-RPC契約 / 逐次配信 / ExecutionToken / タイムアウト・キャンセル・再起動 / プラグイン別ログ / 権限・パス検証 / 互換性表示（非対応WPF UIの明示）/ IPublicAPI分類表（COMPATIBILITY.md §4）。

完了条件: 代表的な第三者プラグインで検索・実行できる / プラグインクラッシュで本体が落ちない / WPF UserControlがWinUIプロセスへ渡らない / 非対応機能を黙って壊さず理由を表示する。

**Gate C**: プラグイン互換範囲（COMPATIBILITY.mdのTier実績）を承認する。

## Phase R8: Beacon固有UX

対象: Browse Mode / QueryScope・カテゴリチップ / Actions / Quick Keys / 多段引数入力 / 確認 / クリップボード履歴 / 個人化ランキング / プレビューの新データ契約。**開始時に新アーキテクチャ向け仕様をSPECへ追補する。**

完了条件: Escが1段階戻る / 破壊的操作は確認必須 / クリップボード初期OFF / 個人化データはローカルのみ / 全削除・リセット可能。

## Phase R9: 設定・データ移行

作業: 新設定スキーマ / Legacy importer（`%APPDATA%\Beacon` → `<BeaconRoot>\Data`、[MIGRATION.md](MIGRATION.md)）/ プラグイン移行 / 履歴移行 / バックアップ・ロールバック / 移行バージョン管理 / 旧版との競合防止 / About・Third-party notices。

完了条件: 旧設定なしの新規起動 / 正常な旧設定からの移行 / 壊れた旧設定からの安全な起動 / 移行失敗時の旧版継続利用 / ユーザー確認なしで旧データを削除しない。

## Phase R10: Portable配布・切替・ライセンスゲート

作業: Release Portable x64（ARM64可否判断）/ 再現可能なZIP生成 / 不要ファイル除去 / 更新マニフェスト / Updater（または手動更新手順）/ 更新テスト・ダウングレード防止・ロールバック / フォルダ削除アンインストール確認 / Windows統合の登録・解除 / SBOM / **バイナリライセンス監査（iNKORE完全不在・TTF非同梱の確認）** / `Beacon.Next` → `Beacon` 正式識別子切替 / Beacon-oldのアーカイブ方針 / MSIX Go/No-Go判断。

完了条件: DISTRIBUTION.md §7の受け入れ表をすべて満たす。

**Gate D**: Portable正式リリース承認。

## Phase R11: MSIX / Store版（任意）

Portable正式リリース後に必要性を確認してから着手。Single-project MSIX / WAP / Store提出 / App Installer / x64・ARM64 / PluginHost同梱（Full Trust）/ Portable版との設定移行。**Portable版のMVP・初回リリースをブロックしない。**
