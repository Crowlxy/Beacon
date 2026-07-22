# Beacon — WinUI 3 Portable-firstランチャー（独立リポジトリ）

新Beacon本体。旧WPF版（Flow Launcherフォーク）は `Crowlxy/Beacon-old` にあり、**移植元・比較参照のみ**（Project Reference / Submodule接続禁止）。ローカル配置: 本リポジトリ=`C:\Users\ha.takaku\Desktop\Project\Beacon`、Beacon-old=`C:\Users\ha.takaku\Desktop\Project\Beacon-old`。

## 役割分担
- **Claude**: 仕様策定・アーキテクチャ・フェーズ計画・Codexプロンプト作成・Gateレビュー（コードの実装はしない）。承認済みの製品方針を黙って変えない
- **Codex**: 実装・修正。`codex exec --model gpt-5.6-sol "<docs/rebuild/PROMPTS.mdのプロンプト>"`。指示書は `AGENTS.md`

## 正式ドキュメント（docs/rebuild/ が正）
優先順: `SPEC.md` > 承認済みADR(`adr/`) > `ARCHITECTURE.md` > `DISTRIBUTION.md` > `PLAN.md` > フェーズプロンプト。
補助: `AUDIT.md`（A部=Beacon-old事実/B部=採用構成）/ `DEPENDENCY_MAP.md` / `COMPATIBILITY.md` / `MIGRATION.md` / `TEST_MATRIX.md` / `RISK_REGISTER.md` / `LESSONS.md`（未解決の失敗のみ。環境の恒常制約は `AGENTS.md`「環境の既知制約」が正）。

## 承認済みの方向（2026-07-16）
- WinUI 3でUI新規構築 / 一次配布はPortable ZIP（Unpackaged + Self-contained WinAppSDK）
- 新規 `Beacon.sln` + `src/` + `tests/`（R1で生成。空プロジェクトの先行生成禁止）
- Beacon-oldからは**由来とMIT表記を維持した選択的移植のみ**（ADR-0001）
- Flow互換プラグイン（第三者プラグイン対応）は**実装対象外**（2026-07-20ユーザー決定。ADR-0003のPluginHost隔離設計は将来再開時のために維持、R1ダミーは現状維持）
- DataRoot: `portable.flag` + exe隣接 `Data\`。**保存先の無言切替禁止**。`%LOCALAPPDATA%\Beacon\Data` は将来の非Portable/MSIX用予約（ADR-0004）
- iNKORE UIへの依存・移植は一切なし（Gate Dでバイナリ監査）/ MSIX・StoreはR11任意でリリースをブロックしない

## ブランチ
**`main` が統合先**（常に使える状態を保つ）。実装は `feature/rebuild-rN-*` からPR。永続的な統合ブランチは使わない。Beacon-old側: `beacon`=旧WPF版凍結、`dev`=上流同期専用。

## 不変制約
- **`Spotlight` という語をコードに書かない**（識別子・XAML・コメント・リソースキー・UI文字列・ログ・ファイル名・ブランチ名）。ブランドは `Beacon`。docs散文は可
- `Beacon.Contracts` / `Beacon.Core` にWPF・WinUI・WinForms参照を入れない。WinUI本体プロセスへ `System.Windows.*` を持ち込まない
- 依存追加は必要性・ライセンス原文・バージョン・リリース影響を文書化してから（`DEPENDENCY_MAP.md` B部 + `attribution.md`）
- 素材は商用利用無料のみ: Segoe Fluent Icons(OSフォント参照のみ・TTF同梱禁止) / Fluent UI System Icons(MIT) / Lucide(ISC) / unDraw / WinUI標準Symbol。Apple固有素材禁止
- デザイン値は `src/Beacon.WinUI/Resources/DesignTokens.xaml` に集約。直値の色・寸法を新規に書かない
- 旧WPF版を壊さない・並行起動可能に保つ（開発中識別子 `Beacon.Next` / `.Next` サフィックス）
- プロセス境界を越えるのはシリアライズ可能なデータのみ（デリゲート・任意object・UI型は禁止）

## 運用
- 該当フェーズの仕様とADRが承認される前に実装しない。Codexプロンプトは**次の承認済みフェーズの分だけ**作る（現在は統合R5=旧R5〜R8。2026-07-20ユーザー決定）
- 失敗は `docs/rebuild/LESSONS.md` の記録基準を満たす場合のみ記録してから再試行。再発は文章追記でなく機械的防止（テスト→analyzer→CI→スクリプト→固定ルール）へ反映する
- 事実と推測を分ける。Windows App SDK / WinUI 3 / パッケージングの判断はMicrosoft公式一次情報で確認
- Gate A(R1後・技術成立性) / B(R4後・検索MVP) / C(統合R5後・完成度レビュー) / D(R10後・リリース)。Gateでは実際のRelease ZIPをクリーン環境で確認する
- 検証分担（2026-07-22ユーザー決定）: Codex（実装）は**自動検証と起動確認まで**を行い、**操作・UI・見た目・体感の確認はユーザーが目視で行う**。Codexは目視判定を代行せず、確認すべきUI/操作項目を一覧化して引き渡す（実装に問題がなければ起動まで済ませて引き渡す）。詳細は `AGENTS.md`「完了条件」
