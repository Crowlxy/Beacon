# PROMPTS — Codex実装プロンプト

運用: ユーザーが本リポジトリ（`C:\Users\ha.takaku\Desktop\Project\Beacon`）を作業ディレクトリとしてCodexを**対話モードで**実行し、以下のプロンプト本文を渡す（モデル: gpt-5.6-sol）。ヘッドレスの `codex exec` はこの環境ではサンドボックス制限で失敗するため使わない（LESSONS-archive.md 2026-07-16）。
**本ファイルには現行フェーズのプロンプトのみ置く。** R3は2026-07-20完了。R4はR4.1〜R4.3を経てR4.3でGate B承認（2026-07-21ユーザー承認。B-1の枠再発はR4.4での修正を待たずユーザー確認によりクローズ）。R4.4は未着手のまま不要化。統合R5（旧R5・R6・R8。旧R7=第三者プラグイン対応は実装対象外）はStage 1〜3実装後、2026-07-22にGate C承認（PLAN.md参照）。同日「検索レイテンシ改善」保守修正をユーザー検証済み（HotkeyToDisplayMs 240→33.7ms。一部受け入れ項目はR6へ引き継ぎ）。R6（性能・応答性＝レンダリング刷新）は2026-07-22にレビュー→承認・完了（下記R6結果注記。1点だけWindowsIndexSearch接続撤回のユーザー判断待ち）。**現行フェーズ = Phase R7（設定・データ移行＝設定画面UI + Legacy importer）**。旧番号R6・R8は歴史的言及、旧「R9 設定・データ移行」が現行R7（PLAN.md 2026-07-22注が正）。


## Phase R7: 設定・データ移行（設定画面UI + Legacy importer）

前提: R6（レンダリング刷新）が**mainにマージ済み**であることをユーザーへ確認してから着手する。実装ブランチは `feature/rebuild-r7-settings-migration`。本フェーズは Gate C合格の機能・UI・デザインへの**追加**であり、R4〜R6で確定した検索・ランキング・IME・アクリル/枠/角丸・レンダリング挙動を退行させない。新規NuGet依存の追加は原則禁止（必要なら DEPENDENCY_MAP.md B1 手続きを踏み、追加前に報告して指示を仰ぐ）。**Stage 1（設定画面UI）を先に完了・報告してから Stage 2（移行）へ進む。**

```
あなたはBeacon（WinUI 3 Portable-firstランチャー・独立リポジトリ）のPhase R7を担当する。
目的は「これまでトレイメニューとDataRoot配下の設定ファイルだけで管理していた設定を、
ユーザーが操作できる設定画面UIとして提供し、旧WPF版（Beacon-old）からのデータ移行を
一度きり・明示的・非破壊で行えるようにする」こと。検索・ランキング・状態遷移・IME・
レンダリングなど既存挙動の変更はしない（設定値の反映口を増やすだけ）。
必ず Stage 1（設定画面UI）→ Stage 2（Legacy移行）の順で実装し、各Stage完了時に
ビルド・テストをグリーンへ戻して途中報告を出すこと（最後にまとめて報告しない）。

## リポジトリ配置
- 作業対象: C:\Users\ha.takaku\Desktop\Project\Beacon
- Beacon-old（移行元・参照専用・変更禁止）: C:\Users\ha.takaku\Desktop\Project\Beacon-old

## 読むべき文書（この順で。読み飛ばし禁止）
1. AGENTS.md（特に「環境の既知制約」の固定ルール）
2. docs/rebuild/LESSONS.md（未解決の失敗のみ）
3. docs/rebuild/SPEC.md の §4末（Everythingは「設定画面で一度だけ案内」）・§6（Windows統合）・
   §7.4（Actions/Quick Keys。「編集UIはR7」）・§7.5（クリップボード履歴。「除外アプリ指定UIはR7」）・
   §7.6（個人化。「全リセット・OFFの設定画面はR7」）。
   ※SPEC本文に「§6.6」という見出しは実在しない。設定画面の要件は上記の各節に分散しているので
   それらを正とする（他文書に残る「§6.6」表記はこの分散要件を指す歴史的言及）
4. docs/rebuild/MIGRATION.md（移行元→移行先の対応表・移行フロー・並行運用の安全規則）。
   ※本文中の「Phase R9」は現行フェーズ番号R7の旧称（PLAN.md 2026-07-22注が正）。
   R7開始時にこの旧番号表記を修正し、§14の「Settingsプロパティ走査でのマッピング確定」を実施する
5. docs/rebuild/adr/ADR-0004（DataRoot解決規則。移行先は必ず解決済みDataRoot配下）
6. docs/rebuild/PLAN.md 「Phase R7」（作業項目と完了条件。本プロンプトの正）
7. 現ツリーの設定まわり:
   - src/Beacon.WinUI/DataRootResolver.cs（R1Storage。設定ファイル
     <DataRoot>\Settings\r1-settings.json の既定生成・GetBoolean/SetBoolean・ログローテーション）
   - src/Beacon.WinUI/MainWindow.xaml.cs（トレイ配線・ClipboardEnabled/PersonalizationEnabled
     の読み書き・TogglePersonalization/ResetPersonalization）
   - src/Beacon.WinUI/NativeWindowController.cs（RegisterHotKey・トレイメニュー項目・
     ホットキーが現状ハードコード＝VirtualKeySpace 0x20 + Alt+Shift+Space で登録されている点）

## 現状把握（ここを踏まえて設計する。憶測で二重化しない）
- 設定は <DataRoot>\Settings\r1-settings.json に集約されている。既存キー:
  ContractVersion / LogRetentionCount / ClipboardEnabled / ClipboardExcludedApplications /
  PersonalizationEnabled / QuickKeys{rf,cp,rn,term}。**この単一ファイルを設定の正とし、
  新しい保存先やスキーマを勝手に作らない**（ファイル名 r1-settings.json は歴史的経緯だが
  改名はデータ移行を伴うため本フェーズでは行わず、必要性があれば報告のみ）
- 読み書きは R1Storage の GetBoolean/SetBoolean しかない（真偽値専用）。ホットキー文字列・
  QuickKeys など非真偽値の型付きアクセサが必要になるので、R1Storage を最小限拡張して
  文字列/オブジェクトの安全な読み書き（一時ファイル+置換の既存パターン踏襲）を足す
- ホットキーは現状コード定数（Alt+Shift+Space）で、設定ファイルから読んでいない。
  設定画面で変更できるようにするには「設定ファイルから読む→RegisterHotKey→失敗時は
  旧ホットキーへロールバック」の経路が必要
- 設定のロジック（バリデーション・移行・スキーマ）は Beacon.Core、Windows依存
  （HKCU Run・DPAPI・RegisterHotKey）は Beacon.Platform.Windows、描画のみ Beacon.WinUI に置く

## Stage 1: 設定画面UI

1. 設定ウィンドウ: トレイメニューに「設定」を追加し、独立した AppWindow として設定画面を開く
   （ランチャー本体ウィンドウとは別。多重に開かない＝既に開いていれば前面化）。
   閉じてもアプリは常駐し続ける。ランチャーと同じ DesignTokens.xaml のトーン
   （色・寸法・時間はすべてトークン経由。直値禁止）。標準的な設定画面レイアウト
   （左ナビ or セクション積み）で親しみやすさ優先。キーボードのみで全項目を操作できること
2. グローバルホットキー変更: 現在のホットキーを表示し、キャプチャUI（修飾キー+主キーを押して設定）で
   変更できる。押下の組み合わせを検証し、登録失敗（他アプリが使用中など）は
   その場でエラー表示して**元のホットキーへロールバック**（無効な状態で確定させない）。
   確定値は r1-settings.json に保存し、次回起動時もそれで登録する。
   ※既定値の扱い: 現状の開発中識別子 Beacon.Next では旧版との衝突回避のため Alt+Shift+Space を
   既定にしている。PLAN/SPECの「正式既定 Alt+Space」は Gate D の本番識別子切替時の話。
   本フェーズでは**既定を勝手に Alt+Space へ変えず**、設定ファイルの現既定（Alt+Shift+Space）を
   初期値として保持し、変更手段のみ提供する。既定値変更の要否は報告して判断を仰ぐ
3. Quick Key編集: 既定4種（rf=保存場所を表示 / cp=パスをコピー / rn=名前変更 / term=ターミナル）の
   キー割り当てを編集・追加・削除できる。割り当て可能なアクションは内蔵アクションv1（SPEC §7.4）から選ぶ。
   重複キー・空キーを検証。保存は r1-settings.json の QuickKeys。既定へ戻すボタンを用意
4. 個人化・クリップボードのON/OFFと全リセット: これまでトレイのみだった
   個人化ON/OFF・個人化全リセット・クリップボード履歴ON/OFFを設定画面にも出す
   （トレイ項目は維持してよい。両者が同じ R1Storage/同じサービスを操作し状態が一致すること）。
   破壊的操作（個人化の全リセット・クリップボード履歴の全削除）は SPEC §7.4 の確認フロー相当の
   確認を必須にする
5. クリップボード除外アプリ指定UI（SPEC §7.5。データ枠 ClipboardExcludedApplications は既存）:
   除外するアプリ（プロセス名/実行ファイル）を追加・削除できるUIを付け、
   r1-settings.json の ClipboardExcludedApplications に保存する。監視サービスがこの一覧を
   尊重して除外することをテストで固定する（OFF時は監視APIを呼ばない既存保証を壊さない）
6. Everything未起動の一度きり案内（SPEC §4末）: Everythingが未導入/未起動のとき、
   検索結果にエラーを混ぜず、**一度だけ**案内を出す（案内済みフラグを設定ファイルに保持し
   再表示しない）。設定画面から再表示/リセットできる導線を用意してよい。
   案内は「Everythingがあると高速」という情報提供に留め、強制しない
7. About・Third-party notices: バージョン/ContractVersion表示と、配布物同梱の
   attribution.md / LICENSE 相当（Flow Launcher・Wox・Everything・Windows App SDK等の表記）を
   閲覧できる画面。表示内容は実配布物のライセンスファイルと一致させる（存在しない表記を載せない）

## Stage 2: Legacy importer（MIGRATION.md が正）

1. Settingsプロパティ走査（MIGRATION.md §14）: Beacon-old の Settings.cs 全プロパティを走査し、
   新スキーマ（r1-settings.json のキー）への対応表を作成して報告する。
   移行する値の確定リスト（ホットキー / ColorScheme / 言語 / Everything設定 /
   カスタムショートカット等）と、移行しない値（テーマ・UI寸法・時計/サウンド。新版は固定デザインで該当機能なし）を明記
2. 検出→確認→バックアップ→変換→整合性確認→ロールバック→記録（MIGRATION.md §2 のフロー厳守）:
   - 初回起動時に旧データ（%APPDATA%\Beacon または旧ポータブルの UserData\）を検出したら、
     **対象と移行先を表示してユーザー確認を求める**（自動移行しない）
   - 移行前に旧データを <BeaconRoot>\Data\Backup\legacy-<日付>\ へバックアップ
   - 変換・コピー後に整合性確認（ファイル数・主要キーの読み戻し）
   - 失敗時は新Data側の書き込みを消して未移行状態へロールバック（旧版はそのまま使える）
   - <BeaconRoot>\Data\State\migration.json に移行バージョン・日時・結果を記録し再実行を防ぐ
   - **元データはユーザー確認なしで削除しない**（移行成功後も残す。削除はユーザー明示操作のみ）
3. プラグイン移行は対象外: 第三者プラグイン対応は実装対象外（2026-07-20ユーザー決定）のため、
   MIGRATION.md §1 の「ユーザー導入プラグイン」「プラグイン設定」および §4「プラグイン互換API」に
   関わる移行は**行わない**。この矛盾を MIGRATION.md 側にも注記して整合させる（移行対象から除外と明記）
4. 並行運用の安全規則（MIGRATION.md §3）を守る: 新旧でファイルを共有しない
   （旧版は %APPDATA%\Beacon を使い続け、新版は移行時の読み取り以外触らない）。
   開発中identifier（Beacon.Next / Mutex・パイプ別名）で単一インスタンスが衝突しないこと

## 非対象範囲（やらない）
- 正式識別子切替（Beacon.Next → Beacon）→ Gate D（R10） / MSIX → R11
- 第三者プラグイン対応・プラグイン移行一式（上記 Stage 2-3）
- テーマ/UI寸法のユーザー設定・クリップボード画像対応・AI機能・クラウド送信
- 検索・ランキング・状態遷移・IME・レンダリング挙動そのものの変更

## 変更可能ファイル
src/**（ただし Beacon.Contracts・Beacon.PluginHost は変更禁止）, tests/**, Beacon.sln,
src/Beacon.Distribution/**, src/Beacon.WinUI/Resources/DesignTokens.xaml（トークン追加）,
docs/rebuild/MIGRATION.md（旧番号修正・プラグイン除外注記・マッピング確定）,
docs/rebuild/DEPENDENCY_MAP.md・attribution.md（依存・移植追記）,
docs/rebuild/LESSONS.md（記録基準を満たす失敗時のみ）

## 禁止事項
- `Spotlight` の語（識別子・XAML・コメント・リソースキー・UI文字列・ログ・ファイル名すべて）
- Contracts / Core / Platform.Windows への WPF・WinUI・WinForms 参照 /
  WinUI本体プロセスへの System.Windows.* 追加
- デリゲート・任意object・UI型のプロセス境界越し送信
- DesignTokens.xaml 外への直値の色・寸法・時間
- B1記載外の新規NuGet / iNKORE由来物 / SegoeFluentIcons.ttf / Apple固有素材
- Beacon-old側の変更 / 仕様の独自変更（SPEC・MIGRATIONと矛盾したら実装せず報告）
- 保存先の無言切替（設定は既存の r1-settings.json、移行先は解決済みDataRoot配下のみ）

## ビルド／検証（各Stage末に1〜3、全Stage完了後に全部）
1. dotnet build Beacon.sln -c Release で0警告・0エラー
2. dotnet test Beacon.sln -c Release で全テスト成功
3. Test-NoUiReferences.ps1 がローカルで0件
4. Build-Portable.ps1 → Test-Portable.ps1（既定）→ Test-Portable.ps1 -UseActivationPipe
5. ZIP内: Everything.dll(x64)あり / iNKORE・System.Drawing.Common・WPF系DLLなし
6. 起動確認（Codexが実施。ここまでがCodexの検証範囲）: Portable展開物を起動し、
   トレイ「設定」で設定画面を開く→ホットキー変更/QuickKey編集/各トグルの保存が
   r1-settings.json に反映され再起動後も維持されることをログ/ファイルで確認。
   Data\Logs\ に ERROR / Exception が出ていないこと、終了後に Beacon.Next系プロセスが
   残らないことを確認する
7. 移行の自動確認: 単体/結合テストで「旧設定なしの新規起動」「正常な旧設定からの移行」
   「壊れた旧設定からの安全な起動」「移行失敗時のロールバックと旧版継続」
   「ユーザー確認なしで旧データを削除しない」を固定する（実データが要る箇所は
   テスト用フィクスチャで再現し、不可能な項目は理由付きで未確認と明記）

## ユーザーが目視・手動で確認する（Codexは実施不要・報告に列挙するだけ）
- 設定画面の見た目・トーンがランチャーと一貫しているか（体感）
- ホットキーキャプチャUIの操作感・衝突時のロールバック挙動
- Quick Key編集・除外アプリ指定の操作感
- 実際の旧 %APPDATA%\Beacon データからの移行（確認ダイアログ・バックアップ・結果）
- 日本語IME・Light/Dark・DPI 100〜200% が設定画面でも破綻しないこと

## 完了条件（PLAN.md Phase R7と同一）
- 旧設定なしの新規起動 / 正常な旧設定からの移行 / 壊れた旧設定からの安全な起動 /
  移行失敗時の旧版継続利用 / ユーザー確認なしで旧データを削除しない
- 設定画面からホットキー・Quick Key・個人化/クリップボードのON/OFF・全リセット・
  除外アプリ・Everything案内・About/Third-party noticesが操作でき、r1-settings.json に永続化される
- R4〜R6の完了条件（DPI・複数モニター・IME・レンダリング挙動・アクリル/枠）に退行がない
- Codexの責任範囲: 自動検証（build/test/NoUiRefs/Portableスモーク）＋起動確認＋
  移行テスト＋設定永続化の確認まで。体感・実旧データ移行の目視はユーザー確認待ちとして列挙

## 失敗時
AGENTS.md「環境の既知制約」該当は記録せず固定ルールに従って切り替える。それ以外は
docs/rebuild/LESSONS.md の記録基準を満たす場合のみ記録してから再試行。SPEC/MIGRATIONと
矛盾する実装を勝手に進めず、理由と案を報告して指示を仰ぐ。旧データを扱う処理は
非破壊・ロールバック可能を最優先にする。

## Git差分要約
Stageごとに変更・追加ファイル一覧と概要を報告する。コミットはユーザーが行う
（Stage完了ごとにコミットを促してよい）。
```
