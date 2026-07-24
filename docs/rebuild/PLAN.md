# PLAN — 再構築フェーズ計画 (Phase R0〜R11, Gate A〜D)

実装はCodex（`codex exec --model gpt-5.6-sol "<PROMPTS.mdのプロンプト>"`、指示書は `AGENTS.md`）。
各フェーズは `feature/rebuild-rN-*` ブランチで実装し **`main` へPR**（永続的な統合ブランチは使わない）。作業前に [LESSONS.md](LESSONS.md) を読む。
**Codexプロンプトは先に作り切らない**: [PROMPTS.md](PROMPTS.md) には現行フェーズのみ置く（現行: R8.1目視確認待ち → R9）。各フェーズ完了後にClaudeが次を作成する。

**2026-07-20 ユーザー決定（計画変更）**: 旧R5〜R8を **統合Phase R5** として1フェーズで実装する（機能・UIをR8相当まで完成させる。品質妥協なし）。さらに**旧R7（Flow互換PluginHost＝第三者プラグイン対応）は実装対象外**とする（同日ユーザー決定。ADR-0003は将来再開時の設計として維持し、R1のPluginHostダミーは現状維持で触らない）。他文書に残る「R6」「R8」というフェーズ言及は本統合R5を指し、「R7」への言及は対象外項目を指す（※2026-07-22にR6・R7を新規再定義したため、この歴史的言及と現行フェーズ番号は別物。下記2026-07-22注が正）。

**2026-07-22 追加（新旧比較の結論）**: 機能は現行スコープ（統合R5=Gate C合格）に対しほぼ完備で、旧にあって新に無いものは大半が意図的除外（第三者プラグイン・DialogJump・プレビュー・ユーザーテーマ）か既に設定・データ移行フェーズ計画済み（設定画面・ホットキー変更・Quick Key編集）。純粋な新規機能フェーズは追加しない。一方、旧WPF版に比べ**新WinUI版が体感で重く表示が遅い**問題をコード実測で原因特定した（下記R6の根本原因A〜G）。これを空き番号 **R6=性能・応答性フェーズ** として新規定義する（以後「R6」は本性能フェーズを指す）。あわせて設定画面スコープをSPEC §6.6/§7.6と整合させた。

**2026-07-22 承認・番号変更（ユーザー決定）**: R6（性能・応答性フェーズ）を承認。旧「R9 設定・データ移行」を **R7** へリネーム。現行フェーズ順は R5 → **R6（性能）** → **R7（設定・データ移行）** → R10（Portable配布・Gate D）→ R11（MSIX任意）。R10・R11の番号は据え置き（本プロジェクトは番号の非連続を許容）。他文書（CLAUDE.md「R10後」、SPEC §6.6/§7.6の「R9」等）に残る旧番号は本注が正。

**2026-07-23 追加**: R7目視レビューで判明した検索一致品質と設定画面の仮組みを是正する **R8（検索エンジン統一+設定画面リデザイン）** とその是正 **R8.1** を [PROMPTS.md](PROMPTS.md) で定義・実装（`feature/rebuild-r7` 上）。同日、Flow Launcher比較調査（全指摘をコードで裏取り済み）に基づく **R9（UX完成度フェーズ）をユーザー承認**。仕様は [R9_UX.md](R9_UX.md) が正。フェーズ順は R7 → R8/R8.1 → **R9** → R10。

ローカル環境の前提（2026-07-16時点）:
- 本リポジトリ（Crowlxy/Beacon）: `C:\Users\ha.takaku\Desktop\Project\Beacon`
- Beacon-oldのチェックアウト: `C:\Users\ha.takaku\Desktop\Project\Beacon-old`（読み取り専用の参照元として扱う）

| Phase | 内容 | Gate |
|---|---|---|
| R0 | 現状監査と再構築の凍結線（Beacon-old調査 → 本リポジトリへ文書化） | — |
| R1 | WinUI 3 / Portable技術スパイク（Beacon.sln生成） | **A: レビュー** |
| R2 | ContractsとCoreの境界確立 | — |
| R3 | Windowsプラットフォームサービス抽出（移植） | — |
| R4 | WinUIランチャーMVP | **B: レビュー** |
| R5 | **統合完成フェーズ（旧R5・R6・R8）**: デスクトップ統合・標準プロバイダー/ランキング・Beacon固有UX（旧R7=第三者プラグイン対応は対象外） | **C: レビュー** |
| R6 | **性能・応答性の作り込み（レンダリング刷新）**: 毎フレームwindowリサイズ廃止・Composition化・起動最適化（機能追加なし） | 軽量レビュー |
| R7 | 設定・データ移行（設定画面UIを含む） | — |
| R8 | 検索エンジン統一（FuzzyMatcher一本化・ハイライト）+ 設定画面リデザイン（R8.1=是正） | — |
| R9 | **UX完成度**: 段階表示・スコア実数化・Quick Keys一本化・アクションフィルタ・初回導線・StatusRow（[R9_UX.md](R9_UX.md)） | 軽量レビュー |
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

## Phase R5: 統合完成フェーズ（旧R5・R6・R8。2026-07-20ユーザー決定）

目的: Gate B済みの検索MVPを、**日常常用できる完成品（機能・UIはR8相当）**まで一気に引き上げる。実装は3ステージ順で行い、各ステージ末でビルド・テストをグリーンに保つ。R8機能の仕様は SPEC §7（2026-07-20追補済み）が正。

### Stage 1: デスクトップ統合と安定化（旧R5）
スタートアップ登録・解除（オプトイン。トレイメニューから）/ トレイ常駐 / 単一インスタンス / フォーカス・アクティブ化の信頼性 / 複数モニター / DPI変更中の再配置 / スリープ復帰 / 管理者プロセス境界 / クラッシュ復旧 / ログローテーション / パフォーマンス計測。

### Stage 2: 標準プロバイダーとランキング移植（旧R6）
優先順: 1. Program → 2. Explorer/Everything（+Windows Indexフォールバック）→ 3. Calculator → 4. URL → 5. Web Search → 6. System/Settings → 7. Browser bookmarks → 8. Shell / ProcessKiller。
統合ランキング（SPEC §4スコア表）/ 使用履歴 / 遅いプロバイダー隔離 / Web候補の降格 / 使用統計。

### Stage 3: Beacon固有UX（旧R8）
SPEC §7のとおり: 画面状態遷移 / Browse Mode（Ctrl+1〜4）/ QueryScope・カテゴリチップ / Actions（多段引数・確認・Quick Keys）/ クリップボード履歴 / 個人化ランキング。プレビューの新データ契約（SPEC §7.7）はプラグイン対応と同時に対象外。

### 対象外（2026-07-20ユーザー決定）
旧R7のFlow互換PluginHost＝第三者プラグイン対応一式（PluginHost本実装 / プラグイン検出 / JSON-RPC契約 / IPublicAPI分類表）。標準プラグイン相当の機能はStage 2の内蔵プロバイダーで満たす。ADR-0003は設計として維持、R1のPluginHostダミーは触らない。

### 完了条件（旧R5・R6・R8の合算）
- 再起動後の常駐起動 / ホットキー10回反復で表示失敗なし / メモリ・ハンドルリークが許容範囲
- 主要標準検索が旧版と同等以上 / 遅いプロバイダーがUIを止めない / キャンセルが正しく機能 / 比較テストで大きな結果欠落がない
- Escが1段階戻る / 破壊的操作は確認必須 / クリップボード初期OFF / 個人化データはローカルのみ / 全削除・リセット可能

**Gate C（拡張）**: Stage 1〜3の完了条件と実Release ZIPのクリーン環境確認をまとめて承認する（プラグイン互換は承認対象から除外）。

**Gate C判定: 承認（2026-07-22）**。Stage 1〜3実装済みのPortable ZIPをクリーン環境相当で確認し、ユーザー承認。以後の統合R5コード変更は新フェーズではなく既存Gate C合格範囲への保守・不具合修正として扱う。

## Phase R6: 性能・応答性の作り込み（レンダリング刷新）

目的: **旧WPF版と同等以上の体感速度にする**。Gate C合格の機能・UI・デザインを維持したまま、表示・入力・結果ストリーミング時の重さ/カクつきを解消する。**機能追加はしない**（純粋な品質フェーズ）。

### 根本原因（2026-07-22 コード実測に基づく診断）
A〜Dが自己起因で体感遅延の大半を占める。E〜Gは副次。

- **A. トップレベルHWNDの毎フレームリサイズ（最大要因）**: `MainWindow.R4.cs` の `AnimateResize` がUIスレッドのタイマーで16ms毎に `AppWindow.Resize()` + `SetWindowRgn` を実行。毎フレーム DWM再合成・Acrylic再描画・XAMLレイアウトパス・GDIリージョン再生成が走る。旧版はウィンドウを固定し、内側要素の高さをWPFの描画スレッド（コンポジション）でアニメーションしていた。
- **B. Composition不使用の手動イージング**: 上記をUIスレッドの `DispatcherQueueTimer` で回すため、IME変換・検索・アイコン解決とUIスレッドを奪い合いフレーム落ちする。WinUI標準のComposition/暗黙アニメは描画スレッドで動く。
- **C. 角丸を `SetWindowRgn` で毎回自作**: `NativeMethods.ApplyRoundedRegion` がDWMの角丸（`DwmCornerDoNotRound`で無効化済み）の代わりにGDIリージョンをジオメトリ変更毎に生成/破棄。追加コスト＋クリップ無効化が毎フレーム。
- **D. 逐次結果ごとのリサイズ連打**: `SearchAsync` が結果1件受信毎に `ApplyResults`+`ResizeForResults`→`AnimateResize` を再起動。1クエリ中にウィンドウが何度も伸縮し、アイコン解決（Shell呼び出し）も行ごとにUIスレッドへディスパッチ。
- **E. 起動時プリコンパイル無し**: `Beacon.WinUI.csproj` に `PublishReadyToRun` が無く、WinAppSDK self-contained/unpackaged のコールドスタートがJIT依存で重い（初回表示・ログイン直後に影響）。
- **F. UIスレッドでの同期構築**: `MainWindow` コンストラクタが10プロバイダー+オーケストレーター+クリップボード+ホットキー/トレイを同期生成（time-to-resident と初回表示に影響）。
- **G. Acrylicのリサイズ追従コスト**: 毎フレームのウィンドウ寸法変化にAcrylicブラーが追従再計算。Aと複合。

（WinUI 3 / WinAppSDK自体のコールドスタート・ウィンドウ合成コストは構造的にWPFより重く一部は不可避。ただしA〜Dは修正可能で、旧比の遅さの主因はここ。E〜FはMicrosoft公式一次情報で効果を確認してから採用する。）

### 作業
- **ウィンドウを毎フレームリサイズしない設計へ変更**（Aの解消）: 固定（最大高）ウィンドウ＋内側コンテンツの高さをComposition（描画スレッド）アニメーションで開閉し、ウィンドウはクリップで見せる。旧WPF版と同じ方式。→ B・C・D・Gの毎フレーム churn も同時に消える。
- 角丸を `SetWindowRgn` 自作から `DwmCornerRound`（固定ウィンドウ）またはXAMLクリップへ戻し、ジオメトリ変更毎のGDIリージョン生成を廃止。
- リサイズ/レイアウト更新を逐次結果ごとではなく**結果確定（デバウンス）時に1回**へ集約。アイコン解決をバッチ化・オフスレッド化し確定後にまとめて反映。
- `PublishReadyToRun` 有効化とコールドスタート計測（Portable ZIPのサイズ・クリーン起動への影響も確認。効果が薄ければ理由を記録して不採用）。
- 構築をクリティカルパス外へ移動/安全な範囲で並列化。
- 既存PERFログ（`StartupToResidentMs` / `HotkeyToDisplayMs` / `InputToFirstResultMs`）にしきい値と自動計測を追加し回帰を検出。

### 完了条件
- 入力1文字ごとにウィンドウ寸法が伸縮しない（結果が確定したときのみ変化）。
- ホットキー→表示、入力→初結果、結果ストリーミング中のフレーム落ちが**旧版と同等以上**（体感で引っかからない）。数値目標はGate C合格ビルドと旧版の実測ベースラインで確定する。
- R4完了条件（100〜200% DPI・複数モニターで破綻しない・IME）を維持。機能・デザイン（DesignTokens）に退行がない。

**Gate（軽量レビュー）**: 旧版との体感比較とPERFログのしきい値超過が無いことを確認する。**Gate D（リリース）の前提**とする。

**Gate判定: 承認（2026-07-22）**。Stage 1の実測に基づき、結果確定時だけHWNDを1回リサイズし、角丸リージョンも目標寸法へ1回適用する方式を採用。結果一覧は1入力につき全プロバイダー完了後に1回だけ確定表示し、途中結果との差し替えを廃止した。白フラッシュ、枠・角丸、一覧先頭表示、展開・収縮はユーザー目視承認済み。Portableスモーク実測は `HotkeyToDisplayMs=27.9`、`InputToFirstResultMs=21.4`、`DroppedFrames=0`。Stage 4は表示経路の残課題ではなく、追加変更の効果が未確認のため不採用。

## Phase R7: 設定・データ移行

作業: 新設定スキーマ / **設定画面UI（SPEC §6.6・§7.6）: グローバルホットキー変更（既定Alt+Space）・Quick Key編集・個人化/クリップボードのON/OFFと全リセット（R5でトレイのみ→設定画面へ）・Everything未起動の一度きり案内** / Legacy importer（`%APPDATA%\Beacon` → `<BeaconRoot>\Data`、[MIGRATION.md](MIGRATION.md)）/ プラグイン移行 / 履歴移行 / バックアップ・ロールバック / 移行バージョン管理 / 旧版との競合防止 / About・Third-party notices。

完了条件: 旧設定なしの新規起動 / 正常な旧設定からの移行 / 壊れた旧設定からの安全な起動 / 移行失敗時の旧版継続利用 / ユーザー確認なしで旧データを削除しない。

## Phase R8: 検索エンジン統一 + 設定画面リデザイン（2026-07-23）

R7目視レビューの是正フェーズ。詳細は [PROMPTS.md](PROMPTS.md) の定義が正。Stage 1: Beacon-old `StringMatcher` 由来の統一FuzzyMatcher（`Beacon.Core/FuzzyMatcher.cs`、MIT表記維持）＋一致文字ハイライト。Stage 2: 設定画面のNavigationView・行カード化。**R8.1**: 設定画面が開けない退行（XamlControlsResources未マージ）と行カード未実装の是正。

## Phase R9: UX完成度（2026-07-23 ユーザー承認）

目的: Gate C合格済みの製品を「Flowより良い検索体験」へ引き上げる。仕様・受入条件・コード裏取り済みの現状問題一覧は **[R9_UX.md](R9_UX.md) が正**（S1 段階表示と計測3分離 / S2 Fuzzyスコア実数化＝SPEC §4改訂 / S3 QuickKeyRegistry一本化 / S4 アクションAppliesToフィルタ / S5 初回起動Welcome / S6 StatusRow / S7 設定画面追補）。前提: R8.1完了。機能追加はR9_UX.md記載分のみで、R6のレンダリング方針（毎フレームHWND Resize禁止）を退行させない。

完了条件: R9_UX.md 各節の受入条件を満たす / 既存テスト・R4/R6完了条件（DPI・IME・PERFしきい値）に退行がない / Codexは目視確認項目一覧を添えて引き渡す。

**Gate（軽量レビュー）**: 段階表示の体感・Welcome・StatusRow・Quick Keys挙動をユーザー目視で確認する。

## Phase R10: Portable配布・切替・ライセンスゲート

作業: Release Portable x64（ARM64可否判断）/ 再現可能なZIP生成 / 不要ファイル除去 / 更新マニフェスト / Updater（または手動更新手順）/ 更新テスト・ダウングレード防止・ロールバック / フォルダ削除アンインストール確認 / Windows統合の登録・解除 / SBOM / **バイナリライセンス監査（iNKORE完全不在・TTF非同梱の確認）** / `Beacon.Next` → `Beacon` 正式識別子切替 / Beacon-oldのアーカイブ方針 / MSIX Go/No-Go判断。

完了条件: DISTRIBUTION.md §7の受け入れ表をすべて満たす。

**Gate D**: Portable正式リリース承認。

## Phase R11: MSIX / Store版（任意）

Portable正式リリース後に必要性を確認してから着手。Single-project MSIX / WAP / Store提出 / App Installer / x64・ARM64 / PluginHost同梱（Full Trust）/ Portable版との設定移行。**Portable版のMVP・初回リリースをブロックしない。**
