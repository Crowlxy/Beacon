# ADR-0002: 一次配布は Unpackaged + Self-contained の Portable ZIP

- 状態: 方式は承認済み（2026-07-16）。Windows App SDKバージョン・ライセンス欄はPhase R1の実測後に確定
- 日付: 2026-07-16

## 決定

1. 新Beaconの一次配布は **ZIPポータブル版（x64必須、ARM64は実機検証後）**。`Unpackaged + Self-contained Windows App SDK` を採用し、Runtime事前導入・インストーラ・管理者権限・Storeログインを要求しない。
2. MSIX/Storeは第二配布経路とし、Portable正式リリース（Gate D）後に別Phase（R11）で判断する。MVPをブロックしない。
3. Beacon-oldのsquirrel.windowsインストーラと`Scripts/post_build.ps1`は**新Beaconへ移植しない**（Portable生成はリブランドで破損していることを確認済み — AUDIT.md）。配布は `Beacon.Distribution`（publish + ZIP生成スクリプト）として新規に作る。
4. ZIP構造・更新方式・Windows統合のオプトイン規則は DISTRIBUTION.md を正とする。
5. Windows App SDKのバージョンは本ADRで固定しない。**Phase R1でMicrosoft公式の安定版・サポート期間・既知問題・対象Windowsバージョン・ライセンス原文を確認し、本ADRへ追記して固定する**。

### R1実測（2026-07-17）

| 項目 | 値 |
|---|---|
| Windows App SDK バージョン | **2.2.0**。Microsoft公式の[ダウンロード一覧](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads)でStable（2026-06-09公開）、[リリースノート](https://learn.microsoft.com/windows/apps/windows-app-sdk/release-notes/windows-app-sdk-2-0?pivots=stable)でも2.2.0 stableを確認 |
| 最小Windows バージョン | **Windows 10 version 1809 / build 17763**。`TargetPlatformMinVersion` と `SupportedOSPlatformVersion` はともに `10.0.17763.0` で、Microsoft公式の[バージョン対応表](https://learn.microsoft.com/windows/apps/get-started/versioning-overview)と一致。TFMの`10.0.19041.0`はコンパイル対象SDKであり最小実行OSではない |
| Self-contained時の配布サイズ実測 | `artifacts/Beacon-Portable-x64.zip` **91,454,235 bytes（87.22 MiB）**（2026-07-17最終検証）。WPF RuntimePack除去前は117,642,225 bytes（112.19 MiB）、差は26,187,990 bytes（24.97 MiB） |
| ライセンス（NuGet原文確認） | `Microsoft.WindowsAppSDK.2.2.0.nupkg`内`license.txt`の名称は **MICROSOFT SOFTWARE LICENSE TERMS / MICROSOFT WINDOWS APP SDK**（MITではない）。§3(a)(i)でWindowsAppSDK NuGetがアプリへbinplaceしたファイルはframework-dependent/self-containedとも再頒布可能。§3(b)の付加機能、利用者・配布者条件、補償要件に従う。推移依存は[DEPENDENCY_MAP.md](../DEPENDENCY_MAP.md) B部に記録 |
| Unpackaged既知問題（ホットキー/トレイ/Backdrop） | ホットキーはWindows App SDK APIではなくWin32 [`RegisterHotKey`](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-registerhotkey)を使用し、他アプリとの組合せ競合時は登録失敗する。トレイもWin32 [`Shell_NotifyIcon`](https://learn.microsoft.com/windows/win32/api/shellapi/nf-shellapi-shell_notifyiconw)を使用し、Windows App SDKのモダンtray APIは[提案 #713](https://github.com/microsoft/WindowsAppSDK/issues/713)が未実装。Backdropは実行時サポート判定とfallbackが必要で、MicaはWindows 11のみ、Windows 10では単色fallback（[Microsoft公式](https://learn.microsoft.com/windows/apps/develop/ui/materials)）。これらR1採用経路はpackage identityを要求しない |

Self-contained配布方式はMicrosoft公式の[展開ガイド](https://learn.microsoft.com/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps)どおり、UnpackagedではWindows App SDK依存をexe隣接へ配置する。ホットキーの物理キー送出が不安定なCIでは、単一インスタンスのアクティベーションパイプをスモーク入口に使う。

## 理由

- ランチャーはインストール不要・即起動・フォルダ削除で消える形が利用者要求に合う。
- Self-containedにより、クリーン環境でZIP展開→`Beacon.exe`直接起動が成立する（R1で最優先スパイク検証）。

## 帰結

- 配布サイズはFramework-dependentより大きくなる。許容する（事前導入不要を優先）。
- 更新はStoreに頼れないため、自前Updater（別プロセス・SHA-256検証・ロールバック）をDISTRIBUTION.md §4のとおり設計する。MVPは手動更新のみでも可。
