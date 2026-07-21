# PROMPTS — Codex実装プロンプト

運用: ユーザーが本リポジトリ（`C:\Users\ha.takaku\Desktop\Project\Beacon`）を作業ディレクトリとしてCodexを**対話モードで**実行し、以下のプロンプト本文を渡す（モデル: gpt-5.6-sol）。ヘッドレスの `codex exec` はこの環境ではサンドボックス制限で失敗するため使わない（LESSONS-archive.md 2026-07-16）。
**本ファイルには現行フェーズのプロンプトのみ置く。** 現行は **統合R5**（旧R5・R6・R8の統合。旧R7=第三者プラグイン対応は実装対象外。2026-07-20ユーザー決定）。R3は2026-07-20完了。R4はR4.1〜R4.3を経てR4.3でGate B承認（2026-07-21ユーザー承認。B-1の枠再発はR4.4での修正を待たずユーザー確認によりクローズ）。R4.4は未着手のまま不要化。完了フェーズのプロンプト本文は削除済み。

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
mm
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
- 設定画面UI（Quick Keys編集・除外アプリ指定・Everything案内画面を含む）→ R9
- 設定・データのLegacy移行 → R9 / 正式識別子切替（Beacon.Next維持）→ Gate D / MSIX → R11
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
