# RISK_REGISTER — リスク登録簿

Gateごとに見直し、発生したものはLESSONS.mdと相互リンクする。

| # | リスク | 重要度 | 初期対策 | 検証Phase |
|---|---|---|---|---|
| 1 | Self-contained UnpackagedでPluginHost（複数プロセス）を安定起動できない | **最高** | Phase R1で最優先スパイク。失敗ならGate Aで設計修正 | R1 |
| 2 | グローバルホットキー / トレイがUnpackaged環境で不安定 | **最高** | R1で実機検証（RegisterHotKey + LLフック両方式） | R1 |
| 3 | iNKORE由来物（DLL・XAML・スタイル・フォント）が移植時に混入 | **最高** | 移植の全面禁止 + Gate Dでバイナリ/文字列/リソース監査 | R10 |
| 4 | 旧`Result`等の公開APIへのWPF型漏れ（確認済み: AUDIT §A2） | 高 | Contracts + PluginHostで隔離（ADR-0001/0003） | R2/R7 |
| 5 | 移植コードの由来・MIT表記漏れ（ライセンス違反） | 高 | 移植規則（ARCHITECTURE §2）+ attribution.md運用 + Gate D監査 | 全期間 |
| 6 | 第三者プラグインのUI互換（設定画面・プレビュー）がない | 高 | Tier表で範囲を明示し検索・実行を優先。非対応は理由表示 | R7 |
| 7 | 現行データ移行での破損 | 高 | 明示確認・バックアップ・ロールバック・並行起動（MIGRATION.md） | R9 |
| 8 | PluginHost往復レイテンシで体感が旧版より悪化 | 高 | 逐次配信・内蔵プロバイダー優先。Gate Bで実測 | R4/R7 |
| 9 | Portableと将来MSIXの二重保守 | 高 | 初回はPortableのみ。MSIXはR11へ分離しMVPをブロックしない | R11 |
| 10 | WinUI UIプロジェクトへロジックが再混入 | 高 | 参照方向のCIチェック（ADR-0001 §5） | R2〜 |
| 11 | PluginHostの権限・入力検証不備（不正Token・パストラバーサル） | 高 | 最小権限・パイプACL・セッション紐付けToken・タイムアウト | R7 |
| 12 | Windows App SDKの既知不具合・破壊的変更 | 中〜高 | R1で安定版固定しADR-0002へ記録。更新ルールを持つ | R1 |
| 13 | EverythingのARM64非対応 | 中〜高 | x64を必須、ARM64は検証後。Windows Indexフォールバック | R3/R10 |
| 14 | IME（日本語変換中のサイズ暴れ）がWinUIで再現困難 | 中〜高 | R4完了条件に固定。TextBox/TSFの挙動を早期確認 | R4 |
| 15 | Beacon-old凍結中に新旧コードが乖離し移植が難化 | 中 | Beacon-oldは凍結（バグ修正のみ）。移植は片方向 | 全期間 |
| 16 | Flow.Launcher.Localization等ソースジェネレータがWinUIで動かない | 中 | R1で検証。不可なら標準resx系を原文確認の上で判断 | R1 |
