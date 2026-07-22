# MIGRATION — 旧WPF版（Beacon-old）からのデータ移行（Phase R7）

## 1. 移行元と移行先

| 移行元（旧WPF版の実行環境） | 移行先（新Portable版） |
|---|---|
| `%APPDATA%\Beacon\Settings\Settings.json` | `<BeaconRoot>\Data\Settings\`（新スキーマへ変換） |
| `%APPDATA%\Beacon\Settings\Plugins\*`（プラグイン設定） | `<BeaconRoot>\Data\Settings\Plugins\` |
| 履歴・選択回数・TopMostRecord（保存形式はR0で確認） | `<BeaconRoot>\Data\History\` |
| `%APPDATA%\Beacon\Plugins\`（ユーザー導入プラグイン） | `<BeaconRoot>\Plugins\`（互換Tier判定付き） |
| 旧ポータブル `<旧BeaconRoot>\UserData\` | 同上（`.dead`指標を尊重） |
| 移行しない: テーマ設定・UI寸法設定（QueryBoxFontSize等）・時計/サウンド設定 | 新版に該当機能がない（固定デザイン） |

引き継ぐ値の例: ホットキー / ColorScheme(System・Light・Dark) / 言語 / Everything設定 / カスタムショートカット / プラグイン有効無効・ActionKeyword。確定リストはPhase R9開始時にBeacon-oldの`Settings.cs`全プロパティを走査して作成する。

## 2. 移行フロー（一度だけ・明示的）

1. 初回起動時に旧データ（`%APPDATA%\Beacon` または旧`UserData`）を検出したら、**対象と移行先を表示して確認を求める**（自動では移行しない）
2. 移行前バックアップ: 旧データを `<BeaconRoot>\Data\Backup\legacy-<日付>\` へコピー
3. 変換・コピー → 整合性確認（ファイル数・主要キーの読み戻し）
4. 失敗時ロールバック: 新Data側の書き込みを消して未移行状態へ戻す。旧版はそのまま使い続けられる
5. `Data\State\migration.json` に移行バージョン・日時・結果を記録（再実行防止）
6. **元データはユーザー確認なしで削除しない**（移行成功後も残す。削除はユーザー操作）

## 3. 並行運用の安全規則

- 新旧でファイルを共有しない（ADR-0004 §4）。旧版は`%APPDATA%\Beacon`を使い続け、新版は触らない（読み取りは移行時のみ）
- 開発中identifier（`Beacon.Next` / Mutex・パイプ別名）により単一インスタンス機構が新旧で衝突しないこと（R1で検証）
- ホットキーの二重登録: 新版初回起動時に旧版稼働を検出したら警告。開発中は既定をAlt+Shift+Space等にして回避可

## 4. プラグイン互換API

- `Flow.Launcher.Plugin.dll` 互換層の公開APIに破壊的変更を入れない（COMPATIBILITY.md §3）
- `ResultKind` 等の追加は省略可能メンバーのみ（未指定=Unknown）
