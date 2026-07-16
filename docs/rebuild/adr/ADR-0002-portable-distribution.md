# ADR-0002: 一次配布は Unpackaged + Self-contained の Portable ZIP

- 状態: 方式は承認済み（2026-07-16）。Windows App SDKバージョン・ライセンス欄はPhase R1の実測後に確定
- 日付: 2026-07-16

## 決定

1. 新Beaconの一次配布は **ZIPポータブル版（x64必須、ARM64は実機検証後）**。`Unpackaged + Self-contained Windows App SDK` を採用し、Runtime事前導入・インストーラ・管理者権限・Storeログインを要求しない。
2. MSIX/Storeは第二配布経路とし、Portable正式リリース（Gate D）後に別Phase（R11）で判断する。MVPをブロックしない。
3. Beacon-oldのsquirrel.windowsインストーラと`Scripts/post_build.ps1`は**新Beaconへ移植しない**（Portable生成はリブランドで破損していることを確認済み — AUDIT.md）。配布は `Beacon.Distribution`（publish + ZIP生成スクリプト）として新規に作る。
4. ZIP構造・更新方式・Windows統合のオプトイン規則は DISTRIBUTION.md を正とする。
5. Windows App SDKのバージョンは本ADRで固定しない。**Phase R1でMicrosoft公式の安定版・サポート期間・既知問題・対象Windowsバージョン・ライセンス原文を確認し、本ADRへ追記して固定する**。

### R1で記録する欄（未確認）

| 項目 | 値 |
|---|---|
| Windows App SDK バージョン | 未確認 |
| 最小Windows バージョン | 未確認 |
| Self-contained時の配布サイズ実測 | 未確認 |
| ライセンス（NuGet原文確認） | 未確認 |
| Unpackaged既知問題（ホットキー/トレイ/Backdrop） | 未確認 |

## 理由

- ランチャーはインストール不要・即起動・フォルダ削除で消える形が利用者要求に合う。
- Self-containedにより、クリーン環境でZIP展開→`Beacon.exe`直接起動が成立する（R1で最優先スパイク検証）。

## 帰結

- 配布サイズはFramework-dependentより大きくなる。許容する（事前導入不要を優先）。
- 更新はStoreに頼れないため、自前Updater（別プロセス・SHA-256検証・ロールバック）をDISTRIBUTION.md §4のとおり設計する。MVPは手動更新のみでも可。
