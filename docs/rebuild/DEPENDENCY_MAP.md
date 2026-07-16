# DEPENDENCY_MAP — 依存の事実（Beacon-old）と新Beaconでの採否

本書もAUDIT.mdと同じく **A部=Beacon-oldで確認した事実**（csproj実測 2026-07-16）と **B部=新Beaconに採用する構成** を分ける。
新Beaconは独立リポジトリのため、**ここに載る「継続」はすべて新規依存として追加し直す**ことになる。追加時は必要性・ライセンス原文・バージョン・リリース影響を文書化する（SPEC §8）。

---

# A部: Beacon-oldの依存（確認済み）

## A1. プロジェクト参照グラフ

```
Flow.Launcher (WPF exe)
 ├─ Flow.Launcher.Core ──┬─ Flow.Launcher.Infrastructure ── Flow.Launcher.Plugin
 │                        └─ Flow.Launcher.Plugin
 ├─ Flow.Launcher.Infrastructure
 └─ Flow.Launcher.Plugin
Plugins/* → Flow.Launcher.Plugin (+ 一部 Infrastructure/Core ※R0で確認)
```

## A2. NuGet依存（主要なもの）

| パッケージ | 使用箇所 | 備考 |
|---|---|---|
| iNKORE.UI.WPF.Modern 0.10.1 | Flow.Launcher | **独自ライセンス・商用書面許可** |
| MdXaml ×5 / VirtualizingWrapPanel / NHotkey.Wpf 3.0.0 / SharpVectors.Wpf | Flow.Launcher / Infrastructure | WPF専用 |
| ChefKeys 0.1.2 | Flow.Launcher | Winキー単独ホットキー。依存・ライセンス**未確認**（R0） |
| CommunityToolkit.Mvvm 8.4.0 | Flow.Launcher / Infrastructure | MIT・UI非依存 |
| Microsoft.Extensions.Hosting / DI 9.0.9 | Flow.Launcher | MIT |
| Microsoft.Toolkit.Uwp.Notifications 7.1.3 | Flow.Launcher | 通知 |
| TaskScheduler 2.12.2 | Flow.Launcher | スタートアップ登録 |
| Fody + PropertyChanged.Fody | 複数 | IL織り込み |
| Flow.Launcher.Localization 0.0.6 | 複数 | ソースジェネレータ。WinUI互換**未確認**（R0/R1） |
| **StreamJsonRpc 2.22.11** | Core（JsonRPCPluginV2系） | MIT。**実運用実績あり** |
| squirrel.windows 1.9.0 | Core | NU1701（.NET Framework時代）。更新・インストーラ |
| Droplex / FSharp.Core / InputSimulator | Core / Infrastructure | 用途**未確認**（R0） |
| Meziantou.Framework.Win32.Jobs 3.4.5 | Core | MIT。Job Object |
| Microsoft.Windows.CsWin32 0.3.298 | Infrastructure / Plugin | MIT・ソースジェネレータ |
| NLog 6 / MemoryPack / BitFaster.Caching / Microsoft.VisualStudio.Threading / ini-parser / ToolGood.Words.Pinyin / SemanticVersioning / Microsoft.IO.RecyclableMemoryStream | 各所 | 寛容ライセンス（BSD/MIT/Apache-2.0） |
| System.Drawing.Common 7.0.0 | Infrastructure | 用途**未確認**（R0。アイコン抽出と推定） |
| NUnit 4.4 + Moq 4.20 | Test | テスト基盤 |

## A3. 同梱バイナリ・素材

| 対象 | 事実 |
|---|---|
| `Plugins/...Explorer/EverythingSDK/Everything.dll` | MIT系（原文確認済み）。ARM64版有無**未確認** |
| `Flow.Launcher/Resources/SegoeFluentIcons.ttf` | Microsoftフォント、再配布権**不明** |
| iNKORE由来のXAML/スタイル/画像/フォント | Flow.Launcher内に多数 |

---

# B部: 新Beaconでの採否（行き先はARCHITECTURE.md §1のプロジェクト）

## B1. 採用（新規依存として追加。追加時にライセンス原文確認+記録）

| パッケージ | 行き先 | 状態 |
|---|---|---|
| Microsoft.WindowsAppSDK（WinUI 3本体） | Beacon.WinUI ほか | **R1で安定版選定・原文確認 → ADR-0002へ記録** |
| StreamJsonRpc | Beacon.WinUI / PluginHost / Contracts境界 | 第一候補（MIT・Beacon-old実績）。R1で確定（ADR-0003） |
| Microsoft.Windows.CsWin32 | Beacon.Platform.Windows | 採用予定（MIT） |
| CommunityToolkit.Mvvm | Beacon.WinUI / Core | 採用予定（MIT。Fodyは使わずソースジェネレータで） |
| Microsoft.Extensions.Hosting / DI | Beacon.WinUI | 採用予定（MIT） |
| NLog | Core / 各exe | 採用候補（BSD-3。DataRoot対応設定） |
| Meziantou.Framework.Win32.Jobs | Beacon.WinUI（PluginHost管理） | 採用候補（MIT） |
| NUnit + Moq | tests/ | 採用予定（Beacon-oldと同じ基盤。新フレームワークを増やさない） |
| Everything.dll（同梱） | Beacon.Platform.Windows | 採用（MIT系確認済み）。ARM64はR0確認後 |

## B2. 不採用（新Beaconへ持ち込まない）

| 対象 | 理由 |
|---|---|
| **iNKORE.UI.WPF.Modern**（+iNKORE.UI.WPF, Fizzler） | 独自ライセンス・商用制限。**Gate Dでバイナリ不在を監査** |
| NHotkey.Wpf / SharpVectors.Wpf / MdXaml×5 / VirtualizingWrapPanel | WPF専用 |
| squirrel.windows | Portable方針と不整合・老朽。更新は自前Updater（DISTRIBUTION §4） |
| Fody / PropertyChanged.Fody | Mvvmソースジェネレータで代替 |
| `SegoeFluentIcons.ttf` | 再配布権不明。OSフォント参照のみ |

## B3. 保留（R0/R1の確認結果で判断）

| 対象 | 判断材料 |
|---|---|
| ChefKeys | UI依存とライセンス（R0）。不可ならWH_KEYBOARD_LL移植で代替 |
| Flow.Launcher.Localization | WinUIで動くか（R1）。不可なら標準resx系 |
| Microsoft.Toolkit.Uwp.Notifications | WinAppSDKのAppNotificationで代替可能か（R1） |
| TaskScheduler | Portable版はStartupフォルダ.lnkが基本。管理者起動要件が出た時のみ再検討 |
| ToolGood.Words.Pinyin / BitFaster.Caching / MemoryPack ほか | 移植するロジックが実際に必要とした時点で追加（先回りで足さない） |

## B4. 原則

- **数行で書けるものにパッケージを足さない**。トレイ・ホットキー・単一インスタンスはCsWin32自前実装が第一候補
- 「Beacon-oldに存在する」ことは採用理由にならない。移植するロジックが要求した時点で最小限追加する
- 追加のたびに本書B1へ行を足し、`attribution.md` / `LICENSE`（表示義務があれば全文）を更新する
