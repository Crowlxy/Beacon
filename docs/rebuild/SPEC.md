# SPEC — Beacon WinUI 3 正式仕様 (v2.0)

**本書が唯一の正式仕様。** 旧WPF版はリポジトリ `Crowlxy/Beacon-old` に分離済みで、移植元・比較参照のみ（旧計画 `docs/spotlight/` は実装禁止）。
文書優先順位（矛盾時）: **SPEC > 承認済みADR > ARCHITECTURE > DISTRIBUTION > PLAN > フェーズプロンプト**。矛盾を見つけたら実装せず報告する。
関連: [ARCHITECTURE.md](ARCHITECTURE.md) / [PLAN.md](PLAN.md) / [DISTRIBUTION.md](DISTRIBUTION.md) / [COMPATIBILITY.md](COMPATIBILITY.md) / [MIGRATION.md](MIGRATION.md) / [RISK_REGISTER.md](RISK_REGISTER.md) / [LESSONS.md](LESSONS.md)（**作業開始前に必読**）

## 1. 製品定義

BeaconはWindows用ランチャー。**UIはWinUI 3で新規構築**した独立プロジェクトで、Flow Launcher由来の検索・プラグイン・Everything連携ロジックをUI非依存化して選択的に移植する。macOS 26 SpotlightはUX参考のみ（Apple固有素材は禁止）。一次配布は**ZIPポータブル版**（Unpackaged + Self-contained、ADR-0002）。

**やらないこと（初回リリース）**: 旧WPF UI・テーマシステムの移植 / iNKORE UIコンポーネント・素材の同梱 / WPFプラグイン設定パネル・カスタムプレビューの完全対応 / AI検索・クラウド処理 / MSIX・Storeのリリース必須化 / ユーザーテーマ機能。

## 2. 承認済みの前提と旧ルールの扱い（2026-07-16 ユーザー承認）

- 独立リポジトリ・独立 `Beacon.sln`（`src/` + `tests/`）。**Beacon-oldへProject Reference / Submodule接続しない**。必要ロジックのみ由来とMIT表記を維持して選択的移植（ADR-0001）
- `portable.flag` + exe隣接 `Data\` フォルダ（ADR-0004）。`%LOCALAPPDATA%\Beacon\Data` は将来の非Portable/MSIX用予約。**保存先を無言で切り替えない**
- PluginHost + versioned JSON-RPC over Named Pipe（ADR-0003）
- ブランチ: **`main` が統合先**。`feature/rebuild-rN-*` からPR。永続的なrebuild統合ブランチは使わない
- 旧計画Phase 1の未コミット作業はBeacon-old側にのみ保存し、新Beaconへ持ち込まない

Beacon-old旧ルールからの継承・廃止:

| 旧ルール | 新Beacon |
|---|---|
| `Spotlight` という語をコード（識別子・XAML・コメント・リソースキー・UI文字列・ログ・ファイル名・ブランチ名）に書かない | **維持**。ブランドはBeacon。docs散文は可 |
| 失敗はLESSONSへ記録してから再試行 | **維持**（本リポジトリの [LESSONS.md](LESSONS.md)） |
| 素材は商用利用無料のみ・ライセンス原文確認・全文追記 | **維持**（§8） |
| `Flow.Launcher.Plugin.dll` 公開APIの破壊的変更禁止 | **維持**（PluginHost内の互換層として。ADR-0003） |
| 外観はSystem/Light/Darkのみ・ユーザーテーマなし | **維持** |
| 参照画像がUIの正 | **維持**（§3。素材切り出しは禁止） |
| 新規NuGet依存の追加禁止 | **廃止→変更**: 必要最小限+寛容ライセンス原文確認+文書化で可（DEPENDENCY_MAP.md §3） |
| WPFテーマ凍結・DesignTokens.xaml(WPF)・`assembly=Beacon` | **本リポジトリでは無関係**（Beacon-old側のルール）。デザイン値集約の原則のみ §3.3 として継承 |
| iNKORE.UI.WPF.Modernで実現 | **廃止**: 新Beaconは一切参照・同梱しない（Gate D監査） |

## 3. UI仕様

数値は旧計画で参照画像（Beacon-old `docs/検索バー/IMG_4428.jpg`=未入力 / `docs/展開後/IMG_4429.jpg`=展開。本リポジトリへの取り込みはR4開始時）から確定した値を**初期値として継承**。WinUI実測後の調整は可（無断で大幅変更せず、変更理由をADRまたは本書改訂へ記録）。

### 3.1 未入力時

- グローバルホットキー（既定Alt+Space、変更可。開発中はAlt+Shift+Space可）で**完全ピル形状の検索バー1本だけ**を表示。結果欄・履歴・時計・ホーム画面は出さない
- 入力フォーカスを即時取得。Esc/フォーカス喪失で閉じる

### 3.2 入力後

- 同一パネルが下方向へ展開（ウィンドウ上端は動かない）。検索フィールド+（将来）カテゴリチップ行+結果リストが1枚の角丸パネル
- 最大8件を基準。アプリ・ファイル・フォルダ・設定・操作を統合ランキング
- 上下キー・Enter・Escで完結。入力全削除で検索バーのみへ戻る
- 日本語IME変換中にウィンドウサイズ・検索状態が暴れない
- 100〜200% DPI・DPI混在モニターで破綻しない

### 3.3 デザイントークン初期値（`src/Beacon.WinUI/Resources/DesignTokens.xaml` に集約、直値禁止）

| トークン | 初期値 |
|---|---|
| ウィンドウ幅 | 680 DIP |
| 検索バー高 | 64 DIP |
| 検索文字 / 検索アイコン | 22px Regular / 22px |
| 未入力時角丸 / 展開時外周角丸 | 32（完全ピル） / 24 |
| 結果行高 / 結果アイコン | 52 DIP / 32px |
| タイトル / サブタイトル | 16px / 12px |
| チップ高 / チップ文字 | 28 DIP / 12px |
| 最大結果 | 8件 |
| 展開アニメーション | 160〜200ms |
| 選択表現 | 背景ピル（角丸10）のみ。タイトル色は変えない |

### 3.4 外観

- **System / Light / Dark のみ**。ユーザーテーマ機能は作らない
- Backdrop: Mica/Acrylic（SystemBackdrop）。非対応環境はソリッドフォールバック
- アイコン規則（結果の種類で固定・設定切替なし）: アプリ=実アイコン / ファイル=Shellアイコン / 画像・動画・PDF=サムネイル / プラグイン固有=プラグインアイコン / 操作・設定=WinUI標準SymbolまたはFluent系グリフ / 失敗=汎用グリフ

## 4. 検索とランキング

対象: Applications / Files / Folders / Settings / Actions / Calculator / URL / Web fallback（Webはローカルより下）。

初期ルール（旧計画から継承、機械学習なし。実装はBeacon.Coreのランキング層）:

| 条件 | スコア |
|---|---|
| 完全一致 | +600 |
| 前方一致 | +300 |
| タイトル一致 | +150 |
| 過去24時間以内に実行 | +120 |
| 過去7日以内 | +60 |
| 選択回数 | 最大+180 |
| 現在アプリ/フォルダとの関連 | 各+80 |
| Web検索 | -250 |

**Everything**: 稼働していればファイル検索をEverythingへ、なければWindows Indexへ。未起動エラーを検索結果に混ぜない（設定画面で一度だけ案内）。

## 5. 検索契約とプラグイン互換

- 検索契約（SearchResultDto / ExecutionToken / IconDescriptor / ContractVersion）は ARCHITECTURE.md §4 が正。プロセス境界を越えるのはシリアライズ可能なデータのみ — デリゲート・任意object・コントロール・ImageSource型は渡さない
- Flow互換プラグインは `Beacon.PluginHost.exe` 経由（ADR-0003）。互換レベルは [COMPATIBILITY.md](COMPATIBILITY.md) のTier表が正
- **WinUI本体プロセスへWPF参照を追加してはならない**（プラグイン互換のためであっても）

## 6. Windows統合要件

グローバルホットキー / タスクトレイ / 単一インスタンス / 起動時非表示 / フォーカス制御 / 複数モニター・DPI / AppWindow位置管理 / Shellアイコン / Everything IPC / 管理者権限アプリとの境界 / スタートアップ登録（オプトイン）/ 通知。実装方針は ARCHITECTURE.md §5、ポータビリティ規則は DISTRIBUTION.md。

## 7. Beacon固有UX（統合Phase R5で実装。2026-07-20追補）

Beacon-old旧計画の `ACTIONS.md` / `PRIVACY.md` / `UI_STATES.md` は**製品要件の参考のみ**（WPF向けクラス設計は引き継がない）。以下が新アーキテクチャの正。状態・アクション・履歴のロジックは `Beacon.Core`、Windows依存（クリップボード監視・キー送信等）は `Beacon.Platform.Windows`、描画のみ `Beacon.WinUI` に置く。プロセス境界（PluginHost）を越える型だけを `Beacon.Contracts` に追加し、追加時は ContractVersion を上げる。

### 7.1 画面状態と遷移

```
LauncherViewState: Search / Browse / ContextActions / ActionInput / Confirmation / Running
```

- **Escは常に1段だけ戻る**: Running(キャンセル)→ActionInput→…→Search。Search（未入力）でEscを押すと閉じる
- 結果パネル表示条件: Search以外の状態、またはSearchで「入力あり かつ 結果>0」
- `ContextActions`: 選択行で `→` または `Shift+Enter` → その項目に適用できるアクション一覧を同一パネルに表示
- `Running` 中は再実行をブロック（二重実行禁止）。完了・失敗はサブタイトル行で通知しウィンドウは閉じない（失敗時）

### 7.2 Browse Mode

- `Ctrl+1〜4` = **1: Applications / 2: Files / 3: Actions / 4: Clipboard**。Escで通常検索へ
- 空欄時の初期表示: Applications=よく使うアプリ（個人化データ順）/ Files=最近のファイル（Windows Recent。Everything全列挙はしない）/ Actions=使用頻度順 / Clipboard=最近のコピー（履歴OFF時は「無効」の案内行を1件だけ表示）
- モード中の入力はそのカテゴリ内の絞り込みになる

### 7.3 QueryScope・カテゴリチップ

- スラッシュフィルター: `/app` `/file` `/folder` `/action` `/setting` `/clipboard` + 日本語エイリアス（`/アプリ` `/ファイル` `/フォルダ`）
- Tabで確定すると文字列ではなく **QueryScope構造体**（Core内）として保持し、UIは検索フィールド下のカテゴリチップで表現（トークン: チップ高28/文字12）。Backspace（入力が空のとき）またはチップの×で解除
- QueryScopeはプロバイダーへの絞り込み条件としてOrchestratorが適用する（各プロバイダーへ文字列を渡して再解釈させない）

### 7.4 Actions・Quick Keys

- Core内モデル（WPF型・デリゲートRPC渡し禁止は従来どおり）:
  `ActionDescriptor(Id, Title, Glyph, Parameters[], 実行)` / `ActionParameter(Id, Title, Kind=Text|FilePath|FolderPath|Choice, Required)`
- 状態遷移: `Search → ActionSelected → ParameterInput(多段) → Confirmation → Running → Complete`
- **破壊的操作（名前変更・移動・削除系）は Confirmation 必須**
- 内蔵アクション v1（10個）: 開く / 保存場所を表示 / パスをコピー / 管理者として実行 / 名前変更 / コピー / 移動 / ZIP圧縮 / この場所でターミナル / 既定アプリで開く（画像変換・PDF加工・AIは対象外）
- Quick Keys 既定: `rf`=保存場所を表示 / `cp`=パスをコピー / `rn`=名前変更 / `term`=この場所でターミナル。UIは結果行右端の小さなピルバッジ+検索フィールドのゴースト補完。**編集UIはR7**（R5では既定値固定、定義はDataRoot配下の設定ファイル）

### 7.5 クリップボード履歴

| 項目 | 値 |
|---|---|
| 初期状態 | **OFF**（明示的に有効化するまで監視しない。有効化はトレイメニュー） |
| 対象(v1) | テキスト / URL / ファイル一覧 / HTML（画像は対象外） |
| 保存期間 / 件数 | 7日 / 最大500 |
| 保存先 | `<DataRoot>` 配下のみ。クラウド送信なし |
| 暗号化 | DPAPI `DataProtectionScope.CurrentUser` |
| 重複 | 内容ハッシュで除外。Beacon自身の貼り付けを再登録しない |
| 除外 | パスワードマネージャー等の除外フォーマット（`ExcludeClipboardContentFromMonitorProcessing` 等）を尊重 |
| 操作 | 個別削除 / 全削除（復元不可） / 一時停止。除外アプリ指定のUIはR7（データ形式のみ用意） |

監視は `AddClipboardFormatListener`（Platform.Windows内のサービス。UIコードへ直接埋め込まない）。「AIでパスワードらしさを推測して除外」のような曖昧な保護は採用しない。

### 7.6 個人化ランキング

- 保存する: 結果ID / 選択回数 / 最終選択日時 / 使用時のアクティブプロセス名 / 使用モード
- 保存しない: 画面キャプチャ / キー入力全文 / ファイル内容 / 一切のクラウド送信
- §4スコア表の使用履歴項目（24時間+120 / 7日+60 / 選択回数最大+180 / 現在アプリ・フォルダ関連 各+80）の入力源。すべてローカル処理
- **全リセットと個人化OFFを必須機能とする**（R5ではトレイメニューから。設定画面はR7）

### 7.7 プレビューの新データ契約【対象外 2026-07-20】

第三者プラグイン対応（旧R7 PluginHost）が実装対象外となったため、唯一の消費者を失う本契約も**定義しない**。将来プラグイン対応を再開する場合に、データのみのDTO（Title / Description / ImagePathOrUri / FilePath / Metadata）として設計する方針だけを残す。WPF `PreviewPanel`（Tier 4）非対応・プロセス境界越えのUI型禁止の原則は変わらない。

## 8. ライセンス方針

- 依存・素材の追加前にSPDX expressionまたは**原文**を必ず確認（「たぶんMIT」禁止）。表示義務があるものは `LICENSE`（または第三者表記ファイル）へ全文、`attribution.md` へ出典を追記
- Beacon-oldからの移植コードはFlow Launcher / Wox のMIT著作権表記を維持する（ADR-0001 §3）
- 許可素材: Segoe Fluent Icons（OSフォント参照のみ・**TTF同梱禁止**）/ Fluent UI System Icons(MIT) / Lucide(ISC) / unDraw / WinUI標準Symbol。Apple固有素材・参照画像からの切り出しは禁止
- **最終リリース必須条件（Gate D）**: 配布物にiNKORE DLL・派生XAML/素材が残存しない / SegoeFluentIcons.ttf非同梱 / Windows App SDK等のライセンス原文確認済み / Flow Launcher・Wox・Everythingの著作権表示維持 / 実ZIPのバイナリ単位監査 / SBOMまたは依存一覧生成 / LICENSE・attribution.mdが実配布物と一致（「Beacon-oldに存在するだけ」のものを載せない）

## 9. ブランチと開発中識別子

```
main                      … 統合先。常に使える状態を保つ
feature/rebuild-rN-*      … 各フェーズの実装ブランチ → mainへPR
```

- 永続的な統合ブランチ（rebuild等）は使わない（2026-07-16 ユーザー決定）
- Beacon-old側: `beacon`（旧WPF版・凍結: バグ修正のみ）/ `dev`（Flow Launcher上流同期専用）。上流の取り込みはBeacon-old経由の選択的移植としてのみ新Beaconへ届く
- 開発中の内部識別子は旧版と衝突させない: 実行ファイル `Beacon.Next.exe` / Mutex・パイプ名に `.Next` サフィックス / データはexe隣接`Data\`（旧版と物理的に別）。本番切替（Gate D）で正式な `Beacon.exe` へ統合
