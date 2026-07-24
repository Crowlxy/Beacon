# 失敗記録簿（LESSONS）

**未解決の失敗だけ**を置く。クローズ済みは [LESSONS-archive.md](LESSONS-archive.md)（必読ではない。新規記録前の同一原因検索と類似事象の調査時のみ参照）。
環境の恒常制約（サンドボックス・パッチ引数・パス列挙・ログ排他等）は **AGENTS.md「環境の既知制約」が正**であり、ここへ再記録しない。

## 記録基準

次を**すべて**満たす失敗のみ記録する:
根本原因が判明 / 再発可能性が高い / 他の作業でも使える / 明確な防止策がある / まだ機械的に防止されていない。

記録しない: タイプミス・構文エラー・一度きりのコマンドミス・再現しない一時エラー・**ビルド/analyzer/テスト/スクリプトが既に検出する問題**（CA系エラーやコンパイルエラーはビルドが記録簿）。

## 再発時の扱い

同一原因の再発は文章を追記せず、次の順で機械的防止へ反映してからアーカイブへクローズする:
**テスト → analyzer/lint → ビルド/CI → スクリプト → 事前確認手順 → AGENTS.mdの固定ルール**（自動化不能な場合のみ）。

## 記録形式（新しいものを上へ）

```
## YYYY-MM-DD: 一行要約
- 事象: 何が起きたか（エラーメッセージ・症状）
- 原因: 根本原因（未確定なら未確定と書く）
- 再発防止: 反映先（テスト/CI/スクリプト/AGENTS等）を明記。反映が済んだらアーカイブへ移す
```

## 未解決エントリ

## 2026-07-24: 電卓パーサの無制限再帰で病的入力が常駐プロセスごと落とす
- 事象: 検索欄で深い括弧ネスト（例 `(((…1…)))` を数千重）・連続する単項符号（`-----…1`）を含む入力を打つと、以後いっさい検索結果が出なくなる。原因調査で `CalculatorSearchProvider` 内 `ExpressionParser` に括弧5000重（1万字）を通すとプロセスが `Stack overflow.` で即死することを隔離再現で確認。`WebSearchProvider` は非空クエリなら必ず1件返す設計のため「結果が完全に空」＝プロバイダ例外ではなくプロセス自体の死と一致する。
- 原因: 再帰下降パーサの `Factor`→`Expression`（括弧）/ `Factor`→`Factor`（単項符号）/ `Power`→`Power`（`^`右結合）が入力長に比例して無制限に再帰する。.NETの `StackOverflowException` は `try/catch` で捕捉できずプロセスをfail-fastさせるため、常駐のトレイ・ホットキーごと消える。
- 再発防止: `TryEvaluate` に入力長上限（256字）、全再帰の収束点である `Factor` に再帰深さガード（128、超過で `FormatException`）を追加し、`catch` に `OverflowException` を追加（`src/Beacon.Core/BuiltInSearchProviders.cs`）。回帰は `tests/Beacon.Core.Tests/BuiltInSearchProviderTests.cs` の `CalculatorSurvivesPathologicalInput`（括弧・`^`・単項符号を各5000）で機械的に固定済み。テスト有りで機械的防止済みのため次回整理時にアーカイブへ移してよい。

## 2026-07-24: 文字列キー参照のThemeResourceを消すとビルドは通るが起動時に素材が落ちる
- 事象: 設定画面まわりのDesignTokens改変で、ランチャー用の `AcrylicTintOpacity` / `AcrylicLuminosityOpacity` が `DesignTokens.xaml` から消えた状態のReleaseを起動したところ、`WARN Managed thin desktop acrylic unavailable: … Cannot find a resource with the given key: AcrylicTintOpacity` が出てランチャーのAcrylicが solid fallback へ落ちた（見た目上は透過が消える）。ビルドは0 warning/0 errorで通っていた。
- 原因: `ThinDesktopAcrylicBackdrop` は `Application.Current.Resources["AcrylicTintOpacity"]` のように**文字列キーで実行時にリソースを引く**ため、キーの欠落はコンパイル時に検出されず、起動時の `KeyNotFoundException`（→ WARN・fallback）で初めて顕在化する。設定画面の是正時に「設定専用トークンへの置き換え」と同時にランチャー用トークンを巻き込んで削除したのが引き金。
- 再発防止: 素材トークン（`Acrylic*Opacity` 系）は用途別に分け、ランチャー用（`AcrylicTintOpacity` / `AcrylicLuminosityOpacity`）は設定画面の変更で触らない。設定/Welcomeは素材値を代入せず `useCustomTuning:false` ＋ スクリムで濃さを付ける方式に統一済み（下記Acrylic代入の教訓と同根）。文字列キー参照はビルドで守れないため、将来Beacon.WinUIにテストを置けたら「`ThinDesktopAcrylicBackdrop` が参照するリソースキーが全テーマ辞書に存在する」ことを検証してアーカイブへ移す。Beacon.WinUI用テストが無く機械的検出できないため未クローズ。

## 2026-07-24: Mica/AcrylicのTintOpacity・LuminosityOpacityを自前で代入して配色が壊れた
- 事象: 設定画面のMicaを「薄く」しようと `TintOpacity=0.30` / `LuminosityOpacity=0.75` を代入したところ、ウィンドウ全体がデスクトップ壁紙由来のシアン・マゼンタに染まり、文字が背景に溶けて読めなくなった。
- 原因: Micaの合成は `ぼかした壁紙 → Luminosity層 → Tint層` の順で、**Luminosity層が壁紙の色相を潰して明度だけの面にする役割**を持つ。既定値は Light/Dark とも 1.0 で、1.0未満にすると壁紙の色がそのまま透ける。加えて `TintOpacity` の既定は Light 0.5 / Dark 0.8 とテーマごとに異なり、`MicaController` / `DesktopAcrylicController` はテーマ変更時にこれらを自動で入れ直すが、**アプリ側が一度でも代入するとその値に固定され自動更新が止まる**。結果、片方のテーマ向けの値が両テーマに居座る。
- 再発防止: `MicaController` / `DesktopAcrylicController` では原則 `Kind` だけを設定し、`TintColor` / `TintOpacity` / `LuminosityOpacity` / `FallbackColor` に代入しない。「素材を薄くする」は Mica では成立しない要求として扱い、素材そのものの選択（Mica か Acrylic か）で解く。`src/Beacon.WinUI/ManagedMicaController.cs` に理由付きで実装済み。`ThinDesktopAcrylicBackdrop` は意図的に薄いAcrylicを作るため代入を残しているが、テーマごとの再導出が止まる点は同じ制約下にある。Beacon.WinUI用テストが無く機械的検出ができていないため未クローズ。
- 追記(2026-07-24): 「設定/Welcomeの素材をもう少しだけ濃く」の要望に対し、`SettingsAcrylic*Opacity` をcontrollerへ直接代入する案（案a）を再度採らず、素材値には触れずテーマ別の低アルファ・スクリム `AppSurfaceScrimBrush`（Light `#14FFFFFF` / Dark・Default `#1F000000` / HighContrast `Transparent`）を各テーマ辞書へ用意し、`SettingsRoot` と Welcome の `Root` の `Background` に `{ThemeResource}` で敷く方式（案b）で解決。`ThemeResource` なのでテーマ変更に自動追従し本教訓に完全準拠する。設定/Welcomeは `useCustomTuning:false` で素材既定値を使う。

## 2026-07-24: 独自SystemBackdropがWindowsのテーマ変更でアプリごと落ちる
- 事象: Beacon常駐中にWindows設定でテーマを切り替えると `System.ArgumentException: パラメーターが間違っています。 (target)` が未処理となりプロセス終了。スタックは `SystemBackdrop.OnDefaultSystemBackdropConfigurationChanged` → `ABI...Do_Abi_OnDefaultSystemBackdropConfigurationChanged_2`。
- 原因: `SystemBackdrop` を継承した独自バックドロップ（`ThinDesktopAcrylicBackdrop` 等）が `OnDefaultSystemBackdropConfigurationChanged` を未オーバーライドのままだった。自前の `SystemBackdropConfiguration` で制御しているため既定構成は使っていないのに、テーマ変更時にWinUIが基底実装を呼び、基底実装が接続済みtargetに対してE_INVALIDARGを投げる。
- 再発防止: 独自 `SystemBackdrop` は `OnTargetConnected` / `OnTargetDisconnected` に加えて **`OnDefaultSystemBackdropConfigurationChanged` を必ずオーバーライドする**（自前構成なら空実装）。`src/Beacon.WinUI/ThinDesktopAcrylicBackdrop.cs` に理由付きで実装済み。Beacon.WinUI用のテストプロジェクトが無く機械的検出ができていないため未クローズ。将来 WinUI 側にテストを置けたら「`SystemBackdrop` 派生型は当該メソッドの宣言を持つ」ことをリフレクションで検証してアーカイブへ移す。
