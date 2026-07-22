# PROMPTS — Codex実装プロンプト

運用: ユーザーが本リポジトリ（`C:\Users\ha.takaku\Desktop\Project\Beacon`）を作業ディレクトリとしてCodexを**対話モードで**実行し、以下のプロンプト本文を渡す（モデル: gpt-5.6-sol）。ヘッドレスの `codex exec` はこの環境ではサンドボックス制限で失敗するため使わない（LESSONS-archive.md 2026-07-16）。
**本ファイルには現行フェーズのプロンプトのみ置く。** R3は2026-07-20完了。R4はR4.1〜R4.3を経てR4.3でGate B承認（2026-07-21ユーザー承認。B-1の枠再発はR4.4での修正を待たずユーザー確認によりクローズ）。R4.4は未着手のまま不要化。統合R5（旧R5・R6・R8。旧R7=第三者プラグイン対応は実装対象外）はStage 1〜3実装後、2026-07-22にGate C承認（PLAN.md参照）。同日「検索レイテンシ改善」保守修正をユーザー検証済み（HotkeyToDisplayMs 240→33.7ms。一部受け入れ項目はR6へ引き継ぎ）。**現行フェーズ = Phase R6（性能・応答性＝レンダリング刷新。2026-07-22承認）**。旧番号R6・R8は歴史的言及で、現行R6は本性能フェーズを指す（PLAN.md 2026-07-22注が正）。

---

## Phase R4.3: Gate B再差し戻し修正（DWM枠の除去とアクリル強化）

前提: R4.2実装済みの現ツリーに対する差分修正。**統合R5より先に完了させる**（本修正のGate B合格後にR5プロンプトへ進む）。仕様値（幅・高さ・角丸・件数等）の変更・新規NuGet依存の追加は禁止。

```
あなたはBeacon（WinUI 3 Portable-firstランチャー）の実装担当。R4.2はGate Bレビューで
再差し戻しになった。原因は静的レビューで特定済みなので、以下の2点を最小差分で修正する。

## B-1: ウィンドウ枠（DWM既定ボーダー線）の除去
- 事実: 現実装は DWMWCP_ROUND（属性33）+ SetBorderAndTitleBar(false,false) だが、
  Windows 11はDWM角丸ウィンドウの外周にシステム既定の1pxボーダー線を描画する。
  これが「ウィンドウの枠」として見えている
- 修正: NativeMethods.SetRoundedCorners と同じ箇所で
  DwmSetWindowAttribute(DWMWA_BORDER_COLOR = 34, DWMWA_COLOR_NONE = 0xFFFFFFFE) を
  設定してDWMボーダーを無効化する（Microsoft公式dwmapiドキュメント記載のAPIのみ）。
  WM_DWMCOMPOSITIONCHANGED での再適用（既存の角丸再適用と同じ場所）も行う
- パネル自身の1px輪郭（LauncherBorderBrush）は参照デザインどおり維持する

## B-2: アクリルの実効化（Thin acrylic + ティント減衰）
- 事実: 現実装は既定の DesktopAcrylicBackdrop（Base種＝不透明寄り）+
  LauncherTintBrush（Light #99FFFFFF / Dark #66000000）の重ねで、
  デスクトップがほぼ透けず「アクリルに見えない」
- 修正:
  1. Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController を使う
     カスタム SystemBackdrop サブクラスを新設し、Kind = DesktopAcrylicKind.Thin を設定する
     （WinAppSDK 1.3+の公開API。Microsoft Learnの「Apply Mica or Acrylic materials」
     カスタムSystemBackdropの実装パターンに従い、SystemBackdropConfiguration・
     テーマ追随・DisposalのHookを正しく実装する）。採用APIの根拠URLを報告する
  2. DesktopAcrylicController.IsSupported() が false の環境は既存の
     DesktopAcrylicBackdrop → ソリッド（LauncherFallbackBrush）の順でフォールバックし、
     どの経路になったかをログへ記録する
  3. LauncherTintBrush のアルファ値をDesignTokens.xaml内で引き下げる
     （目安: Light 白系 20〜30% / Dark 黒系 15〜25%。Thin acrylicと合わせて
     参照画像 docs/検索バー/IMG_4428.jpg の「壁紙が明確に透けて見える」水準へ。
     文字の可読性が崩れる場合のみ範囲内で調整し、採用値と理由を報告する）
- 制約: 直値の色を新規にコードへ書かない（すべてDesignTokens.xamlのトークン経由）。
  アニメーション・IME・検索挙動には触れない

## 既知の残存事項（修正対象外・報告のみ）
- DWM角丸（約8px）とパネル角丸（32px）の差により、4隅にごく小さい
  「ティントなしアクリル領域」が残る。Thin化+ティント減衰後にどの程度目立つかを
  スクリーンショットで報告する（対応要否はGate Bレビューで判断する）

## 検証
1. dotnet build Beacon.sln -c Release で0警告・0エラー
2. dotnet test Beacon.sln -c Release で全テスト成功
3. Test-NoUiReferences.ps1 が0件
4. Build-Portable.ps1 → Test-Portable.ps1（既定）→ -UseActivationPipe
5. Portable展開物で手動確認し、以下のスクリーンショットを撮って報告する:
   - Light/Dark × 未入力/展開 の4枚（柄のある壁紙の上で。壁紙が透けて見えること）
   - ウィンドウ外周に線・枠が見えないこと（明るい壁紙と暗い壁紙の両方で確認）
   - docs/検索バー/IMG_4428.jpg・docs/展開後/ と並べた自己比較コメント
6. 日本語IME・Esc/フォーカス喪失・ホットキー再表示・DPI 100〜200%が
   R4.2から退行していないこと（簡易確認でよい。項目別に報告）
7. スモーク後にBeacon.Next系プロセスが残っていないこと

## 完了条件
- ウィンドウ外周にシステムボーダー線が見えない（パネル自身の1px輪郭のみ）
- 壁紙が明確に透けるアクリル表現になっている（before/after添付）
- 仕様値・依存の変更なし、R4の自動検証がすべてグリーン

## 失敗時・Git差分要約
Phase R4プロンプトと同一（AGENTS.md固定ルール優先。コミットはユーザーが行う）。
```

**結果（2026-07-20レビュー→2026-07-21ユーザー承認）**: B-2（アクリル）は解決。B-1（枠）は静的レビューでは再発指摘（`InitializeLauncher`内の適用順序）していたが、2026-07-21にユーザーが実機確認のうえR4.3をGate B合格と判定。原因究明済みのR4.4修正案（`ApplyWindowFrameStyle`呼び出しをpresenterブロックより後へ移動）は未実施のまま不要化 — 枠の再発が実運用上問題にならないと判断されたため。将来、枠の見え方が再度問題化した場合はこのプロンプト案（原因: `SetBorderAndTitleBar`によるDWMフレーム再計算でボーダー色NONE設定が巻き戻る）をLESSONS.mdの記録基準に従って再利用できる。

---

## 統合Phase R5: 完成フェーズ（デスクトップ統合+プロバイダー/ランキング+Beacon固有UX）プロンプト

前提: R4完了（Gate B承認）と、R4差分のコミット・PR・mainマージが済んでいることをユーザーへ確認してから着手する。実装ブランチは `feature/rebuild-r5-integrated`。本フェーズ後に**Gate C（拡張）**レビュー（実Release ZIPをクリーン環境で確認）がある。

```
あなたはBeacon（WinUI 3 Portable-firstランチャー・独立リポジトリ）の統合Phase R5を担当する。
旧計画のR5・R6・R8を1フェーズに統合したもので、目的は「Gate B済みの検索MVPを、
機能・UIともに日常常用できる完成品へ引き上げる」こと。範囲は広いが品質の妥協は禁止。
第三者プラグイン対応（旧R7 PluginHost本実装）は実装対象外（2026-07-20ユーザー決定）。
必ず下記のStage 1→2→3の順で実装し、各Stage完了時にビルド・テストをグリーンへ
戻してから次へ進む。各Stage完了時に途中報告を出すこと（最後にまとめて報告しない）。

## リポジトリ配置
- 本リポジトリ（作業対象）: C:\Users\ha.takaku\Desktop\Project\Beacon
- Beacon-old（移植元・読み取り専用・変更禁止）: C:\Users\ha.takaku\Desktop\Project\Beacon-old

## 読むべき文書（この順で。読み飛ばし禁止）
1. AGENTS.md（特に「環境の既知制約」の固定ルール）
2. docs/rebuild/LESSONS.md（未解決の失敗のみ）
3. docs/rebuild/SPEC.md §3・§4・§6・**§7（2026-07-20追補。Stage 3の正。§7.7は対象外）**
4. docs/rebuild/adr/ADR-0004（DataRoot）
5. docs/rebuild/ARCHITECTURE.md §4（検索契約）・§5（Windows統合）
6. docs/rebuild/COMPATIBILITY.md §2（標準プラグイン分類。冒頭の対象外注記も読む）
7. docs/rebuild/PLAN.md の Phase R5（Stage構成と完了条件）
8. docs/rebuild/DEPENDENCY_MAP.md B部（依存追加の手続き）

## 共通原則（全Stage）
- 検索の逐次配信・キャンセル・ExecutionToken検証（R2契約）を壊さない
- UI直値禁止: 新しい色・寸法・時間はすべて src/Beacon.WinUI/Resources/DesignTokens.xaml のトークン
- ロジックはBeacon.Core、Windows依存はBeacon.Platform.Windows、描画のみBeacon.WinUI。
  Beacon.ContractsとR1のPluginHostダミー（src/Beacon.PluginHost）は**変更しない**
- 永続データはすべて解決済みDataRoot（exe隣接 Data\）配下。保存先の無言切替禁止
- Beacon-oldからの移植はファイル単位で由来を記録し、Flow Launcher/WoxのMIT表記を維持
  （attribution.mdへ追記。丸ごとコピー禁止）
- 新規NuGetはDEPENDENCY_MAP.md B1記載済みのもの以外、必要性・ライセンス原文確認・
  配布影響をB1へ記載してからでないと追加禁止

## Stage 1: デスクトップ統合と安定化

1. スタートアップ登録（オプトイン）: トレイメニューに「スタートアップに登録」トグルを追加。
   実装はHKCU Run キー（値名は開発中識別子 Beacon.Next）。既定OFF。
   起動時に登録済みパスと実exeパスの不一致（Portableフォルダ移動）を検出したら
   登録を現パスへ更新し、その旨をログへ残す
2. トレイメニュー拡充（R1資産を拡張）: 表示 / スタートアップ登録 /
   クリップボード履歴 ON・OFF（Stage 3接続。それまで無効表示）/
   個人化 ON・OFF・リセット（同上）/ 終了
3. フォーカス・アクティブ化の信頼性: ホットキー10回反復で表示失敗ゼロ。
   他アプリ全画面時・スリープ復帰後・モニター構成変更後・DPI変更後も
   ホットキーとウィンドウ配置（カーソルのあるモニター中央上寄り）が機能し続ける
4. 管理者プロセス境界: 昇格ウィンドウがフォアグラウンドのときの表示可否を実測し、
   OSの制限で不可能な範囲は「制限」として報告する（UIAccessや署名回避は使わない）
5. クラッシュ復旧: UnhandledException / UnobservedTaskException / XAML未処理例外を捕捉し、
   スタックをログへ記録して安全に終了する（無限再起動ループ禁止）。
   次回起動時に前回異常終了を検出したらログに記録する（UIダイアログは最小限）
6. ログローテーション: Data\Logs\ を日付ベースでローテーションし、保持数を超えた分を削除。
   保持数はDataRoot配下の設定ファイルの既定値とする
   （1ログファイル1書き込みプロセスの固定ルール厳守）
7. パフォーマンス計測: 起動→常駐完了 / ホットキー→表示 / 入力→初回結果 の3点を
   計測してログへ記録する仕組みを入れ、実測値を報告に載せる

## Stage 2: 標準プロバイダーとランキング移植

1. 統合ランキングエンジン（Beacon.Core）: SPEC §4スコア表を完全実装
   （完全一致+600 / 前方一致+300 / タイトル一致+150 / 24時間以内実行+120 / 7日以内+60 /
   選択回数最大+180 / 現在アプリ・フォルダ関連 各+80 / Web検索-250）。
   Web fallbackがローカル結果より上に来ないことをテストで固定する
2. 使用履歴ストア: 結果ID・選択回数・最終選択日時・アクティブプロセス名・モードを
   DataRoot配下へJSON保存（SPEC §7.6の個人化データと同一ストア。Stage 3で共用する
   前提で設計する）。書き込み失敗で検索を止めない
3. 遅いプロバイダー隔離: プロバイダーごとの応答期限を設け、遅延プロバイダーが
   UIスレッド・他プロバイダーの結果表示を止めないことをテストで固定する。
   期限超過後に到着した旧セッション結果は表示しない
4. Windows Indexフォールバック: Everything不可時（DLLなし・サービス停止）は
   Windows Search Index経由のファイル検索へ自動フォールバックする
   （src/Beacon.Platform.Windows/WindowsIndexSearch.cs を完成させて接続）。
   どちらの経路を使ったかをログへ記録。エラーを検索結果に混ぜない原則は維持
5. 追加プロバイダー（Beacon-oldから選択的移植。COMPATIBILITY.md §2の分類に従う。
   Cに分類されたPluginsManager / PluginIndicatorは対象外）:
   - WindowsSettings: JSON・RESXデータ駆動部分を移植し、ms-settings URI起動へ接続
   - System操作（旧Sysの再設計）: シャットダウン / 再起動 / スリープ / ロック /
     ごみ箱を空にする 等をActionプロバイダーとして実装。破壊的操作は
     SPEC §7.4の確認フローを通す（Stage 3実装前は暫定確認ダイアログでよい。
     Stage 3完了時に本フローへ差し替える）
   - BrowserBookmark: Chromium系・Firefoxのブックマークローダーを移植（設定UIはR9）
   - Shell: 先頭 `>` でコマンド実行（作業ディレクトリ・履歴は最小限）
   - ProcessKiller: `kill ` プレフィックスでプロセス検索・終了（昇格が必要な対象は
     失敗理由をサブタイトルで表示）
   - Calculator: 内蔵評価器を拡張（べき乗・剰余・パーセント程度）。
     Magesは採用しない（必要と判断した場合もこのフェーズでは追加せず報告のみ）
6. 各プロバイダーのユニットテスト（判定・スコア・キャンセル）を tests/ へ追加

## Stage 3: Beacon固有UX（SPEC §7が正。読み替え禁止。§7.7は対象外）

1. 画面状態機械（Beacon.Core）: Search / Browse / ContextActions / ActionInput /
   Confirmation / Running。**Escは常に1段だけ戻り**、Search未入力でEscは閉じる。
   Running中の再実行ブロック。遷移はユニットテストで固定する
2. Browse Mode: Ctrl+1〜4 = Applications / Files / Actions / Clipboard。
   空欄時初期表示はSPEC §7.2のとおり（Everything全列挙禁止。FilesはWindows Recent）
3. QueryScope・カテゴリチップ: /app /file /folder /action /setting /clipboard +
   日本語エイリアス。Tab確定でQueryScope構造体化しチップ表示（既存チップトークン使用）。
   入力が空のときのBackspaceまたはチップ×で解除。絞り込みはOrchestratorが適用する
4. Actions・Quick Keys: ActionDescriptor / ActionParameter（Text・FilePath・
   FolderPath・Choice）と多段入力→確認→実行のフロー。内蔵アクションv1の10個
   （開く / 保存場所を表示 / パスをコピー / 管理者として実行 / 名前変更 / コピー / 移動 /
   ZIP圧縮 / この場所でターミナル / 既定アプリで開く）。破壊的操作はConfirmation必須。
   Quick Keys既定4種（rf / cp / rn / term）は結果行右端ピルバッジ+ゴースト補完で表示。
   定義はDataRoot配下の設定ファイル既定値（編集UIはR9でやらない）。
   Stage 2の暫定確認ダイアログを本フローへ差し替える
5. クリップボード履歴: SPEC §7.5の表を全項目実装（初期OFF / トレイで有効化 /
   DPAPI CurrentUser暗号化 / 7日・500件 / ハッシュ重複除外 / 除外フォーマット尊重 /
   個別削除・全削除・一時停止）。監視はPlatform.Windowsのサービスに閉じる。
   OFFのとき監視APIを一切呼ばないことをコードで保証する
6. 個人化ランキング: Stage 2のストアを正式接続し、トレイからON・OFF・全リセット。
   OFF時はスコア表の履歴項目を0として扱う。保存しない項目（SPEC §7.6）を厳守
7. UI: カテゴリチップ行・アクション一覧・引数入力・確認・バッジ・ゴースト補完を
   参照デザイン（docs/検索バー・docs/展開後）と同系統のトーンで実装。
   新規の色・寸法・時間はすべてDesignTokens.xamlへトークン追加。
   展開・収縮・状態遷移でウィンドウ上端固定・日本語IME安定（R4の挙動）を壊さない

## 非対象範囲（やらない）
- **Flow互換PluginHost本実装・第三者プラグイン対応一式**（プラグイン検出・JSON-RPC契約・
  IPublicAPI分類・PreviewDataDto契約を含む）。R1のPluginHostダミーは現状維持で触らない
- 設定画面UI（Quick Keys編集・除外アプリ指定・Everything案内画面を含む）→ R7
- 設定・データのLegacy移行 → R7 / 正式識別子切替（Beacon.Next維持）→ Gate D / MSIX → R11
- クリップボード画像対応 / ユーザーテーマ / AI機能 / クラウド送信

## 変更可能ファイル
src/**（ただしBeacon.Contracts・Beacon.PluginHostは変更禁止）, tests/**, Beacon.sln,
src/Beacon.Distribution/**,
docs/rebuild/DEPENDENCY_MAP.md・attribution.md（依存・移植追記）,
docs/rebuild/LESSONS.md（記録基準を満たす失敗時のみ）

## 禁止事項
- `Spotlight` という語（識別子・XAML・コメント・リソースキー・UI文字列・ログ・ファイル名すべて）
- Contracts / Core / Platform.Windows へのWPF・WinUI・WinForms参照 /
  WinUI本体プロセスへのSystem.Windows.*追加
- デリゲート・任意object・UI型のプロセス境界越し送信
- DesignTokens.xaml外への直値の色・寸法・時間
- B1記載外の新規NuGet / iNKORE由来物 / SegoeFluentIcons.ttf / Apple固有素材
- Beacon-old側の変更 / 仕様の独自変更（SPECと矛盾したら実装せず報告）

## ビルド／検証（各Stage末に1〜3、全Stage完了後に全部）
1. dotnet build Beacon.sln -c Release で0警告・0エラー
2. dotnet test Beacon.sln -c Release で全テスト成功
3. Test-NoUiReferences.ps1 がローカルで0件
4. Build-Portable.ps1 → Test-Portable.ps1（既定）→ Test-Portable.ps1 -UseActivationPipe
5. ZIP内: Everything.dll(x64)あり / iNKORE・System.Drawing.Common・WPF系DLLなし
6. 手動確認（Portable展開物で実施し、Stage別に項目ごとの結果を報告。
   不可能な項目は理由付きで未確認と明記）:
   - Stage 1: 再起動後の常駐起動（スタートアップON時）/ ホットキー10回反復 /
     スリープ復帰後・DPI変更後の表示 / ログローテーション動作 / 計測値3点
   - Stage 2: 各プロバイダーの代表検索と実行 / Everything停止時のIndexフォールバック /
     旧版Beaconとの結果比較（大きな欠落がないこと）
   - Stage 3: Esc 1段戻り全遷移 / Ctrl+1〜4 / チップ確定・解除 / 10アクション
     （破壊系の確認ダイアログ含む）/ Quick Keysバッジ・ゴースト補完 /
     クリップボード履歴のON・OFF・削除・暗号化保存の確認 / 個人化リセット /
     日本語IME・Light/Dark・DPI 100〜200%が引き続き正常
7. 長時間確認: 30分常駐+検索反復でメモリ・ハンドルが増え続けないこと（実測値を報告）
8. スモーク後にBeacon.Next系プロセスが残っていないこと
9. 旧WPF版Beaconと並行起動して衝突しないこと（識別子・ホットキー・パイプ名）

## 成果物・完了条件（PLAN.md Phase R5の合算完了条件と同一）
PLAN.md「Phase R5: 統合完成フェーズ」の完了条件3ブロックをすべて満たし、
Stage別の報告（変更ファイル / 完了条件ごとの確認結果 / 仕様と異なる実装とその理由(原則ゼロ) /
追加依存とライセンス確認 / 実行したテスト / 残リスク）を提出する。

## 失敗時
AGENTS.md「環境の既知制約」に該当する失敗は記録せず固定ルールに従って切り替える。
それ以外は docs/rebuild/LESSONS.md の記録基準を満たす場合のみ記録してから再試行。
同じ失敗を繰り返さない。範囲が広いことを理由に品質・検証を省略しない。
どうしても本フェーズ内で完了できない項目が出た場合は、勝手に落とさず
「未完了項目・理由・残作業」を報告して指示を仰ぐ。

## Git差分要約
Stageごとに変更・追加ファイル一覧と概要を報告する。コミットはユーザーが行う
（Stage完了ごとにコミットを促してよい）。
```

**結果（2026-07-22ユーザー承認）**: Stage 1〜3実装済み。Portable ZIPのクリーン環境相当確認を経てGate Cを承認（PLAN.md参照）。以後の変更は新フェーズではなくGate C合格範囲の保守・不具合修正として扱う。

---

## 保守修正: 検索レイテンシ改善（打鍵ラグ）

前提: Gate C合格済みの統合R5コードへの不具合修正。新機能・仕様値変更・UI配色変更は対象外。実機ログ（`Data\Logs\`）とコードレビューで原因を特定済み。既存の自動検証（build/test/Test-NoUiReferences/Portableスモーク）をすべてグリーンに保つこと。

```
あなたはBeacon（WinUI 3 Portable-firstランチャー）の実装担当。統合R5はGate C承認済みだが、
実運用で「入力のたびにラグい」という報告があり、実ログ調査で原因を4点特定した。
最小差分でこの4点のみを修正する（機能追加・リファクタ・仕様値変更はしない）。

## 事実（ログ根拠）
- `Data\Logs\beacon-*.log` で `INFO Everything service is not running. Using Windows Index.`
  がほぼ毎打鍵記録されており、`PERF InputToFirstResultMs` が通常30〜50msのところ
  237〜872msまで跳ねる事例が複数ある
- `WARN Provider windows.bookmarks exceeded 2000ms deadline` を実際に記録済み
  （プロバイダー個別タイムアウト2秒すら超過）
- `PERF HotkeyToDisplayMs` は通常20ms前後だが、240〜275msに跳ねる事例がある

## 修正1: 入力デバウンス
- 対象: src/Beacon.WinUI/MainWindow.R4.cs の OnQueryTextChanged → StartSearch
- 現状: 1打鍵ごとに即 StartSearch → SearchAsync が発火し、10プロバイダーが
  毎回並列起動する（前セッションはキャンセルするが実行中のOS呼び出しは即座には止まらない）
- 修正: 既存の DispatcherQueueTimer（AnimateResize と同様のパターン）を使い、
  80〜120ms程度のデバウンスをテキスト変更に導入する。デバウンス時間は
  DesignTokens.xamlへ新規トークンとして追加する（直値禁止の既存ルール）。
  IME変換中（_composing）は現状どおり検索を発火しない
- Escや確定操作など即時性が必要な既存の分岐（Browse/Actions/Clipboard等）の
  挙動は変えない

## 修正2: WindowsIndexSearchのOleDbConnection使い回し
- 対象: src/Beacon.Platform.Windows/WindowsIndexSearch.cs
- 現状: ExecuteAsync が呼び出しのたびに `new OleDbConnection` → OpenAsync している。
  Search.CollatorDSO のCOM初期化コストが毎回かかり、Everything未導入環境
  （このマシンを含む）での主要な遅延要因になっている
- 修正: 接続を再利用する（インスタンスをFileSearchProvider経由で共有し、
  クラス内に保持した単一OleDbConnectionを開いたままクエリだけ差し替える、
  または軽量なコネクションプール相当の仕組みを導入する）。
  接続が切断された場合の再接続処理を入れる。スレッドセーフ性を確保する
  （同時に複数クエリが来ても壊れないこと。QueryOrchestratorは複数プロバイダーを
  並列実行するため、既存のFileSearchProvider経由の呼び出しが直列であることを
  確認したうえで設計する）

## 修正3: BrowserBookmarkProviderの先読み
- 対象: src/Beacon.Platform.Windows/StandardSearchProviders.cs の BrowserBookmarkProvider
- 現状: `Lazy<Task<Bookmark[]>>` が初回のブックマーク系クエリで初めて起動され、
  全ブラウザのブックマーク読込がその場でブロックしタイムアウトを超過する
- 修正: ランチャー表示時（ShowLauncherCore内、既存の RefreshAppCacheAsync と
  同様の「表示をブロックしないバックグラウンド先読み」パターン）に
  bookmarks の Lazy を先行トリガーする。検索時に間に合わなければ従来どおり待つ

## 修正4: ExplorerPathServiceの非同期化
- 対象: src/Beacon.WinUI/MainWindow.R4.cs の ShowLauncherCore
- 現状: ExplorerPathService.GetCurrentPath()（Shell.Application動的COM生成+
  ウィンドウ列挙）がウィンドウ表示前に同期実行されており、ホットキー→表示の
  クリティカルパスに乗っている
- 修正: ウィンドウ表示（ShowWindow/Activate）より後に非同期取得へ変更する。
  _activeFolder はアクション実行時（現在フォルダでの操作）にのみ参照されるため、
  表示をブロックする必要はない。取得中に検索・実行が行われた場合の
  null/未取得ハンドリングを既存の呼び出し箇所（_activeFolder参照箇所）で確認する

## 制約
- 新規NuGet依存の追加禁止
- DesignTokens.xaml外への直値禁止（デバウンス時間を含む）
- SPEC.mdの検索契約・ランキング・キャンセル仕様は変更しない
- Beacon.Contracts / Beacon.PluginHostは変更禁止

## 検証
1. dotnet build Beacon.sln -c Release で0警告・0エラー
2. dotnet test Beacon.sln -c Release で全テスト成功
3. Test-NoUiReferences.ps1 が0件
4. Build-Portable.ps1 → Test-Portable.ps1（既定）→ -UseActivationPipe
5. Portable展開物で手動確認し、`Data\Logs\` の実測値を報告する:
   - 修正前後の PERF InputToFirstResultMs（Everything未導入環境で連続10打鍵分）
   - 修正前後の PERF HotkeyToDisplayMs（ホットキー10回反復）
   - WARN Provider ... exceeded deadline が発生しないこと
6. 日本語IME変換中に検索が発火しないこと（デバウンス導入後も回帰しないこと）
7. Everything導入環境・未導入環境の両方でファイル検索結果が従来どおり得られること
8. スモーク後にBeacon.Next系プロセスが残っていないこと

## 完了条件
- Everything未導入環境で連続入力時の体感ラグが解消したことを実測値で示す
- windows.bookmarksのデッドライン超過ログが発生しない
- HotkeyToDisplayMsの異常値（200ms超）が解消する
- 既存の自動検証・手動確認項目に退行がない

## 失敗時
AGENTS.md「環境の既知制約」に該当する失敗は記録せず固定ルールに従って切り替える。
それ以外は docs/rebuild/LESSONS.md の記録基準を満たす場合のみ記録してから再試行。

## Git差分要約
変更・追加ファイル一覧と概要、修正前後のPERF実測値を報告する。
コミットはユーザーが行う。
```

**結果（2026-07-22ユーザー検証）**: Release build 0警告0エラー / 全テスト66件成功 / Test-NoUiReferences 0件 / Build-Portable成功 / Portable既定スモーク・Activation Pipeスモーク成功 / 残存Beaconプロセスなし / スモークログにERROR・Exception・Providerデッドライン超過なし。`HotkeyToDisplayMs`=修正前240〜275ms→最終スモーク33.7ms。追加依存・仕様差異なし。**未実施（R6の受け入れへ引き継ぐ）**: Portable上での連続10打鍵・ホットキー10回の手動採取 / 日本語IME目視 / Everything導入環境での確認。よって `InputToFirstResultMs` の修正後10件実測（修正前: 正常30〜50ms・異常237〜872ms）とIME・両Everything環境の完了確認は未了。本修正は**入力レイテンシ**の解消であり、レンダリング（毎フレームのHWNDリサイズ＝R6根本原因A〜D）は対象外。

---

## Phase R6: 性能・応答性の作り込み（レンダリング刷新）

前提: R6はPLAN.mdで承認済み（2026-07-22）。上記「検索レイテンシ改善」修正が**mainにマージ済み**であることをユーザーへ確認してから着手する（本フェーズはその上に積む）。実装ブランチは `feature/rebuild-r6-rendering`。**機能・仕様値・UI配色・R4.3で確定したアクリル/枠の見え方（Gate B視覚）を変えない**純粋な品質フェーズ。本フェーズ後に軽量レビュー（Gate Dの前提）がある。仕様値変更・新規NuGet依存の追加は禁止。

```
あなたはBeacon（WinUI 3 Portable-firstランチャー・独立リポジトリ）のPhase R6を担当する。
目的は「旧WPF版と同等以上の体感速度にする」こと。入力レイテンシ（HotkeyToDisplayMs /
InputToFirstResultMs）は先行の保守修正で解消済み（HotkeyToDisplayMs 240→33.7ms）。
本フェーズが残す課題は【レンダリング／アニメーション経路】＝結果欄の展開・収縮や結果
ストリーミング中の「重い・カクつく」感で、原因はPLAN.md Phase R6の根本原因A〜Dにある。
機能追加・リファクタ・仕様変更はしない。各Stage完了時に途中報告を出すこと。

## リポジトリ配置
- 作業対象: C:\Users\ha.takaku\Desktop\Project\Beacon
- Beacon-old（参照専用・変更禁止）: C:\Users\ha.takaku\Desktop\Project\Beacon-old

## 読むべき文書（この順で。読み飛ばし禁止）
1. AGENTS.md（特に「環境の既知制約」の固定ルール）
2. docs/rebuild/LESSONS.md（未解決の失敗のみ）
3. docs/rebuild/PLAN.md 「Phase R6」（根本原因A〜Gと作業・完了条件。本プロンプトの正）
4. docs/rebuild/PROMPTS.md の「Phase R4.3」結果注記（アクリル・DWM枠の確定仕様。壊さないため）
5. docs/rebuild/SPEC.md §3.3（デザイン値集約）・§6（Windows統合）
6. 現ツリー: src/Beacon.WinUI/MainWindow.R4.cs（AnimateResize / ResizeForResults /
   ApplyResults / SearchAsync）, NativeWindowController.cs（ApplyRoundedRegion /
   ApplyWindowFrameStyle）, ThinDesktopAcrylicBackdrop.cs, Beacon.WinUI.csproj

## 前提知識（重要な制約）
- 現設計は SystemBackdrop（Thinアクリル）が【ウィンドウ全面】を覆う。収縮時にピル形状を
  保つためウィンドウ自体を結果数に応じてリサイズしている。よって「固定ウィンドウ＋内側だけ
  アニメ」への単純移行はアクリルが全面に広がり見た目が崩れる。方式を変える場合はこの制約を
  必ず検討し、R4.3で確定したアクリル/枠/角丸の見え方を退行させないこと
- HotkeyToDisplayMs は現状33.7ms。これを退行させない（悪化させる変更は不可）

## Stage 1: 計測と範囲確定（先に事実を取る。いきなり書き換えない）
1. 展開・収縮1回あたりの AppWindow.Resize 呼び出し回数、SetWindowRgn 呼び出し回数、
   1クエリ（例:5文字入力し8件ヒット）中の ResizeForResults 発火回数を計測ログに出す
2. 展開・収縮・結果ストリーミング中のフレーム時間（1フレームの所要ms、ドロップ有無）を
   計測する手段を用意し、現状値を記録する
3. 上記からA〜Dのうち体感カクつきへの寄与が大きいものを特定し、Stage 2以降の範囲を
   データで確定して報告する（「計測上すでに滑らかで対処不要」ならその旨を数値で示し、
   該当Stageを実施しない判断も可。憶測で大改修しない）

## Stage 2: 低リスク修正（Aの churn を減らす。視覚を変えない）
- D（逐次結果ごとのリサイズ連打）の解消: SearchAsync が結果1件受信ごとに
  ResizeForResults→AnimateResize を再起動している。可視件数が確定するまで
  リサイズを遅延・集約し、1クエリあたりのウィンドウリサイズを最小回数（理想は
  「件数が変わったときだけ」）にする。逐次の結果【内容】反映（ApplyResults）は
  従来どおり即時でよい
- C（SetWindowRgn の毎フレーム実行）の削減: AnimateResize の各フレームで
  CreateRoundRectRgn/SetWindowRgn/DeleteObject を実行している。角丸クリップを
  アニメーション毎フレームで作り直さずに済む方法（例: 目標サイズ確定時に一度だけ設定、
  またはXAMLのクリップ/角丸で代替しウィンドウリージョンを廃止できるか）を検討し、
  R4.3の角丸・枠の見え方を保ったまま毎フレームのGDI呼び出しを無くす
- アイコン解決（ResolveIconAsync）を行ごとの逐次ディスパッチからバッチ化・オフスレッド化し、
  ストリーミング中のUIスレッド競合を減らす
- 以上はいずれも仕様値・配色・レイアウトを変えない範囲で行う

## Stage 3: アニメーション経路（Stage 1の計測でまだカクつく場合のみ）
- B（UIスレッドの DispatcherQueueTimer による手動イージング）を、描画スレッドで動く
  WinUI Composition/暗黙アニメーションへ寄せられるか検討する。ただし前述のアクリル全面
  制約により、ウィンドウ寸法アニメ自体をやめて内側コンテンツのアニメへ切り替える方式は
  見た目の作り込みに影響しうる。【この方式変更に踏み込む場合は、実装前に「採用方式・
  アクリル/枠/角丸への影響・before/afterの想定」をユーザーへ提示して承認を得ること】
  （Gate Bで確定した視覚を勝手に変えない）

## Stage 4: 起動最適化（Stage 1で初回/コールド表示が重いと確認できた場合のみ）
- E: Beacon.WinUI.csproj に PublishReadyToRun を有効化し、コールドスタート時間と
  Portable ZIPサイズ・クリーン起動への影響を計測する。効果が薄い/ZIPが過大なら
  理由を記録して不採用にしてよい（Microsoft公式ドキュメントで挙動を確認して採否判断）
- F: MainWindow コンストラクタの10プロバイダー+オーケストレーター構築のうち、
  表示クリティカルパス外へ安全に移せる初期化を遅延/並列化する（表示・入力の順序と
  スレッド安全性を壊さない範囲で。無理なら現状維持で報告）

## 制約
- 機能追加・仕様値変更・UI配色変更・R4.3のアクリル/枠/角丸の見え方の変更をしない
- 新規NuGet依存の追加禁止 / DesignTokens.xaml外への直値（色・寸法・時間）禁止
- Beacon.Contracts / Beacon.PluginHost は変更禁止 /
  Core・Platform.Windows へのWPF・WinUI・WinForms参照禁止
- `Spotlight` の語をいかなる形でも書かない
- SPECの検索契約・ランキング・キャンセル・IME挙動を壊さない

## 検証
1. dotnet build Beacon.sln -c Release で0警告・0エラー
2. dotnet test Beacon.sln -c Release で全テスト成功
3. Test-NoUiReferences.ps1 が0件
4. Build-Portable.ps1 → Test-Portable.ps1（既定）→ -UseActivationPipe
5. ZIP内: Everything.dll(x64)あり / iNKORE・System.Drawing.Common・WPF系DLLなし
   （PublishReadyToRun採用時はZIPサイズの前後差も報告）
6. 起動確認（Codexが実施。ここまでがCodexの検証範囲）: Portable展開物を起動し、
   常駐→ホットキーでウィンドウ表示→数回検索→終了までを一度通す。
   Data\Logs\ に ERROR / Exception / Providerデッドライン超過が出ていないこと、
   終了後にBeacon.Next系プロセスが残らないことを確認する。ログに自動出力される値と、
   本フェーズで追加した計測値を報告する:
   - HotkeyToDisplayMs（起動確認中に出た値。33.7ms水準を退行させていないこと）
   - InputToFirstResultMs（同上）
   - 1クエリあたりのウィンドウリサイズ / SetWindowRgn 呼び出し回数（Stage 2の前後比較）

## ユーザーが目視・手動で確認する（Codexは実施不要）
体感・見た目・特定環境を要するため、以下はユーザーが確認する。**Codexは自分で判定せず、
これらを「ユーザー確認待ち項目」として報告に列挙するだけでよい**（起動確認までで手を止める）:
- 展開・収縮・ストリーミングが旧WPF版と同等以上に滑らかか（体感）
- 入力1文字ごとにウィンドウ寸法が伸縮しないこと（目視）
- 日本語IMEの入力・変換・確定
- Light/Dark・DPI 100〜200%・複数モニター
- R4.3のアクリル/枠/角丸の見え方が変わっていないこと
- Everything導入環境でのファイル検索
- HotkeyToDisplayMs / InputToFirstResultMs の連続10回手動採取

## 完了条件（PLAN.md Phase R6と同一。体感・見た目の判定はユーザー目視による）
- 入力1文字ごとにウィンドウ寸法が伸縮しない（結果が確定したときのみ変化）
- ホットキー→表示・入力→初結果・結果ストリーミング中のフレーム落ちが旧版と同等以上
  （体感で引っかからない。Codexは計測値を出し、滑らかさの最終判定はユーザーが行う）
- R4完了条件（DPI・複数モニター・IME）と機能・デザインに退行がない
- Codexの責任範囲: 自動検証（build/test/NoUiRefs/Portableスモーク）＋起動確認＋計測値報告まで

## 失敗時
AGENTS.md「環境の既知制約」該当は記録せず固定ルールに従って切り替える。それ以外は
docs/rebuild/LESSONS.md の記録基準を満たす場合のみ記録してから再試行。計測で対処不要と
出た項目を無理に改修しない。方式変更が必要と判断したら勝手に進めず理由と案を報告して指示を仰ぐ。

## Git差分要約
Stageごとに変更・追加ファイル一覧と概要、修正前後のPERF/フレーム実測値を報告する。
コミットはユーザーが行う（Stage完了ごとにコミットを促してよい）。
```
