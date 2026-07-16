# ADR-0001: 再構築境界 — 独立リポジトリ・独立slnとし、Beacon-oldへは接続せず選択的移植のみ行う

- 状態: **承認済み（2026-07-16 ユーザー承認。「既存slnへ追加」案は不採用）**
- 日付: 2026-07-16

## 決定

1. 新Beaconは **`Crowlxy/Beacon` リポジトリの独立プロジェクト**。旧WPF版（Flow Launcherフォーク）は `Crowlxy/Beacon-old` に分離済みで、**移植元・比較参照のみ**に使う。
2. 本リポジトリに新規 `Beacon.sln` を作り、`src/` と `tests/` を持つ独立構成とする（構成図は ARCHITECTURE.md §1。プロジェクト生成はPhase R1以降）。
3. **Beacon-oldへ Project Reference / Git Submodule / パッケージ参照で接続しない。** 必要なロジックはファイル単位で選択的に移植し、その際:
   - 由来（Beacon-old内のパス / Flow Launcher・Wox由来であること）をコミットメッセージまたは移植記録に残す
   - MIT等の著作権・ライセンス表記を維持する（`attribution.md` / `LICENSE` 運用）
   - 移植前に AUDIT.md の分類（再利用/アダプト/書き直し/廃止）で判定されていること
4. `Beacon.Contracts` と `Beacon.Core` では以下への参照を禁止する: `System.Windows.*` / `Microsoft.UI.Xaml.*` / `Windows.UI.Xaml.*` / WPF `UserControl` / `ImageSource` / `BitmapSource` / WinUI `UIElement` / `BitmapImage`。CoreがUIへ返すのは**データと識別子のみ**。
5. 禁止はビルドで強制する: 両プロジェクトは `UseWPF` / `UseWindowsForms` を持たない純粋 `net9.0`（Platform.Windowsのみ `net9.0-windows`）。加えてCIで `FrameworkReference` と `using System.Windows` のgrepチェックを行う。
6. 旧 `Flow.Launcher.Plugin.dll`（WPF汚染済み公開API）はソース移植せず、**PluginHostプロセス内でのみバイナリ/ソース互換として扱う**（ADR-0003）。互換のための公開API再現時も破壊的変更をしない。

## 理由

- Result / IPublicAPI / ISettingProvider へのWPF型漏れをBeacon-old実コードで確認済み（AUDIT.md §2）。WinUIプロセスへの直接ロードは不可能。
- リポジトリ分離により、旧WPF版の並行運用（比較・互換確認）と新プロジェクトの依存衛生（iNKORE等の混入防止・履歴の分離）を両立する。
- Submodule / Project Reference接続は「iNKORE含む旧依存グラフの引き込み」「上流フォーク履歴の汚染」を招くため禁止。

## 帰結

- 移植は片方向（Beacon-old → Beacon）のコピーとなり、二重実装期間が生じる。Beacon-old側は凍結（バグ修正のみ）で管理する。
- 旧計画Phase 1の未コミット作業はBeacon-old側にのみ保存し、**新Beaconへ持ち込まない**（ユーザー決定 2026-07-16）。
- 上流Flow Launcherの取り込みはBeacon-old側 `dev` ブランチの役割とし、新Beaconへは「Beacon-old経由の選択的移植」としてのみ届く。
