# DISTRIBUTION — 配布設計（Portable First）

決定の背景は ADR-0002（Portable）/ ADR-0004（DataRoot）。

## 1. 一次配布: Portable ZIP

```
Beacon-Portable-x64.zip
└─ Beacon/
   ├─ Beacon.exe                （開発中は Beacon.Next.exe）
   ├─ Beacon.PluginHost.exe
   ├─ Beacon.Updater.exe        （更新方式確定後）
   ├─ runtimes/ ほかSelf-contained一式
   ├─ Plugins/                  （標準プラグイン）
   ├─ Data/                     （初回起動時に生成: Settings/ History/ Plugins/ Cache/ Logs/ Clipboard/ State/）
   ├─ LICENSE
   ├─ attribution.md
   └─ portable.flag
```

構成: `Unpackaged + Self-contained Windows App SDK`。要件:

- Windows App SDK Runtime・.NET Runtimeの事前インストール不要
- インストーラー・管理者権限・Storeログイン不要。ZIP展開→`Beacon.exe`起動で完了
- 永続データは原則 `<BeaconRoot>\Data` 内のみ（ADR-0004）。USB・別ドライブ・任意フォルダへ移動しても動く
- アンインストール=フォルダ削除
- 初回リリースはx64必須。ARM64は実機検証後（Everything.dllのARM64可否が前提 — 未確認）
- アプリフォルダが書き込み不可なら、**無言でAppDataへフォールバックせず**明確なエラーと移動案内を表示（ADR-0004 §2）
- `%TEMP%`利用可（永続データを残さない）

## 2. ビルド（Beacon.Distribution）

- `dotnet publish -c Release -r win-x64 --self-contained` + WinAppSDK Self-containedフラグ（R1で確定した設定をここへ記録）
- ZIP生成はスクリプト化し再現可能にする（不要ファイル除去・`portable.flag`同梱・LICENSE類同梱）
- Beacon-oldの `Scripts/post_build.ps1`（squirrel + 破損したPortable生成）は**移植しない**

## 3. 検証（Gate D前に毎回）

実際のRelease ZIPを**クリーン環境**（WinAppSDK/Runtime未導入のWindows）へ展開して確認する。「ビルド成功」をもって配布可能と判定しない。

## 4. 更新方式

MVPは**手動更新**（新ZIP展開 + `Data\`移設手順の文書化）でよい。R1でダミー差し替え・ロールバックの手順を検証する。自動更新は独立フェーズで:

```
Beacon.exe → 更新確認 → マニフェスト取得 → 新ZIPを%TEMP%へ取得・SHA-256検証
→ Beacon.Updater.exe起動・本体終了 → 既存ファイルバックアップ → 差し替え → 失敗時ロールバック
```

要件: 無効化可能 / SHA-256検証（署名導入時は署名検証も）/ `Data\`・`Plugins\`・ユーザー追加ファイルを上書きしない / ロックされたファイルの差し替えは本体終了後の別プロセスで / 失敗後も旧版起動可 / オフラインでも通常利用可 / マニフェストスキーマをバージョン管理 / ダウングレード防止。

## 5. Windows統合（すべてオプトイン）

ポータブル性を壊す統合は**ユーザーの明示操作**でのみ有効化し、設定画面から解除可能にする:

| 統合 | 実装 | 書き込み先（文書化必須） |
|---|---|---|
| スタートアップ登録 | Startupフォルダの.lnk | `shell:startup` |
| ショートカット | スタートメニュー/デスクトップ.lnk | 該当フォルダ |
| アプリ実行エイリアス / URI・ファイル関連付け / Explorer連携 / 通知登録 | レジストリHKCU | 作成キーを設定画面に列挙 |

フォルダ移動を検知した場合（登録パスと実行パスの不一致）、古い登録の修復または削除の導線を出す。

## 6. 二次配布: MSIX / Store（Phase R11・任意）

Portable正式リリース後にGo/No-Go判断。Single-project MSIX / WAP / Full Trust（PluginHost）/ App Installer / x64・ARM64 / Portable版との設定移行（このとき `%LOCALAPPDATA%\Beacon\Data` を使用 — ADR-0004）。**MSIXのためにPortable版アーキテクチャを複雑化しない**が、Core/Contracts/PluginHost境界は将来パッケージ化できる形を維持する。

## 7. Gate D 受け入れ表

- [ ] クリーン環境でZIP展開後に起動可能（Runtime事前導入なし）
- [ ] 任意の書き込み可能フォルダへ移動して利用可能
- [ ] 更新で設定・履歴・プラグインが保持される / 失敗時ロールバック可
- [ ] 永続データが原則 `<BeaconRoot>\Data` 内に収まる
- [ ] フォルダ削除で本体除去 / 明示登録したWindows統合を解除できる
- [ ] 配布物にiNKORE DLL・SegoeFluentIcons.ttf・iNKORE派生XAMLが存在しない（バイナリ監査）
- [ ] SBOM/依存一覧が生成され、LICENSE・attribution.mdが実配布物と一致
