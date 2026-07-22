# Beacon 実装エージェント向けガイド (Codex)

あなたは新Beacon（WinUI 3 Portable-firstランチャー・独立リポジトリ）の実装担当。
作業前に必ず読む: 本ファイル（特に「環境の既知制約」）→ `docs/rebuild/LESSONS.md`（未解決の失敗のみ）→ `CLAUDE.md` → `docs/rebuild/PLAN.md` の該当Phase → 関連ADR。
タスクは `docs/rebuild/PROMPTS.md` のフェーズ単位で与えられる。

## リポジトリ配置（間違えないこと）
- 本リポジトリ（作業対象）: `C:\Users\ha.takaku\Desktop\Project\Beacon`（Crowlxy/Beacon、統合先は`main`）
- Beacon-old（読み取り専用の移植元）: `C:\Users\ha.takaku\Desktop\Project\Beacon-old`（旧WPF版。変更・コミット禁止）

## 必須制約

1. **iNKORE禁止**: `iNKORE.UI.WPF.Modern` およびiNKORE由来のXAML・スタイル・テンプレート・画像・フォントを参照・コピーしない。`SegoeFluentIcons.ttf` も同梱しない
2. **参照方向**: `Beacon.Contracts` / `Beacon.Core` にWPF・WinUI UI型を参照させない。WinUI本体プロセスへ `System.Windows.*` を持ち込まない。Flow互換WPF APIは `Beacon.PluginHost` に隔離
3. **Beacon-oldを丸ごとコピーしない**。移植はAUDIT.mdで分類済みのファイル単位のみ。由来（Beacon-old内パス）を記録し、Flow Launcher/WoxのMIT著作権表記を維持（`attribution.md`）
4. **依存追加**: 必要性・ライセンス原文・バージョン・リリース影響を `DEPENDENCY_MAP.md` B部へ文書化してから。数行で書けるものにパッケージを足さない。「たぶんMIT」禁止
5. **Portable ZIPが一次配布**。MSIXを前提条件にしない。永続データは解決済み `DataRoot`（exe隣接 `Data\`）配下のみ。**保存先の無言切替禁止**（ADR-0004）
6. **プロセス境界**: デリゲート・UIオブジェクト・任意の`object`をシリアライズ/RPC越しに渡さない。ExecutionToken方式（ADR-0003）。境界入力はすべて検証する
7. **`Spotlight` という語をコードに書かない**（C#識別子・XAML・コメント・リソースキー・UI文字列・ログ・ファイル名・ブランチ名すべて）。ブランドは `Beacon`
8. デザイン値は `src/Beacon.WinUI/Resources/DesignTokens.xaml` のトークンを追加・参照。直値の色・寸法を新規に書かない
9. 仕様の解釈は `SPEC.md > 承認済みADR > ARCHITECTURE.md > DISTRIBUTION.md > PLAN.md` の優先順。独自判断で仕様を変えない。矛盾は実装せず報告
10. **失敗時は `docs/rebuild/LESSONS.md` の記録基準を満たす場合のみ記録してから再試行**。下記「環境の既知制約」に該当する失敗は再記録せず、固定ルールに従って即座に切り替える

## 環境の既知制約（固定ルール）

過去に繰り返し発生した、自動化で防げない環境固有の制約。該当時はLESSONSへ記録せずこのルールに従う。

- **ヘッドレス `codex exec` は使わない**（サンドボックスが `CreateProcessWithLogonW failed: 5` で補助プロセス起動を拒否する）。対話実行し、同エラーが出たら再試行せず承認済みの個別実行へ切り替える
- **パス・ファイル名を推測しない**。ディレクトリ列挙（`rg --files` 等）で実在確認した名前だけを読む。SDK生成物（obj/bin配下）も実列挙してから検索する
- **パッチ・引数を崩さない**: バッククォート・PowerShell行継続・補間文字列（`$()`）を含めない。複数行パッチはバッチを介さずArgumentListで単一引数として渡す。Windowsオブジェクト名・パスのC#はverbatim文字列。using追加は先頭の既存usingを明示contextにする
- **一時PowerShellでも複雑な式を避ける**（switch式・foreach直後のパイプは解析エラー）。ファイル上書きは、Gitで現状確認→`$ErrorActionPreference='Stop'`→生成文字列の検証、を済ませてから1回だけ書く
- **1ログファイルの書き込みプロセスは1つ**（`FileMode.Append`は非アトミック）。ログ読取は`FileShare.ReadWrite`。ログ出現待ちのテストはフェーズ跨ぎでログを初期化するかオフセットで判定する
- ビルド前にOutput/Debug使用中プロセスを停止。大量警告時はerror行だけ抽出。rg検索は単純な語へ分割。BOMを保持。push前に`git remote -v`を確認

## 作業の進め方

1. `docs/rebuild/LESSONS.md` を読む
2. プロンプトが指すPhaseの完了条件（`PLAN.md`）と関連ADRを読む
3. 変更対象・移植元ファイルの現状を読んでから書く（推測で書かない）
4. 最小の差分で実装。フェーズの指示にないリファクタリング・空プロジェクト生成をしない
5. 検索パスのキャンセル・非同期逐次配信を壊さない。契約・移行・障害復旧・挙動変更にはテストを足す
6. 自己レビュー: ビルド警告 / 参照方向違反 / `Spotlight` 混入 / ライセンス未確認依存 / Beacon-old側の意図しない変更、がないこと

## 完了条件（毎フェーズ共通、詳細は `docs/rebuild/TEST_MATRIX.md`）

**検証は分担する（2026-07-22ユーザー決定）**: Codexは**自動検証と「起動まで」**を行う。**操作・UI・見た目・体感の確認はユーザーが目視で行う**。Codexは目視・体感の判定を代行せず（スクリーンショット比較・アニメの滑らかさ・配色/アクリルの見え・IMEやDPIの見えなどを「確認した」と報告しない）、該当項目は「ユーザーが目視確認する項目」として一覧化して引き渡す。

### Codexが行う（自動化・機械確認できるもの）
1. `dotnet build Beacon.sln`（R1以降 `-c Release` で0警告・0エラー）/ `dotnet test` グリーン
2. `Test-NoUiReferences.ps1` 0件 / `Build-Portable.ps1` → 自動スモーク（`Test-Portable.ps1` 既定・`-UseActivationPipe`）成功 / ZIP内容の機械確認（必要DLLの有無）
3. ログの機械チェック: `<BeaconRoot>\Data\Logs\` にERROR/Exception/Providerデッドライン超過がないこと。自動採取できる実測値（PERFログ等）を採取して報告
4. **仕上げに `Beacon.Next.exe` を起動して常駐状態にする（起動確認まで）**。自動スモーク由来のプロセスが残っていないことを確認したうえで、引き渡し用に起動 → プロセス残存・ログERROR無し・トレイ常駐を確認したら、そこで作業を止めてユーザーへ引き渡す。**UIの目視・操作テスト・ホットキー表示の見た目確認へは踏み込まない**

### ユーザーが行う（目視・操作・体感。Codexは実施しない）
- 操作・UI・レイアウト・配色・アクリル/枠/角丸の見え方、展開/収縮やアニメの滑らかさ・体感速度
- 日本語IMEの入力/変換/確定 / Light・Dark / DPI 100〜200% / 複数モニター / Everything導入環境などでの見え方
- スクリーンショット比較、連続打鍵・ホットキー反復などの手動採取（※自動スモークで採取できる範囲はCodexが採る）

### 報告フォーマット
変更ファイル一覧 / Codex検証の結果（上記1〜4）/ **ユーザーが目視確認すべき項目の一覧（何をどう見ればよいか）** / Phase完了条件の項目別確認結果（目視項目は「ユーザー確認待ち」と明記）/ 仕様と異なる実装とその理由（原則ゼロ）/ 追加依存とライセンス確認結果 / 実行したテスト / 残リスク。コミットはユーザーが行う
