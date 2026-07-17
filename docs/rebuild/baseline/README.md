# 現行UIベースライン取得手順

## 状態

2026-07-16のPhase R0実行環境は非対話シェルで、旧WPF版のウィンドウ表示、グローバルホットキー入力、画面キャプチャ結果を目視検証できない。既知の自動入力試行も表示確認に失敗しているため、画像は未取得。ユーザーによる対話デスクトップ上の取得が必要。

## 取得する画像

| ファイル名 | 状態 |
|---|---|
| `launcher-empty.png` | ホットキー表示直後、検索文字なし |
| `launcher-results.png` | 検索語入力後、結果一覧が展開された状態 |
| `settings.png` | 設定画面の全体 |

## 手順

1. `C:\Users\ha.takaku\Desktop\Project\Beacon-old\Output\Debug\Beacon.exe`を起動する。
2. 既存プロセスがある場合は、新規PIDではなく実際に残る単一インスタンスを対象にする。
3. `%APPDATA%\Beacon\Settings\Settings.json`、またはポータブル時の`Output\Debug\UserData\Settings\Settings.json`で現在のホットキーを確認する。既定値だけを前提にしない。
4. 現在のホットキーを押し、検索文字なしの状態をPNGで`launcher-empty.png`へ保存する。
5. `notepad`など結果が複数表示される検索語を入力し、一覧展開後を`launcher-results.png`へ保存する。
6. トレイメニューまたはアプリ内操作から設定画面を開き、`settings.png`へ保存する。
7. 3画像をこのディレクトリへ配置する。個人名、パス、履歴、クリップボード内容などが写った場合は再取得し、画像編集で隠さない。
8. 100% DPIで取得し、Windowsの表示倍率、解像度、テーマ、Beacon-oldコミットSHAを本ファイル末尾へ追記する。

## 取得メタデータ

- Beacon-oldコミット: 未取得
- Windows表示倍率: 未取得
- 解像度: 未取得
- テーマ: 未取得