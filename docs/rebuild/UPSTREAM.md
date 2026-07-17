# UPSTREAM — Flow Launcher上流からの選択的移植手順

新Beaconへ上流リポジトリを直接接続しない。更新はBeacon-oldの`dev`で受け、監査済みファイルだけを本リポジトリへ移植する。

## 1. 前提

- Beacon-old: `C:\Users\ha.takaku\Desktop\Project\Beacon-old`
- 本リポジトリ: `C:\Users\ha.takaku\Desktop\Project\Beacon`
- Beacon-oldの`origin`: `https://github.com/Crowlxy/Beacon-old.git`
- 上流: `https://github.com/Flow-Launcher/Flow.Launcher.git`
- Beacon-oldの`beacon`は旧版保守、`dev`は上流同期専用とする。
- 作業開始前に両リポジトリの`git status --short`を保存する。未コミット変更がある作業ツリーではブランチを切り替えない。

## 2. Beacon-oldのdevを上流へ同期

Beacon-oldのクリーンな別チェックアウトで実行する。

```powershell
git remote -v
git fetch upstream dev
git switch dev
git pull --ff-only origin dev
git merge --ff-only upstream/dev
dotnet build Flow.Launcher.sln
dotnet test Flow.Launcher.sln --no-build
git push origin dev
```

`upstream`が未登録の場合だけ、最初に次を実行する。

```powershell
git remote add upstream https://github.com/Flow-Launcher/Flow.Launcher.git
```

`--ff-only`が失敗した場合は履歴を書き換えない。差分と競合理由を確認し、Beacon-old側の通常PRとして解決する。新Beacon側で先に取り込まない。

## 3. 移植候補を選ぶ

同期前後のBeacon-oldコミットを固定し、変更ファイルを列挙する。

```powershell
git diff --name-status <同期前コミット>..<同期後コミット>
git diff <同期前コミット>..<同期後コミット> -- <候補ファイル>
```

候補ごとに次を確認する。

1. `docs/rebuild/AUDIT.md`のファイルマップに存在し、行き先プロジェクトと分類が確定している。
2. WPF、WinForms、WinUIのUI型が`Beacon.Contracts`または`Beacon.Core`へ入らない。
3. iNKORE由来のコード、XAML、スタイル、画像、フォントではない。
4. 新しい依存が必要なら、追加前に`DEPENDENCY_MAP.md` B部へ必要性、バージョン、NuGetライセンス原文、配布影響を記録する。
5. 検索処理のキャンセルと非同期逐次配信を維持できる。

## 4. ファイル単位で移植

ディレクトリ一括コピー、Project Reference、Submoduleは使わない。候補ファイルの必要な型またはロジックだけを、AUDIT.mdに記載された行き先へ移す。UI型、旧設定UI、旧テーマ、配布スクリプトは同時に持ち込まない。

移植記録には次を残す。

| 項目 | 記録値 |
|---|---|
| 上流リポジトリ | `Flow-Launcher/Flow.Launcher` |
| 上流コミット | 40桁SHA |
| Beacon-old経由コミット | 40桁SHA |
| 移植元 | Beacon-old内の相対パス |
| 移植先 | 本リポジトリ内の相対パス |
| 分類 | 再利用 / アダプト / 書き直し / 廃止 |
| 変更理由 | UI境界、DTO化、DataRoot対応など |
| ライセンス | Flow Launcher / WoxのMIT表記と原文確認先 |

`attribution.md`へ同じ由来を追記し、元ファイルに著作権ヘッダーがある場合は維持する。表示義務のあるライセンス本文は配布物の第三者表記へ含める。

## 5. 検証

```powershell
rg -n "using System.Windows|Microsoft.UI.Xaml|Windows.UI.Xaml" src/Beacon.Contracts src/Beacon.Core
rg -n "iNKORE|SegoeFluentIcons.ttf" src tests
dotnet build Beacon.sln
dotnet test Beacon.sln
```

`rg`の最初の2コマンドは、該当禁止物が0件であることを確認する。移植対象の契約、移行、障害復旧、挙動変更には最小の回帰テストを追加する。最後にBeacon-oldの`git status --short`が同期作業として意図した差分だけ、本リポジトリの`git diff`が選択したファイルだけであることを確認する。