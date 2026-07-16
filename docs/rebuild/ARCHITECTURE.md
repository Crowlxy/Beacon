# ARCHITECTURE — Beacon WinUI 3 アーキテクチャ

前提の決定は adr/ 参照（0001境界 / 0002配布 / 0003 PluginHost / 0004 DataRoot）。

## 1. リポジトリとソリューション構成（独立構成・ADR-0001）

- 本リポジトリ `Crowlxy/Beacon` に新規 **`Beacon.sln`** を作る（Phase R1で生成。図に合わせるためだけに全プロジェクトを先行生成しない）
- 旧WPF版は **`Crowlxy/Beacon-old`**（別リポジトリ）。Project Reference / Git Submodule で接続せず、選択的移植のみ

```
Beacon.sln
├─ src/
│  ├─ Beacon.Contracts/         … net9.0。シリアライズ可能なDTO・enum・interfaceのみ。依存ゼロ
│  ├─ Beacon.Core/              … net9.0。クエリ編成/ランキング/履歴/アクション/設定抽象/DataRootResolver
│  ├─ Beacon.Platform.Windows/  … net9.0-windows。Everything/Shell/アイコン/プロセス起動/クリップボード (CsWin32)
│  ├─ Beacon.PluginHost/        … exe。Flow互換プラグインのロード（WPF参照可）
│  ├─ Beacon.WinUI/             … WinUI 3 exe（開発中識別子: Beacon.Next.exe）
│  └─ Beacon.Distribution/      … publish + Portable ZIP生成・更新マニフェスト
└─ tests/
   ├─ Beacon.Core.Tests/
   ├─ Beacon.Platform.Windows.Tests/
   ├─ Beacon.PluginHost.Tests/
   └─ Beacon.IntegrationTests/
```

参照方向(違反はCIでブロック):

```
Beacon.WinUI ─→ Beacon.Core ─→ Beacon.Contracts
     │               ↑
     │        Beacon.Platform.Windows ─→ Beacon.Contracts
     │
     └─(プロセス境界: Named Pipe JSON-RPC)─→ Beacon.PluginHost ─→ Flow互換アセンブリ（移植物）
```

- Contracts / Core は WPF・WinUI・WinForms を参照しない（ADR-0001。ビルド設定+CI grepで強制）
- WinUI本体プロセスへ `System.Windows.*` を持ち込まない
- PluginHost だけがWPF互換の世界に触れる

## 2. Beacon-old からの移植規則

1. 移植前に AUDIT.md の分類（再利用/アダプト/書き直し/廃止）で判定済みであること
2. ファイル単位の選択的コピー。由来（Beacon-old内パス・Flow Launcher/Wox由来）を記録し、MIT著作権表記を維持（`attribution.md` / `LICENSE`）
3. iNKORE由来のコード・XAML・スタイル・画像・フォント、`SegoeFluentIcons.ttf` は**移植禁止**
4. 移植は片方向（Beacon-old → Beacon）。逆方向の同期はしない

## 3. プロセス構成

```
Beacon.exe (WinUI 3)                      Beacon.PluginHost.exe
├─ AppWindow / 検索UI / 設定UI             ├─ Flow互換.NETプラグイン (WPF可)
├─ Beacon.Core (内蔵プロバイダー:          ├─ JsonRPC系プラグイン (Python/Node/Exe)
│   Program/Files/Calculator/URL…)        └─ Result→DTO変換・ExecutionToken管理
├─ ホットキー/トレイ/単一インスタンス
└─ RPCクライアント ⇄ versioned JSON-RPC over Named Pipe

Beacon.Updater.exe … 更新差し替え専用（DISTRIBUTION.md §4）
```

- 内蔵プロバイダー（R3/R6で移植する標準検索）は**本体プロセス内**のBeacon.Core経由。PluginHostが死んでも基本検索は生きる
- PluginHostはJob Objectで本体に紐づけ、孤児化させない

## 4. 検索契約（Beacon.Contracts — 概形。確定はPhase R2）

```csharp
public sealed record SearchRequest(
    string SessionId, string RawQuery, QueryScope Scope,
    int ContractVersion /* … */);

public sealed record SearchResultDto
{
    public required string Id { get; init; }            // セッション内一意
    public required string ProviderId { get; init; }
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public ResultKind Kind { get; init; }               // Unknown既定
    public double Score { get; init; }
    public IconDescriptor Icon { get; init; }
    public string? ExecutionToken { get; init; }        // Host側で実体デリゲートに解決
    public string? CopyText { get; init; }
    public string? AutoCompleteText { get; init; }
    public string? FilePath { get; init; }
}

public sealed record IconDescriptor(IconSource Source, string? Value);
public enum IconSource { None, FilePath, FileShellIcon, FileThumbnail, UriOrDataUri, FluentGlyph, ProviderIcon }
```

原則: UI型を含めない / デリゲート・任意`object`を含めない / 実行はExecutionTokenで元プロセスへ要求 / キャンセル可能な非同期 / 逐次(partial)配信 / ContractVersionを持つ / 古い・不正な結果を実行しない。

旧 `Result`（Beacon-old）からの写像（PluginHost内）:

| 旧 `Result` | DTO |
|---|---|
| Title/SubTitle/Score/CopyText/AutoCompleteText/RecordKey | そのまま |
| IcoPath / IcoPathAbsolute | `IconDescriptor(FilePath…)` |
| `Icon` デリゲート(WPF ImageSource) | Host内で評価しPNG一時ファイル or data URI化（初期はProviderIconへ降格でも可、R7で決定） |
| `Action` / `AsyncAction` | `ExecutionToken`（Hostがセッション辞書で保持・実行） |
| `PreviewPanel` (WPF UserControl) | **非対応（Tier 4）**。`PreviewInfo` のデータ部分のみDTO化 |
| `ContextData` | Hostから出さない（コンテキストメニューRPCで間接利用） |

## 5. Windows統合サービス（Beacon.Platform.Windows + Beacon.WinUI）

WPFの `WindowChrome` / `HwndSource` / `System.Windows.Media` は使わない。CsWin32ベースで再設計:

| サービス | 実装方針 | 検証 |
|---|---|---|
| グローバルホットキー | RegisterHotKey（メッセージ専用HWND）。Winキー単独はBeacon-old `GlobalHotkey.cs`(WH_KEYBOARD_LL・UI非依存確認済み)方式を移植 | R1 |
| トレイ | Shell_NotifyIcon 自前実装を第一候補（パッケージ追加はライセンス原文確認が条件） | R1 |
| 単一インスタンス | Mutex + Named Pipe（Beacon-old `SingleInstance.cs` の方式を非WPF化） | R1 |
| 起動時非表示 | ウィンドウ生成遅延 or 非アクティブ生成。AppWindowで検証 | R1 |
| フォーカス取得/喪失で閉じる | AppWindow.Activate + WM監視 | R1/R4 |
| 複数モニター/DPI | AppWindow + GetDpiForMonitor。カーソル/アクティブモニターへ配置 | R4 |
| Backdrop | Mica/Acrylic（SystemBackdrop）。非対応環境はソリッドフォールバック | R1/R4 |
| Shellアイコン/サムネイル | Beacon-old ImageLoader/ThumbnailReaderのロジックを移植、出力は `IconDescriptor` → WinUI側でImageSource化 | R3 |
| Everything | Beacon-old P/Invoke層を移植（UI依存なし確認済み） | R3 |
| 通知 | AppNotification（WinAppSDK）。Unpackaged時の制約をR1で確認 | R1 |
| スタートアップ登録 | Startupフォルダ .lnk（オプトイン、DISTRIBUTION.md §5） | R5 |

## 6. しないこと

- Beacon-oldへのProject Reference / Submodule接続（ADR-0001）
- WPF UserControl / 設定画面のWinUI埋め込み
- WinUI本体へのWPF/WinForms参照追加（プラグイン互換のためであっても禁止）
- iNKORE由来物の移植（コード・スタイル・素材すべて）
- 図に合わせるためだけの空プロジェクト先行生成
