# PROMPTS — Codex実装プロンプト

運用: ユーザーが本リポジトリ（`C:\Users\ha.takaku\Desktop\Project\Beacon`）を作業ディレクトリとしてCodexを**対話モードで**実行し、以下のプロンプト本文を渡す（モデル: gpt-5.6-sol）。ヘッドレスの `codex exec` はこの環境ではサンドボックス制限で失敗するため使わない（LESSONS-archive.md 2026-07-16）。
**本ファイルにはPhase R0とR1のみ存在する。** R2以降はGate A通過後、実コードと確定ADRを見てClaudeが作成する（先に作り切らない）。

---

## Phase R0: 現状監査プロンプト

```
あなたはBeacon（WinUI 3再構築・独立リポジトリ）のPhase R0（現状監査）を担当する。
コードの実装・修正は一切しない。調査とドキュメント更新のみを行う。

## リポジトリ配置（重要）
- 本リポジトリ（Crowlxy/Beacon・作業対象）: C:\Users\ha.takaku\Desktop\Project\Beacon
- 調査対象のBeacon-old（Crowlxy/Beacon-old・読み取り専用）: C:\Users\ha.takaku\Desktop\Project\Beacon-old
  ※**Beacon-oldフォルダ内のファイルを一切変更しない**

## 読むべき文書（この順で）
1. docs/rebuild/LESSONS.md（失敗記録簿。作業前必読。Beacon-old側 docs/spotlight/LESSONS.md の環境系教訓も読む）
2. AGENTS.md
3. docs/rebuild/SPEC.md / ARCHITECTURE.md / PLAN.md の Phase R0
4. docs/rebuild/AUDIT.md / DEPENDENCY_MAP.md / COMPATIBILITY.md（今回更新する対象。「未確認」欄を埋める）

## 対象範囲
- AUDIT.md §A8 の未確認事項をすべてBeacon-oldの実コードで確認して埋める:
  - 標準12プラグイン（Beacon-old Plugins/*）それぞれのWPF依存箇所（using System.Windows*, UserControl, ImageSource, ISettingProvider実装, PreviewPanel使用）を列挙し、COMPATIBILITY.md §2 の分類（A〜E）を根拠ファイル付きで確定する
  - Flow.Launcher.Infrastructure/Win32Helper.cs と DialogJump/ のWPF結合度（WPF Window/HwndSource前提のAPIを列挙）
  - ChefKeys 0.1.2 のUIフレームワーク依存とライセンス（NuGet原文を確認。推測禁止）
  - Droplex / FSharp.Core / InputSimulator / System.Drawing.Common の実際の参照元と用途
  - 履歴・TopMostRecord・UserSelectedRecordの保存形式（ファイルパス・シリアライザ）とUI依存
  - Everything.dll のアーキテクチャ（x64のみか。EverythingSDKフォルダの実物を確認）
  - Flow.Launcher.Localization の生成物とWinUIでの利用可否（確認できる範囲で。不能なら「未確認・要R1実験」と書く）
- WPF型が漏れている公開API（Beacon-old Flow.Launcher.Plugin全体）をgrepで全抽出し、AUDIT.md §A2を完成させる
- 再利用/アダプト/書き直し/廃止のファイルマップを AUDIT.md へ追加（ARCHITECTURE.md §1の行き先プロジェクト単位。移植候補ファイルのBeacon-old内パスを明記）
- Beacon-old Release配布物のDLL一覧: Beacon-old側で dotnet build -c Release し Output/Release を列挙してAUDIT.mdへ追記（ビルド生成物はBeacon-old側に置いたまま。コミットしない）
- 上流Flow Launcherの選択的移植手順を docs/rebuild/UPSTREAM.md として新規作成（Beacon-old dev経由・ファイル単位移植・由来とMIT表記の記録手順）
- 現行UIのスクリーンショットを docs/rebuild/baseline/ へ保存（Alt+Space表示・検索展開・設定画面。取得できない場合は手順だけ書いてユーザーへ依頼）

## 非対象範囲（やらない）
- 一切のコード実装・新プロジェクト作成（Beacon.sln生成はR1）
- Beacon-old側のあらゆるファイル変更（ビルドの一時生成物を除く）
- パッケージの追加

## 変更可能ファイル（すべて本リポジトリ側）
docs/rebuild/AUDIT.md, DEPENDENCY_MAP.md, COMPATIBILITY.md, UPSTREAM.md(新規), baseline/(新規), LESSONS.md（失敗時）

## 禁止事項
- 推測で「未確認」欄を埋めること。確認できなければ「未確認」のまま理由を書く
- `Spotlight` という語を成果物のコード欄・ファイル名に書くこと（散文は可）
- Beacon-oldの変更・コミット

## ビルド／テスト
- Beacon-old側: dotnet build Flow.Launcher.sln のベースライン記録（所要時間・警告数）。実行前に Output/Debug を使用中のBeaconプロセスがないか確認（既知事象）
- 本リポジトリ側: ビルド対象なし（ドキュメントのみ）

## 成果物・完了条件
- AUDIT.md A部に「未確認」が残っていない、または残った項目に理由が明記されている
- COMPATIBILITY.md §2 の全12行が根拠ファイル付きで確定している
- UPSTREAM.md が存在し移植手順が実行可能
- Beacon-oldに差分がない（git status で確認）

## 失敗時
docs/rebuild/LESSONS.md へ「事象・原因・再発防止」を記録してから再試行する。

## ライセンス確認
新たに判明した依存のライセンスは原文を確認し、DEPENDENCY_MAP.mdへ記録する。

## Git差分要約
最後に変更ファイル一覧と各ファイルの変更概要を報告する。コミットはユーザーが行う。
```

---

## Phase R1修正: Gate A差し戻し B1〜B4（2026-07-17）

前提: R1本体は実装済み。開発機スモーク・テスト4件・iNKORE/TTF不在・禁止語ゼロはGate Aレビューで確認済み。差し戻しは下記4点+報告書。Gate A承認はこれらの解消後。

```
あなたはBeacon（WinUI 3再構築・独立リポジトリ）のPhase R1 Gate A差し戻し対応を担当する。
対応はB1〜B4と報告書作成のみ。スコープを広げない。

## リポジトリ配置
- 本リポジトリ（作業対象）: C:\Users\ha.takaku\Desktop\Project\Beacon
- Beacon-old（参照のみ・変更禁止）: C:\Users\ha.takaku\Desktop\Project\Beacon-old

## 読むべき文書
1. docs/rebuild/LESSONS.md（必読）
2. AGENTS.md
3. docs/rebuild/adr/ADR-0002-portable-distribution.md / DEPENDENCY_MAP.md B部

## B1: 配布ZIPからWPFランタイムパックを除去する
事実（レビューで確認済み）:
- artifacts/Beacon-Portable-x64.zip の Beacon.Next.deps.json に
  runtimepack.Microsoft.WindowsDesktop.App.Runtime.win-x64/9.0.17 が含まれ、
  PresentationFramework*/PresentationCore/WindowsBase/System.Windows.*/wpfgfx_cor3 等
  約60ファイルがZIPに同梱されている。
- src/Beacon.WinUI/obj/project.assets.json の frameworkReferences には
  WindowsDesktop が無い（Microsoft.NETCore.App と Windows SDK projection のみ）。
  つまり restore ではなく build/publish 時のターゲットが追加している。
手順:
1. dotnet publish src/Beacon.WinUI/Beacon.WinUI.csproj -c Release -r win-x64 -bl で
   binlog を取得し、FrameworkReference / RuntimePack に
   Microsoft.WindowsDesktop.App を追加しているターゲットを特定する（推測で直さない）
2. 特定した原因に対する最小の除去策を Beacon.WinUI.csproj へ適用する
   （候補: <FrameworkReference Remove="Microsoft.WindowsDesktop.App" /> 等。
   binlogの事実に合わせる）
3. ZIP再生成後、Presentation*/WindowsBase/System.Windows.*/wpfgfx/UIAutomation*/
   ReachFramework/System.Printing が1つも含まれないことを portable-dlls.txt で確認し、
   除去前後のZIPサイズを記録する
4. Microsoft公式一次情報で除去不能と判明した場合のみ、根拠URLと理由を
   ADR-0002へ記録して同梱を許容する（その判断はR1_REPORTにも書く）
※binlog（*.binlog）はコミットしない。

## B2: ADR-0002「R1で記録する欄」を実測で埋める
docs/rebuild/adr/ADR-0002-portable-distribution.md の表5項目:
- Windows App SDK バージョン（採用中: 2.2.0。Microsoft公式で安定版であることを確認）
- 最小Windowsバージョン（csprojのTargetPlatformMinVersionと公式要件を突き合わせる）
- Self-contained配布サイズ実測（B1適用後のZIPサイズ）
- ライセンス（NuGetパッケージ内のライセンス原文を実際に開いて確認。推測禁止。
  WinAppSDKはMITではない可能性が高い—原文の名称と再頒布可否を記録）
- Unpackaged既知問題（ホットキー/トレイ/Backdrop。公式ドキュメント・GitHub issueで確認）

## B3: 採用依存の文書化
- docs/rebuild/DEPENDENCY_MAP.md B部へ、実際に採用した版数で記録:
  Microsoft.WindowsAppSDK 2.2.0 / StreamJsonRpc 2.25.29（各ライセンス原文確認）
- 配布ZIPに入る推移的依存も一覧化する（deps.json由来: MessagePack,
  MessagePack.Annotations, Nerdbank.MessagePack, Nerdbank.Streams, Newtonsoft.Json,
  PolyType, Microsoft.VisualStudio.Threading, Microsoft.VisualStudio.Validation,
  System.IO.Pipelines 等。ライセンスを原文確認して表へ）
- attribution.md へ表示義務のあるものを追記

## B4: クリーン環境スモークのGitHub Actionsワークフロー
新規: .github/workflows/portable-smoke.yml
- トリガー: workflow_dispatch と pull_request（mainターゲット）
- runs-on: windows-latest（WinAppSDK Runtime未導入のクリーン環境として使う）
- 手順: actions/setup-dotnet で .NET 9 → Build-Portable.ps1 → Test-Portable.ps1 →
  失敗時も beacon.log と portable-dlls.txt を actions/upload-artifact で回収（if: always()）
- CIでは keybd_event によるホットキー送出が届かない可能性がある。
  Test-Portable.ps1 に -UseActivationPipe スイッチを追加:
  ホットキー送出の代わりに Beacon.Next.exe をもう1本起動し、
  単一インスタンスのアクティベーションパイプ経由で表示させる
  （2本目は終了コード0で即終了すること自体も検証になる）。
  ローカル実行の既定動作（キー送出）は変えない。CIではこのスイッチを使う
- ついでに同スクリプトの既知不備を直す: 15秒deadlineが「登録待ち」と
  「表示/RPC待ち」で共有されており後段の時間が枯渇する。段階ごとに別deadlineにする
※ワークフローの実行確認はpush後にユーザーが行う。Actionsの成功判定に必要な
  チェックポイント（ログマーカー一覧）を報告に含めること。

## 報告書: docs/rebuild/R1_REPORT.md（新規・R1プロンプトの未提出成果物）
PLAN.md R1の各検証項目の成否を表で記録する（失敗・未実施も隠さず書く）。
今回のB1〜B4の結果、ZIPのDLL一覧参照、サイズ、既知の制約
（クリーン環境検証はwindows-latest=Server系イメージで実施、の注記）を含める。

## 非対象範囲（やらない）
- 上記以外のコード変更・リファクタリング / パッケージの追加・更新（除去は可）
- UI実装

## 禁止事項
- `Spotlight` という語（識別子・ログ・文字列・ワークフロー名すべて）
- Beacon-old側の変更
- ライセンス欄を推測で埋めること

## ビルド／検証（この順で全部やる）
1. dotnet build Beacon.sln -c Release で0エラー
2. Build-Portable.ps1 でZIP再生成 → portable-dlls.txt にWPF系が無いこと（B1）
3. Test-Portable.ps1（既定モード）が最後まで通ること
4. Test-Portable.ps1 -UseActivationPipe も最後まで通ること（CI経路のローカル検証）
5. dotnet test -c Release で4件成功
6. スモーク後にBeacon.Nextプロセスが残っていないこと（既知事象）

## 失敗時
docs/rebuild/LESSONS.md へ「事象・原因・再発防止」を記録してから再試行。

## Git差分要約
変更ファイル一覧と概要を報告する。コミットはユーザーが行う。
```

---

## Phase R1修正: スモーク残課題2点（2026-07-17）

前提: R1本体は実装済み。XBF同梱ZIPの再スモーク（2026-07-17）で、起動〜ホットキー登録〜RPC spike開始まで全チェックポイント通過を確認済み。残る失敗は下記2点のみ（LESSONS.md 2026-07-17 参照）。

```
あなたはBeacon（WinUI 3再構築・独立リポジトリ）のPhase R1の残課題修正を担当する。
修正は2点のみ。スコープを広げない。

## リポジトリ配置
- 本リポジトリ（作業対象）: C:\Users\ha.takaku\Desktop\Project\Beacon
- Beacon-old（参照のみ・変更禁止）: C:\Users\ha.takaku\Desktop\Project\Beacon-old

## 読むべき文書
1. docs/rebuild/LESSONS.md（特に 2026-07-17 のエントリ。必読）
2. AGENTS.md

## 修正1: Test-Portable.ps1 のホットキー送信タイミング（競合の解消）
ファイル: src/Beacon.Distribution/Test-Portable.ps1
現状: ログファイルの出現を待った直後に Invoke-BeaconHotkey を呼んでいるが、
実際の RegisterHotKey 完了（ログ行 "Hotkey and tray registered"）は約150ms後で、
送信したキーが登録前に失われる。
修正: ログ「ファイルの存在」ではなくログ「内容」に
'Hotkey and tray registered' が現れるまで待ってから Invoke-BeaconHotkey を呼ぶ。
待機はdeadline（15秒）内でポーリング。deadline超過時は
'Hotkey registration was not observed.' でthrowする。
ログ読み取りは FileShare 競合を避けるため既存と同じ Get-Content -Raw を使う。

## 修正2: RPC通知のパラメータバインド不一致
ファイル: src/Beacon.WinUI/MainWindow.xaml.cs（SearchResultReceiver）
現状: サーバー（Beacon.PluginHost）は NotifyWithParameterObjectAsync("searchResult", result) で
SearchResultDto を「名前付きパラメータオブジェクト」として送るが、
受信側 OnSearchResult(SearchResultDto result) の [JsonRpcMethod("searchResult")] に
UseSingleObjectParameterDeserialization = true が無く、バインド失敗で通知が黙って破棄される。
修正: 属性を [JsonRpcMethod("searchResult", UseSingleObjectParameterDeserialization = true)] へ変更。
これで解決しない場合のみ、StreamJsonRpc 2.25.29 のドキュメント/実装を確認して原因を特定し、
サーバー側を含む最小の修正を行う（推測で別APIへ書き換えない）。

## 非対象範囲（やらない）
- 上記2ファイル以外のコード変更（ビルドに必要な場合を除く）
- パッケージの追加・更新 / リファクタリング

## 禁止事項
- `Spotlight` という語（識別子・ログ・文字列すべて）
- Beacon-old側の変更

## ビルド／検証（この順で全部やる）
1. dotnet build Beacon.sln -c Release で0エラー
2. src/Beacon.Distribution/Build-Portable.ps1 でZIP再生成
3. src/Beacon.Distribution/Test-Portable.ps1 が最後まで通り
   'Portable launch, hotkey, RPC cancellation, and folder-move restart passed.' が出ること
4. artifacts/smoke-b/Beacon/Data/Logs/beacon.log に
   'Hotkey or activation pipe displayed the AppWindow' と
   'RPC incremental result' と 'RPC cancellation confirmed' があり、ERRORが無いこと
※検証中はビルド出力を使用中のBeacon.Nextプロセスが残っていないか先に確認する（既知事象）

## 失敗時
docs/rebuild/LESSONS.md へ「事象・原因・再発防止」を記録してから再試行。同じ失敗を繰り返さない。

## Git差分要約
変更ファイル一覧と概要を報告する。コミットはユーザーが行う。
```

---

## Phase R1: WinUI 3 / Portable技術スパイクプロンプト

```
あなたはBeacon（WinUI 3再構築・独立リポジトリ）のPhase R1（技術スパイク）を担当する。
目的は見た目ではなく、最も危険な技術要素の成立証明。使い捨てではなく後続Phaseの土台になる最小実装を書く。

## リポジトリ配置
- 本リポジトリ（作業対象）: C:\Users\ha.takaku\Desktop\Project\Beacon
- Beacon-old（参照のみ・変更禁止）: C:\Users\ha.takaku\Desktop\Project\Beacon-old

## 読むべき文書（この順で）
1. docs/rebuild/LESSONS.md（必読）
2. AGENTS.md
3. docs/rebuild/PLAN.md の Phase R1 / ARCHITECTURE.md / adr/ADR-0001〜0004 / DISTRIBUTION.md
4. docs/rebuild/AUDIT.md（R0完成版）

## 対象範囲
1. 新規 Beacon.sln を作成し、最小プロジェクトを src/ と tests/ に生成:
   - src/Beacon.WinUI（WinUI 3, Unpackaged, AssemblyName=Beacon.Next）
   - src/Beacon.PluginHost（コンソールexe。ダミー: SearchRequestを受けて固定のSearchResultDtoを逐次返す）
   - src/Beacon.Contracts（スパイク用最小DTO: SearchRequest/SearchResultDto/ContractVersionのみ。本設計はR2）
   - src/Beacon.Distribution（publish + ZIP生成スクリプト初版。DISTRIBUTION.md §1の構造）
   ※図に合わせるためだけのCore/Platform.Windows等の空プロジェクトは作らない
2. Windows App SDK: Microsoft公式の安定版を選定し、バージョン・最小Windows・ライセンス原文確認結果・既知問題を docs/rebuild/adr/ADR-0002-portable-distribution.md の表へ追記する
3. 検証項目（それぞれ結果を docs/rebuild/R1_REPORT.md へ記録）:
   - Unpackaged + Self-contained publish（win-x64）
   - ZIP展開→Beacon.Next.exe直接起動（可能ならクリーンVM/サンドボックスで。不能なら手順を書きユーザーへ依頼）
   - 非表示起動 → グローバルホットキー（開発用Alt+Shift+Space、RegisterHotKey方式）でAppWindow表示
   - トレイアイコン表示・コンテキストメニューから表示/終了（CsWin32自前実装を第一候補。パッケージを足す場合は寛容ライセンス原文確認+DEPENDENCY_MAP.md B1更新が条件）
   - 単一インスタンス（Mutex+NamedPipe。識別子は .Next サフィックスで旧版と衝突させない）
   - Beacon.PluginHost.exe を同一フォルダから起動し、JSON-RPC over Named Pipe（第一候補StreamJsonRpc・MIT原文確認）で検索要求→逐次結果→キャンセルの往復
   - exe隣接 Data\ への設定・ログ読み書き（ADR-0004の解決規則: portable.flag判定 / flag消失+Data残存の提示 / 読み取り専用での明確な失敗）
   - フォルダ移動後の再起動
   - ダミー更新: 新ZIP展開差し替え→ロールバックの手動手順確認
   - 旧WPF版Beacon（Beacon-oldビルド）との並行起動
4. 将来MSIX化を阻害しないかの確認（メモをR1_REPORTへ）

## 非対象範囲（やらない）
- 検索UI・デザイン実装（ピルバー等はR4）
- 実プラグインのロード（ダミーのみ）
- Beacon-oldからのコード移植（R2以降。今回は新規記述のみ）
- 自動更新の本実装

## 変更可能ファイル
Beacon.sln(新規), src/(新規), tests/(新規), docs/rebuild/R1_REPORT.md(新規),
adr/ADR-0002(表の追記), DEPENDENCY_MAP.md(B1/B3の更新), attribution.md / LICENSE(依存追加時), LESSONS.md(失敗時)

## 禁止事項
- `Spotlight` という語（識別子・XAML・ログ・リソースキー・UI文字列すべて）
- WPF/WinForms参照を Beacon.WinUI / Beacon.Contracts へ追加すること
- iNKORE由来のコード・スタイル・素材のコピー
- ライセンス未確認のパッケージ・素材の追加
- Beacon-old側の変更

## ビルド／テスト
- dotnet build Beacon.sln 0エラー / DataRoot解決規則の単体テスト（tests/Beacon.Core.Tests相当はまだ無いのでContracts隣接でよい）
- publish→ZIP→展開→起動のスモーク手順をスクリプト化（Beacon.Distribution）
- 起動確認は「プロセス残存 + <BeaconRoot>\Data\Logs にERRORなし + ホットキーで表示」

## 成果物・完了条件（PLAN.md Phase R1と同一）
- クリーン環境でZIP展開後にBeacon.Next.exeが起動（Runtime事前導入なし）
- PluginHostダミーとのRPC往復・キャンセルが動く
- ホットキー表示・トレイ表示/終了が動く
- Data\への保存・フォルダ移動後起動・読み取り専用での明確な失敗
- 旧WPF版と並行起動できる
- 成果物ZIPにiNKOREが含まれない（DLL一覧をR1_REPORTへ添付）
- 各検証項目の成否がR1_REPORT.mdに記録されている（失敗も隠さず記録。Gate Aの判定材料）

## 失敗時
docs/rebuild/LESSONS.md へ記録してから再試行。同じ失敗を繰り返さない。

## ライセンス確認
追加した全パッケージ（WinAppSDK含む）の原文確認結果を DEPENDENCY_MAP.md / attribution.md /（表示義務があれば）LICENSE へ反映する。

## Git差分要約
変更・追加ファイル一覧と概要を報告する。コミットはユーザーが行う。
```
