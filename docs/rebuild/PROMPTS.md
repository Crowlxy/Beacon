# PROMPTS — Codex実装プロンプト

運用: ユーザーが本リポジトリ（`C:\Users\ha.takaku\Desktop\Project\Beacon`）を作業ディレクトリとしてCodexを**対話モードで**実行し、以下のプロンプト本文を渡す（モデル: gpt-5.6-sol）。ヘッドレスの `codex exec` はこの環境ではサンドボックス制限で失敗するため使わない（LESSONS-archive.md 2026-07-16）。
**本ファイルには現行フェーズのプロンプトのみ置く。** 次フェーズ以降は各フェーズ完了後、実コードと確定ADRを見てClaudeが作成する（先に作り切らない）。現行はR3（R2は2026-07-20レビュー済み・完了。完了プロンプトは次フェーズ完了時に削除する）。

---

## Phase R2: ContractsとCoreの境界確立プロンプト【完了 2026-07-20】

前提: Gate A承認済み（2026-07-17）。R1のスパイク用最小DTO（`src/Beacon.Contracts/SearchContracts.cs`・ContractVersion=1）を本設計へ置き換える。

```
あなたはBeacon（WinUI 3再構築・独立リポジトリ）のPhase R2（ContractsとCoreの境界確立）を担当する。
目的はプロセス境界を越える検索契約の確定と、UI非依存のBeacon.Core新設。UI実装はしない。

## リポジトリ配置
- 本リポジトリ（作業対象）: C:\Users\ha.takaku\Desktop\Project\Beacon
- Beacon-old（参照のみ・変更禁止）: C:\Users\ha.takaku\Desktop\Project\Beacon-old

## 読むべき文書（この順で）
1. docs/rebuild/LESSONS.md（必読。特にStreamJsonRpcのUseSingleObjectParameterDeserializationと複数プロセスログの教訓）
2. AGENTS.md
3. docs/rebuild/ARCHITECTURE.md §1・§4 / adr/ADR-0001（特に§4-5）/ adr/ADR-0003
4. docs/rebuild/AUDIT.md §A2 / COMPATIBILITY.md §3
5. docs/rebuild/PLAN.md の Phase R2

## 対象範囲

### 1. Beacon.Contracts 本設計（src/Beacon.Contracts。net9.0・依存ゼロを維持）
ARCHITECTURE.md §4 の概形を正として以下を確定実装する。R1の最小DTOは置き換えてよい
（互換不要。ContractVersion.Current は 2 へ上げる）:
- ContractVersion: const int Current = 2。受信側は不一致を拒否する（例外ではなく「無視+警告ログ可能」な戻り値設計）
- QueryScope enum: All / Applications / Files / Folders / Settings / Actions / Calculator / Url / WebSearch
  （SPEC §4の検索対象と一致させる。既定All。R8のカテゴリチップはこの列挙を使う前提）
- ResultKind enum: Unknown=0 / Application / File / Folder / Setting / Action / Calculation / Url / WebSearch / Plugin
- SearchRequest: sealed record (string SessionId, string RawQuery, QueryScope Scope, int ContractVersion)
- SearchResultDto: ARCHITECTURE §4 の形をそのまま（required Id/ProviderId/Title、
  Subtitle?/Kind/Score/Icon/ExecutionToken?/CopyText?/AutoCompleteText?/FilePath?）。
  ContractVersionは結果単位では持たない（リクエスト/ハンドシェイク単位）
- IconDescriptor: sealed record (IconSource Source, string? Value) / IconSource enum: ARCHITECTURE §4 のまま
- 実行要求: ExecuteRequest (string SessionId, string ResultId, string ExecutionToken, int ContractVersion) と
  ExecuteResponse (bool Success, string? FailureReason)
- Provider contract: ISearchProvider { string ProviderId { get; }
  IAsyncEnumerable<SearchResultDto> SearchAsync(SearchRequest request, CancellationToken cancellationToken); }
- 全DTOはSystem.Text.Jsonで往復可能（デリゲート・object・UI型・IntPtrを含めない）

### 2. Beacon.Core 新設（src/Beacon.Core。net9.0・Beacon.Contractsのみ参照）
今回入れるのはクエリ編成の骨格だけ（ランキングはR6、DataRootResolverの移設はR3）:
- QuerySession/QueryOrchestrator: 新しいクエリで新SessionIdを発行し、前セッションをCancellationTokenで
  キャンセルする。複数ISearchProviderの逐次結果を1本のストリームへ統合する
- 古い結果の破棄: 現在セッション以外のSessionIdの結果は配信しない
- 実行検証: ExecuteRequestが「現在セッションのResultId/ExecutionToken」であるときだけ実行対象として
  受理する（古い・不正な要求はExecuteResponse(Success=false, 理由)で拒否。SPEC §5「古い・不正な結果を実行しない」）
- ContractVersion不一致のSearchRequest/ExecuteRequestを受理しない

### 3. UI参照禁止のビルド+CI強制（ADR-0001 §5）
- Beacon.Contracts / Beacon.Core は純粋net9.0のまま（UseWPF/UseWindowsForms/FrameworkReferenceなし）
- CIへチェックを追加（既存 .github/workflows/portable-smoke.yml への step追加でよい）:
  src/Beacon.Contracts と src/Beacon.Core（obj/bin除外）に対して
  「using System.Windows」「Microsoft.UI.Xaml」「Windows.UI.Xaml」「UseWPF」「UseWindowsForms」
  「FrameworkReference」のgrepが0件であることを検証し、ヒットしたらfailさせる
- 同じチェックをローカルでも実行できるようスクリプト化する（Beacon.Distribution配下でよい）

### 4. 旧Result→DTO変換方針の確定（ドキュメント）
- ARCHITECTURE.md §4 の見出し「（概形。確定はPhase R2）」を確定版へ改め、実装した型と一致させる
- 写像表（旧Result→DTO）を実装済みの型名で確定する。AUDIT.md §A2の
  「データ部分だけをDTO写像対象にする」との整合を確認し、矛盾があれば実装せず報告する

### 5. R1スパイクの追随
- Beacon.PluginHost（ダミー）と Beacon.WinUI のRPC spikeを新契約へ追随させる
  （SessionId/Scope/ContractVersion=2を流す。UseSingleObjectParameterDeserialization=trueを維持）
- Build-Portable.ps1 → Test-Portable.ps1（既定と-UseActivationPipeの両方）が引き続き通ること

### 6. テスト（tests/Beacon.Core.Tests 新設。NUnit・using NUnit.Framework明示）
- 全DTOのSystem.Text.Json往復（値の完全一致。enum・null許容含む）
- 逐次配信: ダミーProvider2本の結果が到着順に統合されること
- キャンセル: 新クエリ開始で旧セッションのCancellationTokenがキャンセルされ、旧SessionIdの結果が配信されないこと
- 実行要求: 現在セッションのトークンは受理、古いセッション/未知のトークン/版数不一致は拒否されること
- 既存 tests/Beacon.R1.Tests は現状維持（DataRoot関連。移設はR3）

## 非対象範囲（やらない）
- UI実装・DesignTokens / ランキング・履歴（R6）/ Beacon-oldからのコード移植（R3以降）
- 実プラグインのロード・PluginHost本実装（R7）/ DataRootResolverのCore移設（R3）
- Beacon.Platform.Windowsの生成（R3）/ パッケージの追加

## 変更可能ファイル
src/Beacon.Contracts/**, src/Beacon.Core/**(新規), tests/Beacon.Core.Tests/**(新規),
src/Beacon.PluginHost/Program.cs, src/Beacon.WinUI/(RPC spike追随に必要な最小限),
Beacon.sln, .github/workflows/portable-smoke.yml, src/Beacon.Distribution/(禁止参照チェックスクリプト),
docs/rebuild/ARCHITECTURE.md(§4確定), docs/rebuild/LESSONS.md(失敗時)

## 禁止事項
- `Spotlight` という語（識別子・ログ・文字列・ファイル名すべて）
- Contracts/CoreへのWPF・WinUI・WinForms参照（本フェーズの主目的）
- DTOへのデリゲート・任意object・UI型・非シリアライズ型の追加
- Beacon-old側の変更 / 新規NuGet依存の追加

## ビルド／検証（この順で全部やる）
1. dotnet build Beacon.sln -c Release で0エラー
2. dotnet test -c Release で全テスト成功（R1の4件+新規Core.Tests）
3. 禁止参照チェックスクリプトがローカルで0件
4. Build-Portable.ps1 → Test-Portable.ps1（既定）→ Test-Portable.ps1 -UseActivationPipe が通ること
5. スモーク後にBeacon.Nextプロセスが残っていないこと（既知事象）

## 成果物・完了条件（PLAN.md Phase R2と同一）
- Contracts/CoreにWPF・WinUI参照がない（ビルド設定+CI grepの両方で検証可能）
- 逐次結果・キャンセル・実行要求（受理と拒否）がテストで再現できる
- Result→DTO写像表がARCHITECTURE §4で実装と一致した確定版になっている
- R1スモーク（両モード）が新契約でも通る

## 失敗時
docs/rebuild/LESSONS.md へ「事象・原因・再発防止」を記録してから再試行。同じ失敗を繰り返さない。

## Git差分要約
変更・追加ファイル一覧と概要を報告する。コミットはユーザーが行う。
```

---

## Phase R3: Windowsプラットフォームサービス抽出プロンプト

前提: R2完了（2026-07-20レビュー済み）。実装は `feature/rebuild-r3-platform` ブランチで行う（R2差分のコミット・PRが済んでいることをユーザーへ確認してから着手）。

```
あなたはBeacon（WinUI 3再構築・独立リポジトリ）のPhase R3（Windowsプラットフォームサービス抽出）を担当する。
目的はBeacon-oldからのUI非依存なWindows統合サービスの選択的移植と、WinUIを起動せずに
アプリ検索・ファイル検索が動く状態の実現。UI実装はしない。

## リポジトリ配置
- 本リポジトリ（作業対象）: C:\Users\ha.takaku\Desktop\Project\Beacon
- Beacon-old（参照のみ・変更禁止）: C:\Users\ha.takaku\Desktop\Project\Beacon-old

## 読むべき文書（この順で）
1. docs/rebuild/LESSONS.md（必読。環境系はLESSONS-archive.mdの一行要約も有効）
2. AGENTS.md
3. docs/rebuild/ARCHITECTURE.md §1・§5 / adr/ADR-0001 / adr/ADR-0004
4. docs/rebuild/AUDIT.md §A3・§A10・§A12（移植対象ファイルの確定リスト）
5. docs/rebuild/DEPENDENCY_MAP.md B1・B2・B4 / docs/rebuild/PLAN.md の Phase R3

## 対象範囲

### 1. Beacon.Platform.Windows 新設（src/Beacon.Platform.Windows）
- TFM: WinRT API（UWPパッケージ列挙等）が必要なら Beacon.WinUI と同じ net9.0-windows10.0.xxxx、
  不要なら net9.0-windows。UseWPF / UseWindowsForms / WinUI参照は一切入れない
- 参照は Beacon.Contracts（必要なら Beacon.Core も可。ARCHITECTURE §1の参照方向を守る）
- Microsoft.Windows.CsWin32 を採用（DEPENDENCY_MAP B部で採用予定済み）。追加前に
  DEPENDENCY_MAP.md B1へバージョン・MITライセンス原文確認・配布影響
  （ソースジェネレータでありランタイムDLLは配布物へ入らない）を記載する
- Beacon.sln へ追加。tests/Beacon.Platform.Windows.Tests も新設（NUnit・using NUnit.Framework明示）

### 2. DataRootResolverのCore移設（ADR-0004）
- src/Beacon.WinUI/DataRootResolver.cs の DataRootResolver / DataRootResolution /
  DataRootResolutionException を src/Beacon.Core へ移設（namespace Beacon.Core。挙動は変えない）
- R1Storage（スパイク用ログ・設定書き込み）はWinUI側へ残す。WinUIはCore参照で追随
- tests/Beacon.R1.Tests のDataRootResolverテストを tests/Beacon.Core.Tests へ移設し、R1.Tests側から削除

### 3. Everything移植（AUDIT §A12「再利用候補」の列挙ファイル）
- Plugins/Flow.Launcher.Plugin.Explorer/Search/Everything/ の EverythingApiDllImport.cs・
  EverythingAPI.cs・EverythingSearchOption.cs・EverythingSortOption.cs・Exceptions/* を
  Beacon.Platform.Windows へ移植。EverythingSearchManager.cs は結果を SearchResultDto
  （Kind=File/Folder、Icon=IconDescriptor）で返すようアダプト。キャンセル契約（CancellationToken）を維持
- 各移植ファイル先頭に由来コメント（Beacon-old内パス・Flow Launcher/Wox由来）を付け、
  MIT著作権表記を維持。attribution.md を更新
- EverythingSDK x64 の Everything.dll を配布へ同梱（Build-Portable.ps1でZIPへ含める。x86は同梱しない）
- Everything未導入・サービス未起動時は例外で落とさず「空結果+理由をログ」で退避する
- EverythingDownloadHelper.cs / Droplex は移植しない（廃止判定済み）

### 4. アプリ検索（Programのアダプト）
- Plugins/Flow.Launcher.Plugin.Program/Programs/ の Win32.cs・UWPPackage.cs・IProgram.cs・
  ShellLinkReader.cs・ShellLinkReadResult.cs・ShellLocalization.cs から、WPF Input/BitmapImage・
  WinForms依存を除去して列挙・ShellLink処理を移植
- ISearchProvider実装（例: AppSearchProvider。ResultKind.Application、Icon=IconDescriptor(FileShellIcon,...)）
  として Beacon.Core の QueryOrchestrator に接続できる形にする

### 5. ファイル検索プロバイダー
- Everything経由の FileSearchProvider（ISearchProvider実装）
- Everything不可時のfallbackとしてWindows Index検索を移植する場合、System.Data.OleDb等の
  新規依存が必要になる。追加前にDEPENDENCY_MAP.md B1へ必要性・ライセンス原文・バージョン・
  配布影響を記載すること。原文確認ができない依存が必要なら実装せず報告する
  （fallback未実装でもR3完了条件は満たせる。その場合は未実装と理由を報告に明記）

### 6. Shellアイコン・サムネイル
- Flow.Launcher.Infrastructure/Image/ImageLoader.cs・ThumbnailReader.cs から
  Shell取得ロジックだけをアダプト。出力は IconDescriptor（FileShellIcon / FileThumbnail）または
  ファイルパス・data URI。System.Windows.Media.ImageSource を返すコードを持ち込まない
- System.Drawing.Common は採用しない（B2で不採用済み。Shell API / CsWin32経路で実装）

### 7. Win32統合サービス（AUDIT §A10の分割アダプト）
- Flow.Launcher.Infrastructure/Win32Helper.cs から HWND/CsWin32ベース部分だけをサービス別に移植:
  プロセス起動 / ファイル操作（開く・フォルダで表示） / Active Window判定 / Explorerの現在パス取得 /
  クリップボード（テキストget/set）
- WPF Window / Visual / HwndSource を引数・戻り値に持つAPIは移植しない（ファイル全体コピー禁止）
- DialogJump系は保留判定のまま移植しない

### 8. コンソールハーネス
- WinUIを起動せずアプリ検索・ファイル検索を実行できる最小のConsoleプロジェクト
  （例: src/Beacon.Platform.Windows.DevHarness。Beacon.slnへ追加、配布ZIPへは含めない）
- 標準入力のクエリで QueryOrchestrator + 各Providerの逐次結果を表示できること

### 9. 検証の拡張
- Test-NoUiReferences.ps1 の検査対象へ src/Beacon.Platform.Windows を追加
  （パターンは既存と同じ。net9.0-windows TFM自体は違反ではない。誤検知が出たらパターン側を報告）
- 環境依存テスト（Everything実機・Windows Index）は自動実行から分離（[Explicit]または
  環境検出スキップ）し、CIを不安定にしない。ロジック部分はfake/一時ディレクトリでテストする

## 非対象範囲（やらない）
- UI実装・DesignTokens（R4）/ ランキング・履歴・使用統計（R6）
- Calculator・URL・WebSearch・Bookmarkプロバイダー（R6）
- PluginHost本実装・実プラグインロード（R7）/ DialogJump（保留）
- トレイ・ホットキー・単一インスタンスの再実装（R1スパイクを維持）
- Everything/ランタイムの自動ダウンロード（Droplex系は廃止）

## 変更可能ファイル
src/Beacon.Platform.Windows/**(新規), src/Beacon.Platform.Windows.DevHarness/**(新規),
tests/Beacon.Platform.Windows.Tests/**(新規), src/Beacon.Core/**(DataRootResolver受け入れ),
tests/Beacon.Core.Tests/**, tests/Beacon.R1.Tests/**(DataRootテスト削除のみ),
src/Beacon.WinUI/(DataRootResolver移設への追随に必要な最小限), Beacon.sln,
src/Beacon.Distribution/(Everything.dll同梱とテスト対象追加), .github/workflows/portable-smoke.yml,
docs/rebuild/DEPENDENCY_MAP.md(B1追記), attribution.md, docs/rebuild/LESSONS.md(失敗時)

## 禁止事項
- `Spotlight` という語（識別子・ログ・文字列・ファイル名すべて）
- Contracts / Core / Platform.Windows へのWPF・WinUI・WinForms参照
- System.Windows.Media.ImageSource 等のUI型を返すAPIの移植
- DEPENDENCY_MAP.md B1へ記載する前の新規NuGet追加 / ライセンス原文未確認の依存追加
- Beacon-old側の変更 / iNKORE由来物・SegoeFluentIcons.ttf の移植

## ビルド／検証（この順で全部やる）
1. dotnet build Beacon.sln -c Release で0警告・0エラー
2. dotnet test Beacon.sln -c Release で全テスト成功（環境依存テストは自動スキップでよい）
3. Test-NoUiReferences.ps1（Platform.Windows追加後）がローカルで0件
4. Build-Portable.ps1 → Test-Portable.ps1（既定）→ Test-Portable.ps1 -UseActivationPipe が通ること
5. ZIP内に Everything.dll(x64) が含まれ、iNKORE・System.Drawing.Common・WPF系DLLが含まれないこと
6. DevHarnessでアプリ検索・ファイル検索を手動確認（Everythingあり・なしの両方。なし側は空結果+理由ログ）
7. スモーク後にBeacon.Nextプロセスが残っていないこと

## 成果物・完了条件（PLAN.md Phase R3と同一）
- コンソールハーネスまたはCoreテストでアプリ・ファイル検索が動く
- Everythingあり・なし両方で動く（なし側はクラッシュせず退避）
- WPFを起動せず結果を取得できる（IconDescriptor出力・ImageSource不使用）
- 移植ファイル全件に由来コメントとMIT表記があり、attribution.mdが更新されている

## 失敗時
docs/rebuild/LESSONS.md へ「事象・原因・再発防止」を記録してから再試行。同じ失敗を繰り返さない。

## Git差分要約
変更・追加ファイル一覧と概要を報告する。コミットはユーザーが行う。
```
