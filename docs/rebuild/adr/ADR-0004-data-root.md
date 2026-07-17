# ADR-0004: DataRoot — ポータブルは exe隣接 `Data\` + `portable.flag`。保存先を無言で切り替えない

- 状態: **承認済み（2026-07-16 ユーザー承認、§2の解決規則を含む）**
- 日付: 2026-07-16

## 決定

1. ポータブルモードのデータルートは **`<BeaconRoot>\Data\`**（`Settings/ History/ Plugins/ Cache/ Logs/ Clipboard/ State/`）。**`portable.flag`（exe隣接）がポータブルモードの明示条件**。一次配布ZIPには `portable.flag` を同梱し、既定はポータブル。
2. 保存先の解決規則（**無言のフォールバック禁止**）:
   - `portable.flag` あり → `<BeaconRoot>\Data`。書き込み不可なら**起動前に明確なエラーと移動案内を表示して終了**（読み取り専用メディア・Program Files直下等）。AppDataへ黙って切り替えない
   - `portable.flag` なし・`<BeaconRoot>\Data` も存在しない → 非ポータブルモードとして `%LOCALAPPDATA%\Beacon\Data` を使用（**将来の非Portable/MSIX配布用に予約**。Roamingにしない: 検索キャッシュ・履歴は同期対象にすべきでない）
   - **`portable.flag` が消失したが `<BeaconRoot>\Data` が残っている → 無言で `%LOCALAPPDATA%` へ切り替えない。** 起動時に状態不整合を提示し、「flagを復元してポータブル継続」か「非ポータブルへ移行」をユーザーに選ばせる
3. DataRootの解決は**Beacon.exe起動時に一度だけ**行い、正規化した絶対パスを PluginHost / Updater へ**起動引数（または環境変数）で渡す**。各プロセスが独自判断で保存先を決めない。
4. 旧WPF版のデータ（`%APPDATA%\Beacon` / 旧ポータブル `UserData`）とは**ファイルを共有しない**。取り込みは移行ウィザード（Phase R9、MIGRATION.md）が唯一の経路。並行運用中の相互破壊を防ぐ。
5. 書き込み規則: テンポラリファイル経由のatomic rename / 設定更新前バックアップ / シンボリックリンク解決後にDataRoot配下であることを検証（パストラバーサル防止）/ 秘密情報をログへ出さない。
6. `%TEMP%` の利用は可（永続データを残さない）。レジストリ/AppDataへの永続書き込みは、ユーザーが明示的に有効化したWindows統合（DISTRIBUTION.md §5）に限定し、場所と削除方法を文書化する。

## 理由

- Beacon-oldの `DataLocation.cs`（exe隣接`UserData` + `.dead`指標）はポータブル先例として有効だが、(a) staticフィールドで起動時固定・プロセス間注入不可、(b) 旧版と同居した場合の衝突、の2点で新要件を満たさない。方式を継承しつつ `Beacon.Core` の `DataRootResolver` として新規実装する。
- フォルダ名を `UserData` から `Data` へ変えるのは、旧版ポータブルデータとの誤共有を構造的に防ぐため。
- 「flagが消えたのにDataが残っている」はUSB移動・部分コピーで起きやすく、無言でAppDataへ移ると**ユーザーから見てデータが消えた**ことになる。必ず提示して選ばせる。

## 帰結

- 非ポータブルモード（`%LOCALAPPDATA%\Beacon\Data`）はR10まで実装優先度が低い（一次配布はポータブル）。解決規則だけ先に実装し、テストで固定する。
