# R9 — UX完成度フェーズ（2026-07-23 ユーザー承認）

**状態: 承認済み（2026-07-23）。** 実装プロンプトは [PROMPTS.md](PROMPTS.md) の Phase R9。前提: R8.1（設定画面是正）の完了確認。

番号について: 本フェーズはドラフト時点で「R8」と呼称していたが、R8は検索エンジン統一+設定画面リデザイン（PROMPTS.md定義・実装済み）に使用済みのため、空き番号 **R9** を採用（旧R9は2026-07-22にR7へリネーム済みで空き。番号の非連続許容は既存方針どおり）。

出典: 2026-07-23のFlow Launcher比較調査（静的調査）。記載の問題点は全件 feature/rebuild-r7 のコードで裏取り済み（各項に該当箇所を記載）。

## 目的

Gate C合格済みの「見た目の良い検索ランチャー」を「Flow Launcherより良い検索体験」へ引き上げる。機能数でFlowを追わず、次の4点を直す: 初回描画の速さ・ランキング精度・操作の一貫性・状態の可視化。

## 非目的（このフェーズでやらない）

- 第三者プラグイン対応（既存決定どおり対象外）
- ユーザーテーマエディタ / 常設プレビューパネル / ホーム画面 / Plugin Store / Proxy / Game Mode
- ファイル内容検索・Quick Look（P2として別途判断）
- 設定項目の大量追加（FlowのSettingsを真似しない）

## 検証済みの現状問題（コード裏取り 2026-07-23）

| # | 問題 | 該当箇所 |
|---|------|----------|
| 1 | 結果は全プロバイダー列挙完了後に一括表示。`InputToFirstResultMs` はChannel到着時刻で描画時刻ではない | `src/Beacon.WinUI/MainWindow.R4.cs:174-191`（`ScheduleResults` はループ後の1回のみ）、同 `:183` |
| 2 | Fuzzyの `rawScore` は合格判定にのみ使われ捨てられる。返却は600/300/150の3段階（R8がSPEC §4スコア表へ合わせた結果） | `src/Beacon.Core/FuzzyMatcher.cs:35-38` |
| 3 | Quick Keys既定値が3系統に分裂: Core `BuiltInActions`（rf/cp/rn/term定義済みだが未使用）、SettingsWindowの `DefaultQuickKeys()`（保存時のみ書込）、ランチャーの実行時既定は**空Dictionary** | `src/Beacon.Core/LauncherUx.cs:118-133`、`src/Beacon.WinUI/SettingsWindow.xaml.cs:220`、`src/Beacon.WinUI/MainWindow.R5.cs:323` |
| 4 | 結果バッジは設定を読まず固定（Folder→`term` / それ以外→`rf`） | `src/Beacon.WinUI/MainWindow.R4.cs:379` |
| 5 | アクションはFilePathがあれば10種全表示。`open-with` は `open` と同一実装 | `src/Beacon.WinUI/MainWindow.R5.cs:149-157`、`src/Beacon.Platform.Windows/BuiltInActionService.cs:19-20` |
| 6 | 初回起動導線なし（OnLaunchedは常駐のみ）。Everything案内はネイティブMessageBox | `src/Beacon.WinUI/App.xaml.cs:25`、`src/Beacon.WinUI/MainWindow.xaml.cs:112-119` |
| 7 | 「案内を再表示」はフラグを戻すだけで即時動作しない | `src/Beacon.WinUI/SettingsWindow.xaml.cs:305` |
| 8 | 検索例外・実行失敗はログのみでUIに出ない | `src/Beacon.WinUI/MainWindow.R4.cs:195` |

## 仕様

### S1. 検索結果の段階表示（最優先）

1. 初回表示: 最初の3件到着 **または** 16ms経過のどちらか早い方。ウィンドウResizeはこの1回。
2. 以降: 表示上位（MaximumResultCount件）の構成が変わったときだけListViewを差分更新。HWND Resizeは表示件数が変わった場合のみ。
3. 80〜120ms新着がなければStable扱い（以後の遅延プロバイダー結果は差分挿入のみ）。
4. R6の方針（毎フレームHWND Resize禁止）は維持。ListView内部差分のみで更新する。

> **2026-07-24 ユーザー変更（再改訂）:** 表示後に上位行が並び替わる(reshuffle)体験を避けつつ即時性を回復するため、段階表示の全戻しではなくstable-prefix/append-only方式を採る。初回paintは最初の候補到着後の短い収集窓（既定32ms／MaximumResultCount到達・全完了で早期確定）で1回。以降の遅延到着（主にファイル検索）は表示済みの行を並び替えず末尾の空き位置へのみ追記する。可視行の順序を凍結し入れ替わりを構造的に再発させない。ログは InputToFirstCandidateMs / InputToFirstPaintMs(初回ApplyResults) / InputToStableResultsMs(無新着96ms継続 or 最終flush) の3分割で実描画時刻を反映。S1受入条件（ファイルタイムアウト2s時もアプリ・計算機はFirstPaint<50ms）と整合。

計測ログを3つに分離する:
- `InputToFirstCandidateMs`（現行InputToFirstResultMsの改名。Channel到着）
- `InputToFirstPaintMs`（初回ApplyResults完了）
- `InputToStableResultsMs`（Stable確定）

受入条件: ファイルプロバイダーがタイムアウト（2s）する状況でも、アプリ・計算機等の結果が体感即時（FirstPaint < 50ms目安）に表示される。

### S2. Fuzzyスコアの実数化（SPEC §4スコア表の改訂を伴う。2026-07-23承認）

1. `FuzzyMatcher` は `tier + rawScore` を返す（tier=完全一致/前方一致/その他の底上げ、rawScoreで同tier内を順位付け）。
2. マッチ種別を分離: タイトル完全一致 > 単語先頭一致 > 連続部分一致 > 頭字語 > 非連続一致。サブタイトル・パス一致はタイトル一致より低いベース。
3. アプリ検索の対象フィールド拡張: 実行ファイル名・ショートカット名（既存タイトルに加え）。UWPパッケージ名・エイリアスはP1へ。
4. 既存の使用履歴ブースト・プロバイダースコアとの合成規則を明文化する（tier加算がブーストを常に打ち消さないこと）。
   - 実装規則: `finalScore = providerScore + fuzzyScore + history/contextBoost`。fuzzyはマッチ種別の基礎点とrawScoreを連続値で加算し、履歴は直近+120/+60、選択回数は最大+180、コンテキスト一致は+80とする。隣接するマッチ品質では履歴が再順位付けできる一方、Web検索は基礎点250で最下位を維持する。

受入条件: "vs" で Visual Studio Code が VideoStudio 等より上位。既存FuzzyMatcherテストを実スコア前提へ更新。

### S3. Quick Keys一本化

`Beacon.Core` に `QuickKeyRegistry` を1つ設け、既定値の出所を一本化する:
- `DefaultMappings`（rf/cp/rn/term — 現行 `BuiltInActions` のQuickKey定義を正とする）
- `Load/Save`（保存が無ければDefaultMappingsを返す。**空Dictionaryフォールバック禁止**）
- `FindAction(key)` / `FindKey(actionId)`

SettingsWindow・ゴースト補完・`TryQuickKey`・結果バッジの全てがこれを参照する。バッジは固定文字列をやめ、選択中の結果にのみ表示する。

受入条件: 新規環境（設定未保存）で `foo rf` が即動作する。設定でキーを変更すると、バッジ・ゴースト補完・実行の全てへ反映される。

### S4. アクションの対象別フィルタ

`ActionDescriptor` に `AppliesTo`（File / Folder / Application / Url のFlags）を追加し、`OpenActions` で対象種別により絞る:

| 対象 | 表示 |
|------|------|
| アプリ | 開く、管理者として実行、保存場所、パスコピー |
| ファイル | 開く、プログラムから開く、保存場所、コピー、移動、名前変更、ZIP、パスコピー |
| フォルダ | 開く、ターミナル、コピー、移動、名前変更、ZIP、パスコピー |
| URL | 開く、URLコピー |

`open-with` はWindowsの「プログラムから開く」ダイアログ（`SHOpenWithDialog` 等、公式APIをMicrosoft一次情報で確認）を呼ぶ。コピー・移動先の入力はフォルダ選択（`FolderPicker` 相当）を第一導線にする。

### S5. 初回起動導線

1. 初回起動時のみ小さなWelcomeウィンドウを表示: アプリ名＋「Alt+Space でいつでも検索」＋[試してみる]＋[Windows起動時に開始]トグル。
2. Everything未検出案内はネイティブMessageBoxをやめ、Welcome内（初回）または後述StatusRow（通常時）へ移す。
3. 常設ホーム画面は作らない（空欄時は検索バーのみを維持）。

### S6. 状態・エラーの可視化（StatusRow）

通常結果と別種の `StatusRow` を導入し、次の状態を表示する: 検索中（遅延プロバイダー待ち）/ 結果なし / 一部の検索元が応答しない / 実行失敗（ファイルを開けない・権限）/ キャンセル。警告色・再試行導線・`AutomationProperties.LiveSetting` を持つ。検索例外・実行例外はログに加えStatusRowへ反映する。

ContextActions表示中はScope Chip位置に「<対象名> › アクション」のパンくずを出し、Escで戻れることを初回数回ヒント表示する。

### S7. 設定画面の追補

1. Everythingカードを状態表示に変更（接続済み/未接続＋[接続を再確認]ボタン）。「案内を再表示」ボタンは廃止（問題#7の解消）。
2. 「一般」へ追加: Windows起動時に開始 / 外観（System・Light・Dark）。NavigationViewは現行5カテゴリを維持し、Quick Keysページ独立は現状のまま。
3. 除外アプリに[起動中のアプリから選択]と[exeを選択]の導線を追加（手入力は残す）。
4. `QuickKeyBrush` 等のバッジ配色をLight/DarkのThemeDictionaryへ移す（DesignTokens.xaml集約の原則どおり）。

## R9実装後の是正（2026-07-24 ユーザー指摘）

R9実装完了後の目視確認で出た不具合と改善要求。実装済み。

| # | 内容 | 対応 |
|---|------|------|
| F1 | Windowsのテーマ変更でアプリが落ちる | 独自 `SystemBackdrop` が `OnDefaultSystemBackdropConfigurationChanged` を未オーバーライドで、基底実装がE_INVALIDARGを投げていた。空実装でオーバーライド（[LESSONS.md](LESSONS.md)に記録） |
| F2 | StatusRowが「白い枠の中にAcrylicの枠」に見える | 不透明な `Border.Background` の内側に `SystemBackdropElement`（Dense Acrylic）を置いていたため、Paddingぶんが枠として残っていた。バックドロップの二重掛けをやめ、ランチャー本体のAcrylicへ薄い単色オーバーレイ（`StatusRowBrush`）を重ねる方式へ統一。`DenseDesktopAcrylicBackdrop` は用途が無くなったため削除 |
| F3 | ブラウザ結果に各ブラウザのアイコンを出す | `BrowserIconService`（App Paths / https UserChoice から実行ファイルを解決）を追加。ブックマークは取得元ブラウザ、URL結果は既定ブラウザの実行ファイルアイコンを表示。解決できない場合は従来のグリフ |
| F4 | 設定画面のタイトルバーをUIに馴染む形に | Windows純正キャプションを外し（`SetBorderAndTitleBar(true, false)`）、自前のタイトル行＋閉じるボタンへ差し替え。ドラッグは `InputNonClientPointerSource` のCaption領域で行い、閉じるボタン部分は領域から除外する |
| F5 | 設定画面のMicaとAcrylicの透明度が乖離 | ナビ枠のバックドロップ二重掛けを廃止し、Micaへ薄いオーバーレイ（`SettingsPaneBrush`）を重ねる方式へ変更。Mica自体も `SettingsMicaTintOpacity` / `SettingsMicaLuminosityOpacity` で既定より薄くした |

目視確認の2巡目（同日）で出た追加是正:

| # | 内容 | 対応 |
|---|------|------|
| F6 | 「検索中…」のStatusRowが下端で切れる | `ResizeForResults` がStatusRow/Scope Chipの**Marginを高さに加算していなかった**（StatusRowで10px、Chipで6px不足）。Gridの`*`行が先に潰れ、余りが最後のAuto行のはみ出しになっていた。`LauncherHeight.Calculate` にMargin込みのブロック高さを渡す形へ変更し、[LauncherHeightTests](../../tests/Beacon.R1.Tests/LauncherHeightTests.cs) で回帰を固定 |
| F7 | 設定画面のタイトルバーが二色 | ナビ枠のオーバーレイが `Grid.RowSpan="2"` でタイトルバー行にも掛かっていた。F8で解消 |
| F8 | 「設定タブがAcrylicでウィンドウがMica」の乖離（F5の再指摘） | F5では単色オーバーレイに置き換えたが、素材を2種類重ねること自体が乖離の原因だったため、**ナビ枠専用の面を廃止**しウィンドウ全体を1枚のMica面に統一。ナビの区別はNavigationView標準の選択ハイライトのみ（Windows 11設定アプリと同じ構成）。`SettingsPaneBrush` トークンは削除 |

| F9 | 設定画面の配色が壊れる（壁紙のシアン/マゼンタが乗り、文字が背景に溶ける） | F5対応で入れた `SettingsMicaTintOpacity=0.30` / `SettingsMicaLuminosityOpacity=0.75` が原因。MicaのLuminosity層は壁紙の色相を潰す層で、1.0未満にすると壁紙の色がそのまま透ける。またTintOpacityの既定はテーマごとに異なる（Light 0.5 / Dark 0.8）のに単一値で固定していた。**両トークンを削除し `MicaController` は `Kind` のみ設定**してテーマ既定へ戻した。詳細は [LESSONS.md](LESSONS.md) |
| F10 | ハイコントラストで独自トークンがシステム配色を無視する | `DesignTokens.xaml` の `ThemeDictionaries` に `HighContrast` が無く、`Default`（ダーク相当の半透明値）へフォールバックしていた。`SystemColor*Color` を参照する不透明な `HighContrast` 辞書を追加。`ListViewItemBackgroundSelected*` はWinUI標準のハイコントラスト値へ委ねるため定義しない |

| F11 | 素材の統一（F5/F8の最終形・2026-07-24ユーザー決定） | 「Micaを薄くしてAcrylicへ寄せる」はF9のとおり原理的に成立しないため、**設定画面とWelcome画面をランチャーと同じThin Acrylicへ変更**しMicaを廃止（`ManagedMicaController.cs` 削除）。透明度差はナビ枠＝素材そのまま／コンテンツ側＝NavigationView標準のレイヤー、の二段階で付ける。設定・Welcomeでは不透明度を自前で代入せず `DesktopAcrylicKind.Thin` の既定値を使う（長く開く大きな面は既定値のほうが本文が読める。F9の再発防止でもある）。ランチャーだけは意図的に薄い独自値を維持し、`ThinDesktopAcrylicBackdrop(useCustomTuning:)` で切り替える |

| F12 | 設定/Welcomeのacrylicが薄い（もう少しだけ濃く／F11の延長・2026-07-24ユーザー要望） | 素材値の代入は F9/F11 のとおり不可なので、**素材には触れずテーマ別の低アルファ・スクリム `AppSurfaceScrimBrush` を重ねる**方式を採用。Light `#14FFFFFF`／Dark・Default `#1F000000`／HighContrast `Transparent` を全テーマ辞書に用意し、`SettingsRoot` と Welcome `Root` の `Background` に `{ThemeResource}` で敷く。`ThemeResource` なのでテーマ変更へ自動追従（LESSONS.md準拠）。ナビ枠／コンテンツの二段階差は維持。ランチャー（`useCustomTuning:true`／`AcrylicTintOpacity`・`AcrylicLuminosityOpacity`）は無改修。※作業ツリーに紛れていた「Liquid Glass」風のグラデ改変（`SettingsGlassCardBrush` 等）は未承認かつ後述F14を誘発していたため、jarvis承認の本スクリム方式へ戻した。<br>追補（同日再指摘「まだ透けすぎ／ナビ透明過ぎ・コンテンツ白すぎ」）: `AppSurfaceScrimBrush` のアルファをLight `#29FFFFFF`／Dark・Default `#3D000000` へ引き上げ（HighContrastは`Transparent`据え置き）。加えて設定ウィンドウのナビ／コンテンツ二段階差を明示するトークン `SettingsNavScrimBrush`（Light `#14000000`／Dark・Default `#1F000000`／HighContrastは`SystemColorWindowColor`）と `SettingsContentLayerBrush`（Light `#40FFFFFF`／Dark・Default `#26FFFFFF`／HighContrastは`SystemColorWindowColor`）を追加し、`SettingsWindow.xaml` の `NavigationView.Resources` からThemeDictionaries経由で `NavigationViewDefaultPaneBackground` / `NavigationViewContentBackground` へ割り当ててテーマ追従を保った |
| F13 | 電卓入力後に検索結果が出なくなる（重大バグ・2026-07-24） | `CalculatorSearchProvider` の再帰下降パーサが病的入力（括弧数千重・単項符号連続・`^`連鎖）で捕捉不能な `StackOverflowException` を起こし、常駐プロセスごと即死していた（隔離再現で確認）。`TryEvaluate` に入力長上限256字、`Factor` に再帰深さガード128、`catch` に `OverflowException` を追加。回帰テスト `CalculatorSurvivesPathologicalInput` で固定。詳細は [LESSONS.md](LESSONS.md) |
| F14 | ランチャーのAcrylicが起動時に solid fallback へ落ちる（WARN） | F12作業前の作業ツリーで `DesignTokens.xaml` からランチャー用 `AcrylicTintOpacity`／`AcrylicLuminosityOpacity` が消えており、`ThinDesktopAcrylicBackdrop` が文字列キーで実行時に引くため起動時に `Cannot find a resource with the given key: AcrylicTintOpacity` を出して solid fallback へ落ちていた（ビルドは通る）。両トークンを復元（0.10/0.20）して解消。詳細は [LESSONS.md](LESSONS.md) |
| F15 | 検索結果が最大表示件数（7件）で頭打ちになり、それ以上の候補を見る手段がない（2026-07-24ユーザー指摘） | 原因: `MaximumResultCount`（[DesignTokens.xaml:138](../../src/Beacon.WinUI/Resources/DesignTokens.xaml#L138)）が「ウィンドウ高さ計算用の最大可視行数」と「`ResultMerger.Merge` へ渡す保持候補数上限」を兼用していた（[MainWindow.R4.cs:183,206](../../src/Beacon.WinUI/MainWindow.R4.cs#L183)）。stable-prefix方式（2026-07-24改訂）により`committed`が7件に達すると`Merge`の`Take(max)`が先頭7件で埋まり、以降により高スコアの候補が遅延到着しても追記されなくなる。**対応（実装済み・2026-07-24）**: 候補保持数と可視行数を分離。`DesignTokens.xaml` に `MaximumCandidateCount`（30）を新設し `ResultMerger.Merge` の`max`をこちらへ変更（`MainWindow.R4.cs:183,206`）。`MaximumResultCount`（7）は`ResizeForResults`（`MainWindow.R4.cs:409`）の高さ計算専用として維持（`LauncherHeight.Calculate`の`Math.Clamp`により可視行数は独立にクランプされるため無改修）。`MainWindow.xaml`・`ResultMerger.cs`は無改修。回帰テストは`ResultMergerTests.CandidateRetentionLimitIsIndependentFromVisibleRowLimit`で30件保持/7行クランプの分離を固定。Releaseビルド0警告0エラー・Coreテスト78件/ResultMerger6件/R1テスト9件成功。Windows統合テストの`WindowsRecentEnumerationIsBounded`失敗（`IShellLinkW.Resolve`が環境依存で`ArgumentException`）はF15と無関係の既存事象。**未達**: サンドボックス制約により`Build-Portable.ps1`（NuGet監査データ取得不可でNU1900）と実機起動確認（トレイアイコン登録が拒否され`Unable to add the Beacon tray icon`）は完了していない。ユーザー環境での起動・目視確認が必要 |

設定項目の追加（改善要求の4点目）は別途指示待ちのため未着手。

## P1以降（本フェーズ対象外・次期候補）

パス直接入力・環境変数・Quick Access・最近のフォルダ / `/` 入力時のScope候補表示 / 選択行の操作ヒント / Web検索エンジン選択 / Shell選択・管理者実行 / Calculator関数拡張 / Process Killer強化 / Quick Look。

## 検証分担

Codexは自動検証（ビルド・テスト・起動・PERFログ確認）まで。段階表示の滑らかさ・Welcome画面・StatusRowの見た目・バッジ配色は**ユーザー目視**。Codexは目視確認項目を一覧化して引き渡す。

