# Beacon rebuild documentation

本ディレクトリがWinUI 3 Portable再構築の正式ドキュメント（source of truth）。

## 文書優先順位（矛盾時）

1. `SPEC.md`
2. 承認済みADR（`adr/`）
3. `ARCHITECTURE.md`
4. `DISTRIBUTION.md`
5. `PLAN.md`
6. フェーズ実装プロンプト（`PROMPTS.md`）

矛盾は黙って解釈せず報告する。

## 現在のフェーズ

**Phase R0（現状監査）実行中。** Beacon.slnおよびソースプロジェクトはR1の技術スパイクまで生成しない。

## 文書一覧

- `SPEC.md` — 製品要件・非ゴール・承認済み前提
- `ARCHITECTURE.md` — プロジェクト構成・プロセス境界・移植規則
- `PLAN.md` — Phase R0〜R11とGate A〜D
- `AUDIT.md` — A部: Beacon-oldで確認した事実 / B部: 新Beaconに採用済みの構成
- `DEPENDENCY_MAP.md` — 依存の事実と採否
- `COMPATIBILITY.md` — Flowプラグイン互換Tier
- `DISTRIBUTION.md` — Portable ZIP・DataRoot・更新・任意MSIX
- `MIGRATION.md` — 設定・履歴・プラグインの移行
- `TEST_MATRIX.md` — 自動・統合・リリーステスト
- `RISK_REGISTER.md` — 主要リスクと対策
- `PROMPTS.md` — 承認済みフェーズのCodexプロンプト（現在R0/R1のみ）
- `LESSONS.md` — 失敗記録簿（作業前必読）
- `adr/` — アーキテクチャ決定記録（0001境界 / 0002配布 / 0003 PluginHost / 0004 DataRoot）

旧WPF計画の文書は `Crowlxy/Beacon-old` の `docs/spotlight/` にあり、本リポジトリを拘束しない（実装禁止・参照は移植の参考のみ）。
