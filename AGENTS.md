# Beacon 実装エージェント向けガイド (Codex)

あなたは新Beacon（WinUI 3 Portable-firstランチャー・独立リポジトリ）の実装担当。
作業前に必ず読む: `docs/rebuild/LESSONS.md`（失敗記録簿）→ `CLAUDE.md` → `docs/rebuild/PLAN.md` の該当Phase → 関連ADR。
タスクは `docs/rebuild/PROMPTS.md` のフェーズ単位で与えられる。

## リポジトリ配置（間違えないこと）
- 本リポジトリ（作業対象）: `C:\Users\ha.takaku\Desktop\Project\Beacon-winui`（Crowlxy/Beacon、統合先は`main`）
- Beacon-old（読み取り専用の移植元）: `C:\Users\ha.takaku\Desktop\Project\Beacon`（**フォルダ名はBeaconだが中身は旧WPF版**。変更・コミット禁止）

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
10. **失敗したら `docs/rebuild/LESSONS.md` へ記録してから再試行**（事象・原因・再発防止）。Beacon-old側 `docs/spotlight/LESSONS.md` の環境系教訓（プロセスロック・ログ排他・警告大量時のerror抽出・rg分割・BOM保持）も有効

## 作業の進め方

1. `docs/rebuild/LESSONS.md` を読む
2. プロンプトが指すPhaseの完了条件（`PLAN.md`）と関連ADRを読む
3. 変更対象・移植元ファイルの現状を読んでから書く（推測で書かない）
4. 最小の差分で実装。フェーズの指示にないリファクタリング・空プロジェクト生成をしない
5. 検索パスのキャンセル・非同期逐次配信を壊さない。契約・移行・障害復旧・挙動変更にはテストを足す
6. 自己レビュー: ビルド警告 / 参照方向違反 / `Spotlight` 混入 / ライセンス未確認依存 / Beacon-old側の意図しない変更、がないこと

## 完了条件（毎フェーズ共通、詳細は `docs/rebuild/TEST_MATRIX.md`）

1. `dotnet build Beacon.sln` が0エラー（R1以降）/ `dotnet test` グリーン
2. 起動確認（R1以降）: `Beacon.Next.exe` 起動 → プロセス残存 → `<BeaconRoot>\Data\Logs\` にERROR/Exceptionなし → ホットキーで表示
3. Phaseの完了条件を1項目ずつ確認した結果を報告
4. 報告フォーマット: 変更ファイル一覧 / 完了条件ごとの確認結果 / 仕様と異なる実装とその理由（原則ゼロ）/ 追加依存とライセンス確認結果 / 実行したテスト / 残リスク。コミットはユーザーが行う
