# TEST_MATRIX — テスト戦略

> 検証分担（2026-07-22ユーザー決定）: Codexは自動テスト・自動スモーク・起動確認までを行い、操作・UI・見た目・体感の確認はユーザーが目視で行う。詳細は `AGENTS.md`「完了条件」。以下の手動系はユーザー実施項目（Codexは一覧化して引き渡す）。

## 1. 自動テスト（フェーズごとに追加、CIで常時実行）

| 領域 | 内容 | Phase |
|---|---|---|
| Contracts | DTOシリアライズ往復・ContractVersion互換 | R2 |
| Query | キャンセル・逐次(incremental)結果・セッション無効化 | R2 |
| Ranking | スコア表・Web降格・履歴ブースト | R6 |
| History | 保存・読込・破損時復旧 | R6 |
| Settings migration | 旧Settings.json→新スキーマ（正常・欠損・破損） | R7 |
| PluginHost protocol | RPC契約・タイムアウト・クラッシュ復旧・不正ExecutionToken拒否 | R7 |
| Platform | Everythingあり/なし・Shellパス・IconDescriptor | R3 |
| DataRoot | portable.flag解決規則（あり/なし/flag消失+Data残存/書き込み不可） | R1〜 |
| Distribution | Portable publish→ZIP→展開のスモークテスト・更新マニフェスト検証 | R10 |
| 境界検証 | Contracts/CoreにWPF・WinUI参照がないことのビルド/grepチェック | R2〜(CI常設) |

テスト基盤は NUnit + Moq（Beacon-oldと同じ。新規フレームワークを増やさない）。

## 2. 毎フェーズ共通の確認（Codexが実施）

1. `dotnet build Beacon.sln` が0エラー（R1以降。R0はビルド対象なし）
2. `dotnet test` グリーン
3. 起動確認（R1以降）: `Beacon.Next.exe` 起動 → プロセス残存 → `<BeaconRoot>\Data\Logs\` にERROR/Exceptionなし → ホットキーで表示
4. Phaseの完了条件（PLAN.md）を1項目ずつ確認して報告

## 3. Gate時の手動・統合テスト

| 項目 | 条件 |
|---|---|
| OS | サポート最小Windows（R1で確定）/ 最新Windows |
| CPU | x64（ARM64は対応判断後） |
| DPI | 100 / 125 / 150 / 200%、起動中の変更 |
| モニター | 単一 / 複数 / DPI混在、カーソル位置起動 |
| テーマ | System / Light / Dark、実行中切替、High Contrast |
| IME | 日本語変換中・確定・キャンセルで高さと検索が暴れない |
| Everything | 未導入 / 停止中 / 稼働中 / 実行中停止 |
| プラグイン | 標準 / 第三者 / クラッシュ / 応答停止 / ResultKind未指定 |
| Portable | クリーン環境ZIP展開 / フォルダ移動 / USB / 読み取り専用で明確な失敗 / flag消失+Data残存の提示 / 更新 / ロールバック / フォルダ削除 |
| Windows統合 | 登録 / 解除 / フォルダ移動後の修復 |
| データ | 新規 / 旧版移行 / 破損設定 / 旧版並行運用 |
| 権限 | 通常 / 管理者アプリとの操作 |
| ネットワーク | オフライン / プロキシ |
| 性能 | 連続起動100回 / 高速入力・長文貼り付け / 大量結果 |
| アクセシビリティ | キーボードのみ全操作 / High Contrast / Reduce Motion相当 |

**各Gateでは「ビルド成功」ではなく、実際のRelease ZIPをクリーン環境へ展開して確認する。**
