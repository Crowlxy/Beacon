# COMPATIBILITY — Flow Launcher互換レベル

「完全対応」とは書かない。互換はTierで段階管理する。

## 1. 互換Tier

| Tier | 対象 | 方針 | 対応Phase |
|---|---|---|---|
| 1 | 標準プラグインの検索結果・アクション | 内蔵プロバイダーとして移植（PluginHost不要）またはHost経由。MVPで対応 | R4/R6 |
| 2 | 第三者プラグインの検索結果・アクション | PluginHost経由（ADR-0003）。ExecutionTokenで実行 | R7 |
| 3 | プラグイン独自設定 | JSON/標準設定（Beacon-oldのJsonRPCPluginSettings形式）はWinUIで描画。**WPF専用設定UI（`ISettingProvider.CreateSettingPanel`）は非対応**（将来: 別ウィンドウ表示を検討課題として残すが要件にしない） | R7 |
| 4 | WPFカスタムプレビュー（`Result.PreviewPanel`） | **初期版では非対応**。`PreviewInfo`のデータ部分（画像パス・説明・FilePath）のみ利用。代替データ契約を将来提供 | R8以降 |
| 5 | Beaconネイティブ拡張API | 新API。Flow互換とは別に後から設計 | Gate D後 |

原則: **非対応機能は黙って壊さず、UI上で「このプラグインの設定/プレビューは旧版専用」と理由を表示する。**

## 2. 標準プラグイン分類（Phase R0で確定させる）

分類凡例: A=UI非依存のまま移植可 / B=少量のアダプターで移植可 / C=PluginHostへ隔離 / D=WinUI向けに書き直し / E=廃止候補。
「暫定」はClaudeの初期見立て（2026-07-16、プラグイン本体コードの精査前）。**R0でBeacon-oldのコードを読んで確定し、根拠ファイルを記入すること。**

| プラグイン（Beacon-old `Plugins/`） | 暫定 | 備考 |
|---|---|---|
| Program | B | 検索ロジックは移植、アイコン層のみ差し替え。内蔵プロバイダー化（R6優先1位） |
| Explorer (Everything/Windows Index) | B | Everything P/Invoke層はUI非依存確認済み。設定UI・コンテキストメニューはWPF → 内蔵プロバイダー化 |
| Calculator | A〜B | 計算エンジンは非依存見込み |
| Url | A〜B | |
| WebSearch | B | アイコンダウンロード周りを確認 |
| Sys | B〜D | シャットダウン等のコマンド群。UI混在度を確認 |
| WindowsSettings | A〜B | データ駆動 |
| BrowserBookmark | B | DB読み取りは非依存見込み |
| Shell | B | 実行履歴・昇格実行を確認 |
| ProcessKiller | B | |
| PluginsManager | C〜D | Flowエコシステム前提。Host経由か新規設計かをR0で判断 |
| PluginIndicator | C | Flowプラグイン一覧前提 |

## 3. 禁止事項

- プラグイン互換のためにWinUI本体プロセスへWPF/WinForms参照を追加しない
- `Flow.Launcher.Plugin.dll` 互換層の公開APIへ破壊的変更を入れない（追加は省略可能メンバーのみ）
- WPF `UserControl` / `ImageSource` をプロセス境界（RPC）越しに渡さない
- 移植時にFlow Launcher / WoxのMIT著作権表記を落とさない（ADR-0001 §3）

## 4. IPublicAPI の互換方針（Phase R7で表を完成）

PluginHost内の `IPublicAPI` 実装は各メソッドを次のいずれかに分類する:
1. **Host内で完結**（FuzzySearch, ログ, HTTPダウンロード等）
2. **本体へRPC委譲**（ShowMsgBox→WinUIダイアログ, ChangeQuery, ReQuery, OpenSettingDialog等）
3. **非対応（no-op + 警告ログ + 互換性表示）**（テーマAPI, `LoadImageAsync`のImageSource返却等WPF前提のもの）
