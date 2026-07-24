# PROMPTS — Codex実装プロンプト

運用: ユーザーが本リポジトリ（`C:\Users\ha.takaku\Desktop\Project\Beacon`）を作業ディレクトリとしてCodexを**対話モードで**実行し、以下のプロンプト本文を渡す（モデル: gpt-5.6-sol）。ヘッドレスの `codex exec` はこの環境ではサンドボックス制限で失敗するため使わない（LESSONS-archive.md 2026-07-16）。
**本ファイルには現行フェーズのプロンプトのみ置く。** R3は2026-07-20完了。R4はR4.3でGate B承認（2026-07-21）。統合R5（旧R5・R6・R8）は2026-07-22にGate C承認。R6（レンダリング刷新）は2026-07-22承認・完了。R7（設定・データ移行＝設定画面UI + Legacy importer）は `feature/rebuild-r7` で実装済み（SettingsWindow.xaml(.cs) / LegacyMigration.cs）。ただし2026-07-23のユーザー目視レビューで **(a) 検索の一致品質・並び順・ハイライトが旧WPF版（Flow Launcher）に対し明確に劣る（統一マッチャ不在。①アプリのみ簡易ファジー②他は`String.Contains`③RankingEngineはタイトルexact/prefix/containsのみで①とスケール不一致）、(b) R7の設定画面UIが仮組み（テキスト手入力の羅列）で製品水準に達しない** の2点が確定した。これを是正するのが本フェーズ。**現行フェーズ = Phase R8（検索エンジン統一 + 設定画面リデザイン）**。R8はR7のLegacy importer（移行ロジック）を退行させず、R7の設定画面UIスタブを製品水準へ置き換える。

**2026-07-23追記: R8実装後のユーザー確認で (a) 設定画面が開かない（Data\Logs に `XamlParseException: Cannot find a Resource with the Name/Key TabViewButtonBackground`。App.xaml に XamlControlsResources 未マージのため NavigationView が生成不能）、(b) Stage 2-2 の設定行スタイル（アイコン+ラベル+説明+右側コントロールの行カード）が未実装で素のコントロール羅列のまま、が確定。R8完了条件のうち「トレイ『設定』で新設定画面を開く」「Data\Logs に ERROR が無い」は未達。是正は下記 Phase R8.1。**

**2026-07-23追記2: Phase R9（UX完成度）をユーザー承認。** 仕様・受入条件・コード裏取り済みの現状問題は [R9_UX.md](R9_UX.md) が正（あわせて SPEC §4改訂・§7.8追補、PLAN.md R8/R9追記済み）。**R8.1のユーザー目視確認が済み次第、現行フェーズ = Phase R9**（下記プロンプト）。


## Phase R8: 検索エンジン統一 + 設定画面リデザイン

前提: R7が **mainにマージ済み**（または `feature/rebuild-r7` の内容がツリーに存在）であることをユーザーへ確認してから着手する。実装ブランチは `feature/rebuild-r8-search-settings`。本フェーズは Gate C/R6/R7 で確定した機能・UI・デザインへの**改善**であり、状態遷移・IME・アクリル/枠/角丸・レンダリング・Legacy移行・クリップボード監視・実行契約（ContractVersion/ExecutionToken）を退行させない。新規NuGet依存の追加は原則禁止（必要なら DEPENDENCY_MAP.md B1 手続きを踏み、追加前に報告して指示を仰ぐ）。**Stage 1（検索エンジン統一）を先に完了・報告してから Stage 2（設定画面リデザイン）へ進む。**

```
あなたはBeacon（WinUI 3 Portable-firstランチャー・独立リポジトリ）のPhase R8を担当する。
目的は2つ。
(1) 現在バラバラな検索の一致判定（①アプリだけ手書き簡易ファジー / ②設定・ブックマーク・
    プロセス等は String.Contains / ③RankingEngine はタイトルの exact/prefix/contains のみで
    ①とスコアスケールが噛み合っていない）を、Beacon.Core の「1個のファジーマッチャ」に統一し、
    さらに一致文字を結果一覧で太字ハイライトする。これは旧WPF版（Flow Launcher）が
    StringMatcher.FuzzyMatch 1個で全プラグインを支えていたのと同じ思想へ寄せる作業。
(2) R7で仮組みのまま残っている設定画面UI（テキスト手入力の羅列）を、キーボード操作可能で
    ランチャーと一貫したトーンの製品水準の設定画面へ作り直す。
必ず Stage 1（検索統一）→ Stage 2（設定画面）の順で実装し、各Stage完了時に
ビルド・テストをグリーンへ戻して途中報告を出すこと（最後にまとめて報告しない）。

## リポジトリ配置
- 作業対象: C:\Users\ha.takaku\Desktop\Project\Beacon
- Beacon-old（移植元・参照専用・変更禁止）: C:\Users\ha.takaku\Desktop\Project\Beacon-old

## 読むべき文書（この順で。読み飛ばし禁止）
1. AGENTS.md（特に「環境の既知制約」の固定ルール）
2. docs/rebuild/LESSONS.md（未解決の失敗のみ）
3. docs/rebuild/SPEC.md の §3.3（DesignTokens初期値・「選択表現＝背景ピルのみ・タイトル色は変えない」）・
   §4（検索とランキングのスコア表。**この承認済みスコア表の意味は勝手に変えない**）・
   §7（Beacon固有UX・状態遷移）
4. docs/rebuild/COMPATIBILITY.md §2（標準プラグインの移植分類。検索対象プロバイダーの範囲）
5. docs/rebuild/AUDIT.md A12（StringMatcher.cs / DiacriticsNormalizer.cs / PinyinAlphabet.cs を
   Beacon.Core へ「アダプト」する計画だった行。本フェーズでこの宿題を果たす。
   ただし Pinyin は SPEC の対象外方針に従い**移植しない**）
6. docs/rebuild/adr/ADR-0001（UI境界。Contracts/CoreにUI参照禁止・MIT表記維持）

## 移植元（Beacon-old・参照のみ・コピペ禁止＝UI/Ioc/Settings依存を外して書き直す）
- Flow.Launcher.Infrastructure/StringMatcher.cs
  （FuzzyMatch本体: Acronym Match + サブストリング/部分列マッチ + CalculateSearchScore +
    一致インデックス List<int> の算出。Ioc.Default / Settings / IAlphabet(Pinyin) 依存は外す）
- Flow.Launcher.Infrastructure/DiacriticsNormalizer.cs（アクセント正規化。UI非依存なのでほぼ流用可）
- Flow.Launcher.Plugin/SharedModels/MatchResult.cs
  （Success / Score / MatchData(=一致インデックス) / SearchPrecisionScore の構造を参考にする）
移植コードには Flow Launcher / Wox の MIT 由来表記をファイル冒頭コメントで維持し、
DEPENDENCY_MAP.md / attribution.md に移植元を追記する（ADR-0001 §3）。

## 現状把握（実コード。憶測で二重化しない）
- Beacon.Contracts/SearchContracts.cs: SearchResultDto は Contracts にあり、ContractVersion=2。
  Contracts はプロセス境界（PluginHost RPC）を越える型。**UI都合の変更でContractsを気軽に膨らませない**
- Beacon.Core/RankingEngine.cs: 現在 result.Title に対し exact+600 / StartsWith+300 / Contains+150、
  加えて使用履歴（24h+120 / 7日+60 / 選択回数最大+180 / アクティブプロセス・フォルダ 各+80）、
  WebSearch -250。QueryOrchestrator.SearchAsync が各結果に対しこの Score で上書きする
- Beacon.Platform.Windows/AppSearchProvider.cs: internal static MatchScore(query,candidate) が
  唯一の手書きファジー（完全一致100/部分一致90/頭字語70/部分列 閾値50）。SearchAsync はこれで Score>0 を絞る
- String.Contains だけで一致判定している箇所（＝②。ファジーゼロ・ハイライト不可）:
  - Beacon.Platform.Windows/StandardSearchProviders.cs の
    WindowsSettingsProvider（setting.Terms を Contains）/ BrowserBookmarkProvider（Title・Url を Contains）/
    ProcessKillerSearchProvider（"kill " の後を ProcessName に Contains）/
    SystemActionSearchProvider（Terms を Contains）
  - Beacon.Core/BuiltInSearchProviders.cs の WebSearchProvider は全クエリを拾う設計（一致判定なし＝維持）、
    CalculatorSearchProvider / UrlSearchProvider は式・URL判定であり文字列一致ではない（対象外＝維持）
  - ShellSearchProvider は ">" プレフィクスのコマンド実行（一致判定ではない＝維持）
- Beacon.WinUI/MainWindow.xaml: 結果行の ItemTemplate で Result.Title / Result.Subtitle を
  TextBlock.Text に直接バインドしている（＝ハイライトRunが無い）。ResultRow は MainWindow.R4.cs
- Beacon.WinUI/MainWindow.R4.cs: SearchAsync は _orchestrator.SearchAsync の結果を Score 降順で並べ替えて表示。
  クイックキー適用時は TryQuickKey で「実際の検索文字列(searchText)」と「表示クエリ」を分けている
  （ハイライト対象は searchText＝実際に検索している語であること）
- Beacon.WinUI/SettingsWindow.xaml(.cs): R7の設定画面。ScrollViewer+StackPanel に
  ホットキーTextBox / QuickKeysを改行区切りテキスト手入力 / 除外アプリも改行区切りテキスト、という仮組み。
  コンストラクタで受け取るコールバック（changeHotkey / clipboardEnabled / toggleClipboard /
  personalizationEnabled / togglePersonalization / resetPersonalization / clearClipboard /
  applyClipboardExclusions）と R1Storage キー（GlobalHotkey / QuickKeys / ClipboardExcludedApplications /
  ClipboardEnabled / PersonalizationEnabled / EverythingNoticeShown）は **配線として正しいので壊さない**。
  作り直すのは見た目と操作方法であって、設定の保存先・サービス連携ではない

## Stage 1: 検索エンジン統一（①②③を1本化 + ハイライト）

1. Beacon.Core に統一ファジーマッチャを新規追加（UI参照禁止・純ロジック）:
   - DiacriticsNormalizer を Beacon.Core へ移植（アクセント正規化。既存のCore方針どおりUI非依存）
   - FuzzyMatcher（クラス名は任意）: 入力(query, candidate)に対し
     `record FuzzyMatchResult(bool Success, int Score, IReadOnlyList<int> MatchedIndices)` を返す。
     StringMatcher.FuzzyMatch の Acronym Match（先頭/空白後/大文字/数字を頭字語とみなす）+
     サブストリング/部分列マッチ + CalculateSearchScore + 一致インデックス算出を移植する。
     Pinyin/IAlphabet は移植しない。アクセント無視は DiacriticsNormalizer で常時有効でよい（設定不要）。
     しきい値は SearchPrecisionScore 相当（Regular=50 を既定）を定数で持ち、Success はしきい値到達で判定
   - **これが唯一の一致判定・一致ハイライトの源**。以降すべてのテキスト一致はここを通す
2. ①の置換: AppSearchProvider の手書き MatchScore を削除し、FuzzyMatcher へ差し替える。
   SearchAsync のメンバー判定は FuzzyMatchResult.Success で行う（スコア確定はRankingEngineに委ねる。下記4）
3. ②の置換: StandardSearchProviders.cs の
   WindowsSettingsProvider / BrowserBookmarkProvider / ProcessKillerSearchProvider /
   SystemActionSearchProvider の Contains を、FuzzyMatcher.Success による絞り込みへ置換する。
   - 各プロバイダーが「一致対象にする文字列」を明示（設定=Terms / ブックマーク=Title（Urlは補助でContains可）/
     プロセス="kill "以降 vs ProcessName / システム操作=Terms）。日本語Termは正規化の影響を受けないので従来どおり通る
   - WebSearch / Calculator / Url / Shell は現状維持（一致判定の性質が違う。上記「現状把握」参照）
4. ③の統一（スコアスケールを一本化。SPEC §4の意味は保持）:
   - RankingEngine を「マッチ品質は FuzzyMatcher に一元化し、その上に SPEC §4 の使用履歴・文脈ボーナスを
     加算する」構造へ変更する。現在の title exact/prefix/contains の自前判定は削除し、
     FuzzyMatcher(query, Title).Score を『マッチ品質スコア』として使う
   - プロバイダー固有のベース値（例: Calculator=500, WebSearch=-250 のカテゴリオフセット）は維持する。
     つまり最終スコア = プロバイダーのカテゴリオフセット + FuzzyMatcherのマッチ品質 + 使用履歴/文脈ボーナス、
     の合成として一貫させる。全プロバイダーが同じマッチ品質スケールに乗ることを最優先にする
   - **注意**: SPEC §4 の数値表（完全一致+600 / 前方+300 / タイトル+150 …）は承認済みの製品方針。
     マッチ品質の“機構”をFuzzyMatcherへ替えるのは本フェーズの目的だが、§4の相対的な意味
     （完全一致 > 前方一致 > 部分一致、使用履歴が効く、Webは最下位）が保たれること。
     もし数値の再マッピングが必要になったら、勝手に確定せず対応表を作って**報告し指示を仰ぐ**
     （SPEC §4 を改訂する場合は理由をSPECへ追記）。ファイル検索（Everything/WindowsIndex）は候補源は
     従来どおりだが、並び順の一貫性のため Title に対する FuzzyMatcher スコアで再ランクしてよい
     （性能懸念があれば計測して報告）
5. 一致ハイライト（体感品質の要）:
   - 結果一覧のタイトルで、実際に検索している語（クイックキーを剥がした searchText）に一致した文字を
     **太字**で表示する。色は変えず太さのみ（SPEC §3.3「タイトル色は変えない」を尊重）。
     太字の重み・（必要なら）微差の強調色は DesignTokens.xaml のトークンで定義（直値禁止）。
     TextTrimming（省略記号）は維持する
   - 実装方針（いずれでもよいが Contracts を膨らませない方を優先）:
     (推奨) WinUI 層（ResultRow / MainWindow）で、表示時に Core の FuzzyMatcher を Title に対して再実行して
     一致インデックスを得て、TextBlock の Inlines を Run（通常/太字）で組み立てるヘルパー（例: 添付プロパティや
     小さな変換ヘルパー）を用意する。ContractsのSearchResultDtoは変更しない。
     もし再実行がランキング時の一致と食い違う懸念があるなら、SearchResultDto に
     `IReadOnlyList<int>? TitleMatch` を追加し ContractVersion を 3 へ上げる案を**先に報告**してから採用する
     （ContractsとRPC双方の整合が要るため、独断で上げない）
   - サブタイトルのハイライトは任意（やらない場合はやらない理由を一言）
6. テスト（tests/ に追加。フレームワークは既存に合わせる）:
   - FuzzyMatcher 単体: "vsc"→"Visual Studio Code" が頭字語一致、部分列一致、大小無視、アクセント無視、
     しきい値未満は Success=false、一致インデックスが正しい位置を返す、日本語Termがそのまま通る、を固定
   - RankingEngine: 完全一致 > 前方一致 > 部分一致の順序、使用履歴ボーナス、Web最下位が保たれることを固定
   - 各プロバイダーが FuzzyMatcher 経由で絞り込むこと（Contains時代に落ちていたタイポ・文字飛ばしが拾えること）を最小限固定

## Stage 2: 設定画面リデザイン（配線は保持・見た目と操作方法を作り直す）

前提: R7の SettingsWindow のコールバック配線・R1Storageキー・サービス連携は保持する
（保存先・トグルの意味・移行ロジックを変えない）。作り直すのは XAML と操作フローと入力UI。

1. レイアウト: NavigationView 等による左ナビ（またはセクション積み）で、標準的で親しみやすい設定画面にする。
   セクション例: 一般（ホットキー）/ 検索（Everything案内・必要なら一致しきい値）/ Quick Keys /
   プライバシー（個人化・クリップボード・除外アプリ）/ About。
   ランチャーと同じ DesignTokens.xaml のトーン（色・寸法・角丸・間隔はすべてトークン経由。直値禁止）。
   設定ウィンドウにも Mica/Acrylic 等のバックドロップを適用し本体と統一感を出す（非対応環境はソリッド）。
   **キーボードのみで全項目を操作できること**（Tab移動・フォーカス可視・Enter/Space操作）
2. 設定行の見た目: テキストの羅列をやめ、行単位のカード/リスト風（アイコン+ラベル+説明+右側コントロール）にする。
   CommunityToolkit等の新規NuGetは原則使わず、DesignTokensで軽量な設定行スタイルを自作する
   （どうしても必要なら DEPENDENCY_MAP.md B1 手続きで先に報告）
3. ホットキー: 現在の割り当てをわかりやすく表示し、キャプチャUI（修飾キー+主キーを押す）で変更。
   登録失敗（他アプリ使用中など）はその場でエラー表示して**元のホットキーへロールバック**（R7の
   TryChangeHotkey 経路を使う）。無効な状態で確定させない
4. Quick Key編集: 改行区切りテキストをやめ、「キー入力 → アクションをドロップダウンで選択」の行を
   追加/削除できるリストUIにする。割り当て可能アクションは内蔵アクションv1（BuiltInActions.All）から選ぶ。
   重複キー・空キーを検証。保存は R1Storage の QuickKeys。既定へ戻すボタンを維持
5. クリップボード除外アプリ: 改行区切りテキストをやめ、追加/削除できるリストUIにする。
   保存は ClipboardExcludedApplications。監視サービスがこの一覧を尊重する既存挙動を壊さない
6. プライバシー: 個人化ON/OFF・個人化全リセット・クリップボード履歴ON/OFF・全削除を配置。
   破壊的操作（個人化全リセット・クリップボード全削除）は確認を必須にする（R7の確認相当を維持）。
   トレイメニューの同項目と状態が常に一致すること（同じサービス/同じR1Storageを操作）
7. Everything案内・About: R7の内容（案内の再表示リセット / バージョン・ContractVersion /
   Third-party notices・LICENSE 閲覧）を維持しつつ、新レイアウトへ載せ替える。
   表示するライセンス表記は実配布物のファイルと一致させる（存在しない表記を載せない）

## 非対象範囲（やらない）
- Legacy importer（移行ロジック）の作り直し（R7実装を保持・退行させないだけ）
- 状態遷移・IME・アクリル/枠/角丸・レンダリング挙動・実行契約そのものの変更
- Pinyin/ダブルPinyin検索・検索精度のユーザー設定UI（しきい値は内部定数。UI露出は任意）
- 第三者プラグイン対応・プラグイン移行（実装対象外 2026-07-20）
- テーマ/UI寸法のユーザー設定・クリップボード画像対応・AI機能・クラウド送信
- 正式識別子切替（Beacon.Next→Beacon）→ Gate D(R10) / MSIX → R11

## 変更可能ファイル
src/**（ただし Beacon.Contracts は「SearchResultDto に TitleMatch を足す＋ContractVersion=3」を
採用する場合のみ、事前報告の上で変更可。それ以外の Contracts 変更と Beacon.PluginHost 変更は禁止）,
tests/**, Beacon.sln, src/Beacon.WinUI/Resources/DesignTokens.xaml（トークン追加）,
docs/rebuild/DEPENDENCY_MAP.md・attribution.md（移植元StringMatcher/DiacriticsNormalizerの追記）,
docs/rebuild/SPEC.md（§4の数値を再マッピングする場合のみ・報告と承認後）,
docs/rebuild/LESSONS.md（記録基準を満たす失敗時のみ）

## 禁止事項
- `Spotlight` の語（識別子・XAML・コメント・リソースキー・UI文字列・ログ・ファイル名すべて）
- Contracts / Core / Platform.Windows への WPF・WinUI・WinForms 参照 /
  WinUI本体プロセスへの System.Windows.* 追加（FuzzyMatcher・DiacriticsNormalizer は純ロジックでCoreに置く）
- デリゲート・任意object・UI型のプロセス境界越し送信 / Contractsの独断変更
- DesignTokens.xaml 外への直値の色・寸法・時間（ハイライトの太さ・強調も含む）
- B1記載外の新規NuGet / iNKORE由来物 / SegoeFluentIcons.ttf / Apple固有素材
- Beacon-old側の変更 / 移植元のMIT表記を落とすこと
- SPEC §4 の承認済みスコア方針を黙って変えること（変更が要るなら報告して指示を仰ぐ）

## ビルド／検証（各Stage末に1〜3、全Stage完了後に全部）
1. dotnet build Beacon.sln -c Release で0警告・0エラー
2. dotnet test Beacon.sln -c Release で全テスト成功（Stage 1で追加したマッチャ/ランキングのテスト含む）
3. Test-NoUiReferences.ps1 がローカルで0件（FuzzyMatcher/DiacriticsNormalizerがCoreでUI非依存であること）
4. Build-Portable.ps1 → Test-Portable.ps1（既定）→ Test-Portable.ps1 -UseActivationPipe
5. ZIP内: Everything.dll(x64)あり / iNKORE・System.Drawing.Common・WPF系DLLなし
6. 起動確認（Codexが実施。ここまでがCodexの検証範囲）:
   - Portable展開物を起動し、"vsc" 等の頭字語・タイポ・文字飛ばしでアプリ/設定/ブックマークが
     従来より拾えること、結果タイトルに一致文字の太字ハイライトが出ること、
     並び順が完全一致>前方>部分の直感に沿うことをログ/目視前提で確認（体感判定はユーザーへ引き渡す）
   - トレイ「設定」で新設定画面を開き、ホットキー変更/QuickKey編集（ドロップダウン）/除外アプリ追加削除/
     各トグルが R1Storage に反映され再起動後も維持されることをファイルで確認
   - Data\Logs\ に ERROR/Exception が出ていないこと、終了後に Beacon.Next系プロセスが残らないこと

## ユーザーが目視・手動で確認する（Codexは実施不要・報告に列挙するだけ）
- 検索の一致品質・並び順・ハイライトが旧WPF版に対して見劣りしないか（体感。本フェーズの主目的）
- 設定画面の見た目・トーン・操作感がランチャーと一貫し、製品水準に達しているか（体感）
- ホットキーキャプチャの操作感・衝突時ロールバック / QuickKey・除外アプリのリスト編集操作感
- 日本語IME・Light/Dark・DPI 100〜200% が検索ハイライトと設定画面で破綻しないこと

## 完了条件
- テキスト一致がすべて Beacon.Core の単一 FuzzyMatcher を通り、①の手書きMatchScoreと②のContainsが撤去され、
  ③RankingEngineが同一マッチ品質スケール上で SPEC §4 の相対順序を保って合成される
- 結果タイトルに一致文字の太字ハイライトが表示され、色は変えていない（SPEC §3.3尊重）
- 設定画面が左ナビ等の製品水準レイアウトで、ホットキー/QuickKey(ドロップダウン)/除外アプリ(リスト)/
  個人化・クリップボードのトグルと破壊的操作の確認/Everything案内/About が
  キーボードのみで操作でき、R7の保存先・サービス連携・移行ロジックに退行がない
- R4〜R7の完了条件（DPI・複数モニター・IME・レンダリング・アクリル/枠・クリップボード監視・Legacy移行）に退行なし
- Codexの責任範囲: 自動検証（build/test/NoUiRefs/Portableスモーク）＋起動確認＋
  マッチャ/ランキングのテスト固定まで。検索体感・設定画面の見た目の最終判定はユーザー確認待ちとして列挙

## 失敗時
AGENTS.md「環境の既知制約」該当は記録せず固定ルールに従って切り替える。それ以外は
docs/rebuild/LESSONS.md の記録基準を満たす場合のみ記録してから再試行。SPEC（特に §4 スコア方針）と
矛盾する実装や Contracts の独断変更を勝手に進めず、理由と案を報告して指示を仰ぐ。

## Git差分要約
Stageごとに変更・追加ファイル一覧と概要を報告する。コミットはユーザーが行う
（Stage完了ごとにコミットを促してよい）。
```

## Phase R8.1: 設定画面の起動不能修正 + 製品水準仕上げ（R8是正）

**状況（2026-07-23）: 本フェーズはユーザー承認の例外としてClaudeが直接実装済み**（Codex実行不要）。Stage A/B、アプリアイコン適用（exe / ウィンドウ / トレイ / About）、BEACON_SMOKE_SETTINGSスモークまで完了。build 0警告0エラー / 全テスト成功 / Test-NoUiReferences 0件 / Test-Portable（既定・-UseActivationPipe）成功。残りはユーザー目視確認のみ。

前提: 現行作業ツリー（`feature/rebuild-r7`）の R8 実装（FuzzyMatcher統一・SettingsWindow）を保持したまま是正する。原因は確定済み（憶測での再調査は不要）: `App.xaml` に `XamlControlsResources` が無く、`NavigationView` の生成が `XamlParseException: Cannot find a Resource with the Name/Key TabViewButtonBackground` で失敗する（microsoft/microsoft-ui-xaml #5406・#2629 と同一事象。MainWindow は generic.xaml で足りる基本コントロールのみのため顕在化しなかった）。

```
あなたはBeacon（WinUI 3 Portable-firstランチャー）のPhase R8.1を担当する。
目的は2つ。(1) 設定画面が開かないバグの修正と再発の機械的防止。
(2) R8 Stage 2 で未達だった設定画面の見た目・操作を製品水準（ランチャーと同じ
ミニマルなトーン）へ仕上げる。R8プロンプトの「読むべき文書」「禁止事項」
「非対象範囲」「Git差分要約」はそのまま適用する。設定の保存先（R1Storageキー）・
コールバック配線・Legacy移行・検索エンジンは変更しない。

## Stage A: 起動修正（最小差分・最優先）
1. src/Beacon.WinUI/App.xaml の MergedDictionaries の先頭に
   <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" /> を追加する。
   Resources/DesignTokens.xaml は後段に置いたまま（後勝ちで上書きを維持）。
2. XamlControlsResources 追加で WinUI 既定テーマ資源が全て有効になるため、
   ランチャー側（検索バー・結果リスト・アクリル）の見た目退行がないか起動確認する。
   DesignTokens の TextControl* / ListViewItemBackground* 上書きが効いていること。
3. 再発の機械的防止: 環境変数 BEACON_SMOKE_SETTINGS=1 のとき、起動完了後に
   ShowSettings を1回呼ぶフックを追加し、src/Beacon.Distribution/Test-Portable.ps1 の
   起動スモークで同変数を設定する。設定画面の生成が失敗すれば ShowSettings が
   ERROR をログへ書き、既存の「ログに ERROR|Exception があれば失敗」検査が落ちること。
4. Stage A 完了時点で Build-Portable → Test-Portable を通し、実機で
   トレイ「設定」から設定画面が開くことを確認してから Stage B へ進む。

## Stage B: 設定画面の仕上げ（R8 Stage 2 の未達分）
1. 設定行スタイル: R8 Stage 2-2 の「行単位のカード/リスト風
   （アイコン+ラベル+説明+右側コントロール）」を実装する。素の TextBox/Button の
   縦羅列をやめる。色・寸法・角丸・間隔・フォントサイズは DesignTokens.xaml の
   トークン経由（直値禁止。必要なトークンは追加してよい）。
2. 即時適用: Quick Keys・除外アプリの「保存」ボタンを廃止し、行の追加・削除・編集で
   即検証・即保存する（macOS System Settings と同じ操作モデル）。無効行（空キー・
   重複キー）はその行のインラインエラーで示し、保存対象から除外する。
   「既定へ戻す」ボタンは維持する。
3. 確認ダイアログ: Win32 MessageBoxW を設定画面では使わず ContentDialog へ置換する。
   併せて現在「個人化データを全リセット」は SettingsWindow.OnResetPersonalization と
   MainWindow.ResetPersonalization の両方が確認を出し**ダイアログが2回連続で出る**。
   確認責務を1箇所（実行側 MainWindow）へ寄せ、確認は必ず1回にする。
   文言は「個人化データをすべて削除しますか？」へ統一する（トレイ経路と同一文言）。
4. wayfinding: 固定の大見出し「設定」をやめ、選択中セクション名（一般 / 検索 /
   Quick Keys / プライバシー / About）をページ見出しへ反映する。
5. タイポグラフィ統一: 見出しだけでなく本文・説明・コントロール文字列にも
   LauncherFontFamily 系（Zen Kaku Gothic New）を適用し、ランチャーとトーンを揃える。
6. ホットキー行: TextBox Header の長文（「現在の割り当て（修飾キーと主キーを
   押してください）」）をやめ、行カードの「ラベル=グローバル ホットキー /
   説明=クリックして修飾キーと主キーを押すと変更 / 右側=現在の割り当て表示」へ分離。
   登録失敗時のインラインエラーとロールバック挙動は維持する。

## 検証（Stage A 末に 1〜3、全体完了後に全部）
1. dotnet build / dotnet test -c Release 0警告0エラー・全テスト成功
2. Test-NoUiReferences.ps1 0件
3. Build-Portable.ps1 → Test-Portable.ps1（BEACON_SMOKE_SETTINGS による設定画面生成込み）
   → Test-Portable.ps1 -UseActivationPipe
4. 起動確認（Codex範囲）: トレイ「設定」で設定画面が開く / 各セクション切替 /
   ホットキー変更・QuickKey編集・除外アプリ編集が R1Storage へ即時反映され再起動後も
   維持される / Data\Logs に ERROR/Exception なし / 終了後にプロセス残留なし

## ユーザーが目視・手動で確認する（列挙して引き渡す）
- 設定画面のトーン・行カードの見た目がランチャーと一貫し製品水準か（体感）
- 即時適用の操作感 / ContentDialog の確認が1回だけ出ること
- Light/Dark・DPI 100〜200% で設定画面・ランチャー双方が破綻しないこと
- XamlControlsResources 追加後のランチャー外観に退行がないこと（体感）
```

## Phase R9: UX完成度（段階表示・スコア実数化・Quick Keys一本化・状態可視化）

前提: R8.1目視承認済み・作業ツリーに R8/R8.1 実装が存在。実装ブランチは `feature/rebuild-r9-ux`。仕様は `docs/rebuild/R9_UX.md` が正（本プロンプトと矛盾したら R9_UX.md を優先し、矛盾箇所を報告）。R8プロンプトの「読むべき文書」「禁止事項」「Git差分要約」を引き継ぐ。**Stage順に実装し、各Stage末でビルド・テストをグリーンへ戻して途中報告する。**

```
あなたはBeacon（WinUI 3 Portable-firstランチャー）のPhase R9を担当する。
仕様は docs/rebuild/R9_UX.md（S1〜S7）が正。目的は「見た目の良いランチャー」を
「Flow Launcherより良い検索体験」へ引き上げること。機能追加はR9_UX.md記載分のみ。

## 読むべき文書（この順で）
1. AGENTS.md（環境の既知制約）
2. docs/rebuild/LESSONS.md
3. docs/rebuild/R9_UX.md（本フェーズの正。「検証済みの現状問題」表の file:line が着手点）
4. docs/rebuild/SPEC.md §3.3 / §4（2026-07-23改訂: tier+rawScore）/ §7（特に7.1・7.4・7.8）
5. docs/rebuild/PLAN.md Phase R6（毎フレームHWND Resize禁止の経緯。**この方針を退行させない**）

## Stage 1: 検索結果の段階表示 + 計測分離（R9_UX.md S1）
1. MainWindow.R4.cs SearchAsync を改修: 全列挙完了後の一括 ScheduleResults をやめ、
   (a) 初回=最初の3件到着 or 16ms経過の早い方で表示（HWND Resizeはこの1回）、
   (b) 以降は表示上位の構成が変わったときだけ ListView差分更新（ApplyResults は既に差分適用
       なので流用可）。HWND Resize は表示件数が変わったときのみ、
   (c) 80〜120ms新着なしで Stable 扱い。
   クエリ途中変更時の破棄（displayedQuery != QueryBox.Text）とキャンセルの既存挙動を維持。
2. PERFログを3分離: InputToFirstCandidateMs（旧InputToFirstResultMsの改名。Channel到着）/
   InputToFirstPaintMs（初回ApplyResults完了）/ InputToStableResultsMs（Stable確定）。
   R6のしきい値検査（Test-Portable.ps1のログ検査）があれば新名称へ追随させる。
3. テスト: 遅延プロバイダー（2sタイムアウト相当のFake）を混ぜても速いプロバイダーの結果が
   先に確定することを Orchestrator/表示ロジックの単体で固定（UIテスト不要。ロジックを
   Core側へ寄せられるなら ResultBatcher 等の純ロジックとして切り出してテストする）。

## Stage 2: Fuzzyスコア実数化（R9_UX.md S2。SPEC §4改訂は承認済み）
1. Beacon.Core/FuzzyMatcher.cs:35-38 の tier のみ返却をやめ、tier + rawScore を返す。
   マッチ種別（タイトル完全一致 > 単語先頭一致 > 連続部分一致 > 頭字語 > 非連続一致）を
   スコアで分離。サブタイトル・パス一致はタイトルより低いベースで合成。
2. RankingEngine との合成規則を明文化（コード上の定数とコメント＋R9_UX.mdへの追記報告）:
   tier加算が使用履歴ブースト（最大+180等）を常に打ち消さないこと。
3. AppSearchProvider の一致対象へ実行ファイル名・ショートカット名を追加（タイトルに加え。
   最良スコアを採用）。UWPパッケージ名・エイリアスは対象外（P1）。
4. テスト更新: "vs" → Visual Studio Code が VideoStudio 等より上位になることを固定。
   既存の FuzzyMatcher / RankingEngine テストを実スコア前提へ更新（完全>前方>部分の
   相対順序・Web最下位・履歴ボーナスのテストは意味を保って残す）。

## Stage 3: Quick Keys一本化 + アクションフィルタ（R9_UX.md S3・S4）
1. Beacon.Core に QuickKeyRegistry を新設（DefaultMappings=BuiltInActionsのQuickKey定義を正 /
   Load・Saveは保存が無ければDefaultMappingsを返す。空Dictionaryフォールバック禁止 /
   FindAction(key) / FindKey(actionId)）。保存はこれまでどおり R1Storage "QuickKeys"
   （SettingsWindowの既存保存形式と互換を保ち、移行不要にする）。
2. 参照を一本化: MainWindow.R5.cs TryQuickKey（:323の空Dictionary既定を廃止）/
   ゴースト補完（:334）/ SettingsWindow の DefaultQuickKeys()（:220を廃止しRegistry参照）/
   結果バッジ（MainWindow.R4.cs:379 の固定 "term"/"rf" を廃止しRegistry逆引き）。
   バッジは選択中の結果行にのみ表示する。
3. ActionDescriptor へ AppliesTo（File/Folder/Application/Url のFlags）を追加し、
   OpenActions（MainWindow.R5.cs:149-157）で対象種別により絞る（対応表はR9_UX.md S4）。
4. open-with を「プログラムから開く」ダイアログへ変更（SHOpenWithDialog。API仕様は
   Microsoft Learn一次情報で確認してから実装）。copy/move の宛先入力に FolderPicker を
   第一導線として追加（unpackagedでは InitializeWithWindow が必要。既存の文字入力は
   フォールバックとして残す）。
5. テスト: QuickKeyRegistry（既定値・保存値・逆引き）/ AppliesToフィルタの対応表を固定。

## Stage 4: 初回導線 + StatusRow + 設定追補（R9_UX.md S5・S6・S7）
1. 初回起動時のみ Welcome ウィンドウ（アプリ名＋「Alt + Space でいつでも検索」＋
   [試してみる]＋[Windows起動時に開始]トグル）。表示済みフラグはR1Storage。
   常設ホーム画面は作らない。
2. Everything未検出のネイティブMessageBox（MainWindow.xaml.cs:112-119）を廃止し、
   初回=Welcome内、通常時=StatusRow へ移す。
3. StatusRow: 通常結果と別種の行（検索中 / 結果なし / 一部の検索元が応答しない /
   実行失敗 / キャンセル）。警告色・再試行導線・AutomationProperties.LiveSetting。
   SearchAsync の例外（MainWindow.R4.cs:195）と実行失敗をログに加えStatusRowへ反映。
   ContextActions中はScope Chip位置に「<対象名> › アクション」のパンくず＋初回数回の
   Escヒント。
4. 設定画面: Everythingカードを状態表示（接続済み/未接続＋[接続を再確認]）へ変更し
   「案内を再表示」ボタン（SettingsWindow.xaml.cs:305）を廃止 / 一般へ
   「Windows起動時に開始」（既存スタートアップ登録サービスを流用）と
   外観 System・Light・Dark を追加 / 除外アプリへ[起動中のアプリから選択][exeを選択]を追加
   （手入力は残す）/ QuickKeyBrush 等バッジ配色を Light/Dark の ThemeDictionary へ移動。
   すべて DesignTokens.xaml 経由（直値禁止）。

## 禁止・維持事項（R8プロンプトの禁止事項に加え）
- 毎フレームHWND Resizeへ戻さない（R6方針）。段階表示でもResizeは件数確定時のみ
- Contracts変更は原則禁止（必要が生じたら案を報告して指示を仰ぐ）
- SPEC §3.3のデザイントークン集約 / Escは常に1段戻る（§7.1）/ クリップボード初期OFF を退行させない
- 新規NuGet原則禁止（必要なら DEPENDENCY_MAP.md B1 手続きで先に報告）

## ビルド／検証（各Stage末に1〜3、全Stage完了後に全部）
1. dotnet build Beacon.sln -c Release 0警告0エラー
2. dotnet test Beacon.sln -c Release 全テスト成功
3. Test-NoUiReferences.ps1 0件（QuickKeyRegistry・ResultBatcher等のCore追加分がUI非依存）
4. Build-Portable.ps1 → Test-Portable.ps1（既定）→ -UseActivationPipe
5. 起動確認（Codex範囲）: 遅延プロバイダー相当の状況でも初回表示が先行すること（PERFログの
   InputToFirstPaintMs で確認）/ 新規Data（設定なし）で "foo rf" が即動作 / アクション一覧が
   対象別に絞られる / Welcomeが初回のみ表示 / StatusRowが結果なし・実行失敗で出る /
   Data\Logs に ERROR なし / プロセス残留なし
6. InputToFirstPaintMs < 50ms 目安（超過時は実測値と原因を報告。勝手にしきい値を変えない）

## ユーザーが目視・手動で確認する（列挙して引き渡す）
- 段階表示の体感（ちらつき・並び替えの暴れがないか）と旧WPF版との速度比較
- "vs" 等の曖昧クエリの並び順が直感に沿うか（本フェーズの主目的）
- Welcome画面・StatusRow・パンくず・バッジ（選択行のみ表示）の見た目とトーン
- 設定画面のEverything状態表示 / 外観切替 / 除外アプリ選択導線の操作感
- Light/Dark・DPI 100〜200%・日本語IMEで新UI（Welcome/StatusRow/バッジ配色）が破綻しないこと

## 完了条件
- R9_UX.md S1〜S7の受入条件をすべて満たす（同文書「検証済みの現状問題」表の8件が解消）
- R4〜R8.1の完了条件（DPI・IME・レンダリング・設定保存先・Legacy移行・検索統一）に退行なし
- Codexの責任範囲は自動検証＋起動確認まで。体感・見た目の最終判定はユーザー確認待ちとして列挙

## 失敗時
AGENTS.md「環境の既知制約」該当は固定ルールで切替。それ以外は LESSONS.md の記録基準を
満たす場合のみ記録して再試行。R9_UX.md・SPECと矛盾する実装を勝手に進めず報告して指示を仰ぐ。
```
