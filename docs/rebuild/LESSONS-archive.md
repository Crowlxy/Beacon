# 失敗記録簿アーカイブ（LESSONS-archive）

クローズ済み（再発防止をスクリプト・PROMPTS・AGENTS等へ反映済み、または一過性で完了）のエントリ置き場。**必読ではない**。参照するのは (1) 新規記録前の同一原因検索、(2) 類似事象の調査時のみ。機械反映できない一般教訓は [LESSONS.md](LESSONS.md) 冒頭の「有効な教訓」に一行要約がある。

---

## 2026-07-20: DevHarnessのStart Menu権限とnative DLL配置
- 事象: アプリ検索がアクセス拒否サブディレクトリで全体クラッシュし、Everything.dllは出力のx64サブフォルダにありロードできなかった。
- 原因: SearchOption.AllDirectoriesが部分的な権限拒否を許容せず、MSBuild None itemの相対配置を出力ルートと誤認した。
- 再発防止: Start Menuはディレクトリ単位でUnauthorizedAccess/IOをスキップし、Everything.dllはTargetPathで出力ルートへ固定する。Harnessのあり/なし実行をR3検証に維持する。

## 2026-07-20: R3初回コンパイルで生成P/Invoke境界の型不一致
- 再発: 2026-07-20（名前空間行への曖昧なpatch contextでusingがファイル末尾へ入った。using追加は先頭の既存usingを明示contextにする）
- 事象: LibraryImportのStringBuilder、CsWin32 HWND.Value、dynamic例外型、予約例外のanalyzerで6エラー。
- 原因: source-generated P/Invokeが要求するchar buffer/unsafe境界と生成型を実シグネチャで確認せず移植した。
- 再発防止: 文字出力は固定char buffer、CsWin32ポインタ値は局所unsafe cast、COM dynamic例外は完全修飾、アプリ例外はInvalidOperationExceptionを使う（Platformへ反映）。

## 2026-07-20: Windows TFMの最小版不一致でNU1201
- 事象: Platformをnet9.0-windows10.0.19041.0、Harness/Testsをnet9.0-windowsの既定7.0にして参照互換性エラーになった。
- 原因: Windows固有プロジェクト間でTargetPlatformVersionを統一していなかった。
- 再発防止: Platformを参照するHarness/Testsは同じnet9.0-windows10.0.19041.0へ固定する（各csprojへ反映）。

## 2026-07-20: Phase R3必読ADRのファイル名推測で読取失敗
- 再発: 2026-07-20（外側apply_patchへの大規模引数でC#補間文字列をPowerShellが解釈しParserError。以後は外側実行を補間を含まない小パッチに限定する）
- 再発: 2026-07-20（中央パッケージ管理ファイルの有無を列挙せず Directory.Packages.props を読んだ。以後は rg --files の実在一覧のみを入力にする）
- 事象: docs/rebuild/adr の短縮ファイル名を指定し、実在しないパスとして読取に失敗した。続く列挙はサンドボックス補助プロセス不在でも失敗した。
- 原因: ADRディレクトリの実名を列挙せず短縮名を推測した。サンドボックス障害は既存の環境制約。
- 再発防止: ADRはディレクトリ列挙後の実在名だけを読み、サンドボックス起動障害時は承認済みの個別読取へ切り替える（AGENTS.md「環境の既知制約」へ集約済み）。

## 2026-07-17: QueryOrchestratorの非キャンセル待機でCA2016

- 事象: Phase R2のReleaseビルドで`WaitToReadAsync()`がCA2016により1エラーとなった。
- 原因: 旧セッションを例外終了させずproducerのchannel完了まで読む設計で、意図的な`CancellationToken.None`を明示していなかった。
- 再発防止: 非キャンセル待機には`CancellationToken.None`を明示し、Releaseビルドのanalyzerを実装チェックとして維持（Coreへ反映済み）。

## 2026-07-17: Coreテストの固定期待値配列でCA1861

- 事象: CA2016修正後のReleaseビルドで、到着順テストの`new[] { "fast", "slow" }`がCA1861により1エラーとなった。
- 原因: 繰り返し評価されるNUnit制約へ固定配列を直接渡した。
- 再発防止: 少数要素の順序確認は個別assertとし、不要なstatic配列を追加しない（Core.Testsへ反映済み）。

## 2026-07-17: 別プロセスからの同一ログファイルappendで行破損

- 事象: セカンダリインスタンスの診断ログをプライマリと同じbeacon.logへ書いたところ、プライマリの`displayed the AppWindow`行が`Window`だけの破損行になり、スモークのマーカー判定が失敗した。
- 原因: .NETの`FileMode.Append`はオープン時にend-of-fileへシークするだけでアトミックappendではなく、複数プロセスの同時書き込みで互いの行を上書きする。
- 再発防止: 1ログファイルにつき書き込みプロセスは1つに限定する。セカンダリはbeacon-secondary.logへ分離（反映済み）。プロセス間で共有するログが必要になったら、その時点でappendの原子性を保証する方式を設計する。

## 2026-07-17: スモークの持ち越しログ誤マッチでMutex獲得レース

- 事象: GitHub Actions windows-latest（run 29561124234）で `Test-Portable.ps1 -UseActivationPipe` がsmoke-b段階の「Second Beacon.Next instance did not exit within 15 seconds.」で失敗。ローカル再現でも同一失敗。
- 原因: フォルダ移動でsmoke-aのbeacon.logがsmoke-bへ持ち越され、smoke-bの登録待ちが古いマーカーへ即マッチ。1本目のMutex獲得前にactivationインスタンスが起動し、レースに勝ったactivation側がプライマリとして常駐した。ログ蓄積型のマーカー待ちは「今回の起動分」だけを見ないと成立しない。
- 再発防止: Test-Beaconがフェーズ開始時に持ち越しログを削除（削除前に artifacts\logs\ へ保全、スクリプトへ反映済み）。セカンダリパスの `Secondary instance signaled=` ログとフェーズ別タイムスタンプ出力を恒久的な診断手段として維持。今後、ログ出現待ちで進行判定するテストは、フェーズ跨ぎでログを初期化するか出現位置（オフセット）で判定する。

## 2026-07-17: WPF RuntimePack除去で参照アセンブリまで無効化

- 再発: 2026-07-17（`DisableTransitiveFrameworkReferenceDownloads` は未導入RuntimePackの取得抑止であり、導入済みSDK環境のコピーは継続した。診断ログの推測パス検索も0件で終了。続く複数ファイルパッチは2ファイル目の更新ヘッダー欠落で拒否されたため、binlogで確認済みのitem名と明示的なファイル境界へ切替）
- 再発: 2026-07-17（`ResolveRuntimePackAssets` 直前のFrameworkReference除去もResolveAssemblyReferenceより先に作用してMSB3277が再発。FrameworkReferenceは維持し、解決後のRuntimePackコピー項目だけをパッケージIDで除去する）
- 再発: 2026-07-17（RuntimePackコピー項目の除去条件で未修飾の `%(NuGetPackageId)` を使い、当該metadataを持たない項目でMSB4096。item名付きmetadataへ修正）
- 再発: 2026-07-17（WindowsDesktop RuntimePackとdeps参照は消えたが、.NETCore RuntimePack自身の互換facade `System.Windows.dll` / `WindowsBase.dll` が監査パターンに2件残った。ファイル名を限定してコピー項目から除外し、起動スモークで検証する）
- 再発: 2026-07-17（2 facadeは `RuntimePackAsset` / `ReferenceCopyLocalPaths` 除去後も最終publish一覧へ再生成され、監査2件が残った。Build-Portableと同じself-contained条件の診断ログで最終コピーitemを特定する。補助的なdeps資産件数集計もtarget選択を誤ってnullエラーになったため、報告根拠にはdepsライブラリ一覧と実ファイル一覧だけを用いる）
- 再発: 2026-07-17（self-contained診断で2 facadeはWinUIではなくPluginHostの.NETCore RuntimePackから配布マージ時に再流入すると判明。WinUI側のWindowsDesktop除去と、Distribution側の厳密な2ファイル除外を分離する）
- 事象: `DisableTransitiveFrameworkReferences=true` でWPF配布DLLは除去できたが、`Microsoft.VisualStudio.Threading.dll` が要求するWindowsBase 8.0と.NETCore側WindowsBase 4.0のMSB3277競合警告が発生した。
- 原因: 配布RuntimePackだけでなく、推移的FrameworkReferenceのコンパイル参照も一括無効化した。
- 再発防止: binlogで分けて確認したコンパイル参照とRuntimePackコピーを同一設定で除去しない。SDK標準の `DisableTransitiveFrameworkReferenceDownloads` でRuntimePack側だけを抑止し、build警告とZIP内容の両方を検証する。

## 2026-07-17: R1残課題の初回調査が環境・パス・パッチ引数で失敗

- 再発: 2026-07-17（Phase R2のRPC追随でPowerShell複数行Replaceが改行表現不一致により一部未反映。直後の検索で検出し、全文置換または狭い正規表現へ切替）
- 再発: 2026-07-17（Phase R2のapply_patchもfs sandbox helperの`CreateProcessWithLogonW failed: 5`で起動不能。承認済み外側実行で検証済み内容をファイル単位に反映）
- 再発: 2026-07-17（Phase R2のARCHITECTURE節抽出でも`CreateProcessWithLogonW failed: 5`。以後の必読文書は必要に応じて承認済み個別読取で継続）
- 再発: 2026-07-17（Phase R2の必読文書を一括読取した際に再び`CreateProcessWithLogonW failed: 5`。既存の再発防止どおり、承認済みの個別読取へ切替）
- 再発: 2026-07-17（中断復帰確認で複数PowerShellの同時起動が再びCreateProcessWithLogonW failed: 5。さらにADR-0003の実名列挙前にパスを推測した。以後は個別実行し、ディレクトリ列挙後の実在パスだけを読む）
- 再発: 2026-07-17（必読文書の一括読取と組み込みパッチが `CreateProcessWithLogonW failed: 5`、未列挙の `scripts` パス指定が不在、並列読取がtimeout、診断logger引数のセミコロンがPowerShellの文区切りとして解釈された。承認済み個別読取・実在パス・十分なtimeout・最小publishコマンドへ切替）
- 事象: 初回の読み取りコマンドと組み込みパッチが `windows sandbox: CreateProcessWithLogonW failed: 5` で失敗した。昇格後は存在しないADR名を指定し、残存プロセスなしを終了コード1として扱った。さらにバッチラッパーへ複数行パッチを渡し `The last line of the patch must be '*** End Patch'` で拒否された。
- 原因: 既知のWindowsサンドボックス起動拒否に加え、実名確認前のパス推測、プロセス不在の未正規化、バッチ経由で複数行引数が崩れる既知制約が重なった。
- 再発防止: 必要最小限の昇格実行を使い、列挙済みの実在パスだけを読む。プロセス不在は正常終了として扱い、複数行パッチはバッチを介さずCodex実体へ単一引数で渡す（AGENTS.md「環境の既知制約」へ集約済み）。

## 2026-07-17: XBF修正後の再スモークでホットキー送信の競合とRPC結果未着を確認

- 事象: XBF同梱ZIP（16:24再ビルド）の再スモークで、XAML初期化〜ホットキー/トレイ登録〜RPC spike開始までログが揃ったが、`Hotkey or activation pipe` と `RPC incremental result` が15秒以内に出ず失敗した。ERRORログはなし。
- 原因: (1) Test-Portable.ps1がログファイル出現直後にホットキーを送信しており、`Hotkey and tray registered`（約150ms後）より早い。前日の再発防止「登録ログを待ってから送る」がスクリプトへ未反映だった。(2) RPC側は未確定だが、サーバーが`NotifyWithParameterObjectAsync`で名前付きパラメータ送信するのに対し、受信側`OnSearchResult(SearchResultDto result)`に`UseSingleObjectParameterDeserialization = true`がなく、バインド失敗で通知が黙って破棄されている可能性が高い。
- 再発防止: スモークは`Hotkey and tray registered`のログ行を確認してからホットキーを送る。StreamJsonRpcでDTO1個を通知する場合は受信側`[JsonRpcMethod]`に`UseSingleObjectParameterDeserialization = true`を必ず付ける（Test-Portable.ps1とR2契約へ反映済み）。

## 2026-07-16: WinUI生成中間物のobjパスを推測して検索した

- 事象: PRI/XBF診断のSelect-Stringが、存在しないsrc/Beacon.WinUI/obj/x64/Releaseを指定して失敗した。
- 原因: binのPlatform付き配置からobjも同じ階層と推測し、実列挙を先にしなかった。
- 再発防止: SDK生成物はobj配下をGet-ChildItemで実列挙し、得られた実パスに対して検索する。

## 2026-07-16: 初回PortableスモークでホットキーとRPCのログが出なかった

- 事象: Release ZIPの展開起動でBeacon.Next startedだけが記録され、ホットキー受信とRPC結果のログが15秒以内に出ず、Test-Portable.ps1がGlobal hotkey was not observedで失敗した。
- 原因: チェックポイント追加後、MainWindow.InitializeComponentで ms-appx:///MainWindow.xaml を解決できないことを確認した。App.xbf/MainWindow.xbfはobjへ生成されたがpublish出力へコピーされず、exe隣接へ仮配置するとXAML、AppWindow、ホットキー、トレイ登録まで成功した。
- 再発防止: XBFをMSBuildのBuild/Publish後ターゲットで出力へコピーする。MainWindowとWin32登録完了をログへ明示し、スモークはそのログを待ってからホットキーを送る。
- 再発: 2026-07-17（「登録ログを待ってからホットキー送信」がTest-Portable.ps1へ未反映だった。LESSONS.md 2026-07-17参照）

## 2026-07-16: App.DisposeにGC.SuppressFinalizeが不足した

- 事象: 6回目のdotnet buildでCA1816だけが残った。
- 原因: 非sealedのAppへIDisposableを実装した際、将来の派生型を含む標準Dispose契約のGC.SuppressFinalizeを省略した。
- 再発防止: 非sealed型へIDisposableを実装する場合は所有資源破棄後にGC.SuppressFinalizeを呼ぶ。

## 2026-07-16: MainWindowのIDisposable化でApp側CA1001が顕在化した

- 事象: 5回目のdotnet buildでAppが破棄可能なMainWindowを所有するCA1001だけが残った。
- 原因: MainWindowの所有契約を正した一方、Application.Start終了後にProgramからAppを破棄するライフタイムを実装していなかった。
- 再発防止: ネイティブ資源はProgram、App、Windowの所有鎖全体で破棄経路を作り、解析規則の局所抑制で隠さない。

## 2026-07-16: WinUIビルドがCA1806とCA1001をエラー化した

- 事象: 4回目のdotnet buildはコンパイルを通過したが、MessageBoxW戻り値未使用のCA1806と、MainWindowがNativeWindowControllerを破棄可能フィールドとして所有するCA1001で失敗した。
- 原因: Directory.Build.propsのlatest-recommendedとTreatWarningsAsErrorsに対し、Win32戻り値の意図的破棄と所有者のIDisposable契約を明示していなかった。
- 再発防止: 戻り値を使わないWin32 APIは明示的にdiscardし、破棄可能なネイティブ資源を所有する型はIDisposableとして終了経路から一度だけ破棄する。

## 2026-07-16: Application.Startのラムダ引数名をdiscardと誤認した

- 事象: 3回目のdotnet buildでProgram.csのApp生成行がCS0029となり、XAML Pass2も連鎖失敗した。
- 原因: ラムダ引数をアンダースコアという実変数名にしたスコープ内で、App生成の左辺もdiscardのつもりでアンダースコアにしたため、ApplicationInitializationCallbackParamsへの代入として解釈された。
- 再発防止: Application.Startのラムダ引数には意味のある名前を付け、生成インスタンスの保持不要を表すdiscardと同名にしない。

## 2026-07-16: Mutex名のバックスラッシュがパッチ送信時に1段失われた

- 事象: 2回目のdotnet buildでSingleInstanceCoordinator.csのMutex名がCS1009となり、WinUI XAML Pass2もWMC9999で連鎖失敗した。
- 原因: JavaScriptテンプレートリテラルを経由したパッチでC#文字列の二重バックスラッシュが単一になった。
- 再発防止: Windowsオブジェクト名・パスをC#へ書く場合はverbatim文字列を使い、送信段階のエスケープ数に依存させない。構文エラー解消後にXAMLエラーの再現有無を判定する。

## 2026-07-16: R1初回ビルドでNUnit名前空間とStreamJsonRpc通知APIを誤った

- 事象: dotnet build Beacon.sln が11エラー。テスト属性10件を解決できず、NotifyWithParameterObjectAsyncの第3引数へCancellationTokenを渡して型不一致になった。
- 原因: NUnit.Frameworkのusingを明示せず、StreamJsonRpc 2.25.29の通知オーバーロードを確認せず旧想定で記述した。
- 再発防止: NUnitテストファイルはNUnit.Frameworkを明示する。逐次通知のキャンセルは検索ループのTask.Delayで受け、通知APIには実在する2引数オーバーロードだけを使う。

## 2026-07-16: PowerShell行継続記号がパッチ送信用JavaScriptと衝突した

- 事象: テスト・配布スクリプト追加パッチが JavaScriptの SyntaxError: Unexpected identifier '$Configuration' でシェル実行前に停止した。
- 原因: PowerShellのバッククォート行継続を、JavaScriptテンプレートリテラル内へ無加工で含めた。
- 再発防止: パッチで追加するPowerShellコマンドは引数配列で組み立て、行継続記号を使わない。パッチ本文にバッククォートがないことを送信前に確認する。

## 2026-07-16: R1初回パッチ送信でJavaScriptのbtoaが未定義だった

- 事象: ソリューション初回追加パッチをBase64化するオーケストレーションが ReferenceError: btoa is not defined でシェル実行前に停止し、教訓追記の初回JavaScriptもテンプレートリテラル内のバッククォートで構文エラーになった。
- 原因: ツールのJavaScript isolateにブラウザー組み込みのbtoaが存在すると誤認し、さらにパッチ本文をJavaScriptテンプレートリテラルへ無加工で埋めた。
- 再発防止: パッチ本文はバッククォートを含まない文字列配列から組み立て、PowerShellのliteral here-stringを経由してCodex apply-patchモードへArgumentListで直接渡す。Base64変換は使わない。

## 2026-07-16: R1文書確認と教訓追記でサンドボックス起動拒否が発生

- 事象: 文書確認コマンドが `windows sandbox: CreateProcessWithLogonW failed: 5` で実行前に2回失敗し、教訓追記用の組み込み`apply_patch`も同じ理由で3回失敗した。バッチラッパー経由の2回は複数行引数が崩れ、末尾行エラーになった。
- 原因: 本環境で既知のWindowsサンドボックスによる補助プロセス起動拒否と、Windowsバッチ経由で複数行パッチ引数を渡せない制約。
- 再発防止: このセッションでは既定サンドボックスの再試行をやめ、読み取り・ビルドも必要最小限の昇格実行に切り替える。組み込み`apply_patch`が継続失敗する場合は、バッチを介さずCodexのapply-patchモードへArgumentListでパッチを渡す。
- 再発: 2026-07-17（初回調査で再びサンドボックス起動拒否とバッチ経由パッチ崩れ。LESSONS.md 2026-07-17参照）

## 2026-07-16: Localization生成メソッドのIL呼出先解決が終了コード1になった

- 事象: 生成された`Localize`メソッドのILから呼出先を解決する調査が、文字列キー取得後に終了コード1となった。
- 原因: ReleaseアセンブリをPowerShellへ直接ロードした状態で、依存解決を含むILトークン解決を行ったため。詳細は未確定。
- 再発防止: R0では反射で確認できる型名・公開シグネチャとパッケージpropsを証拠範囲とし、WinUI実利用可否はR1の最小プロジェクト実験へ明示的に残す。目的外の簡易IL解析は行わない。

## 2026-07-16: Localization生成型の反射確認で空パイプ構文エラーを起こした

- 事象: `foreach`ブロックの出力を直後に`Format-Table`へ渡す監査コマンドが `An empty pipe element is not allowed` で解析時に失敗した。
- 原因: PowerShellで文として完結した`foreach`の直後へパイプを置いた。
- 再発防止: 複数行の反射・集計結果は配列変数へ格納し、その変数を別文で整形する。解析時失敗のため旧リポジトリへ変更がないことを確認して再試行する。

## 2026-07-16: Everything.dllのPE解析用PowerShellでswitch式構文を誤った

- 事象: PE Machine値を表示する監査コマンドが `Unexpected token '{'` で解析時に失敗した。
- 原因: PowerShellの`switch`文をハッシュテーブル値の式として直接記述した。
- 再発防止: 監査用の一時コマンドでも複雑な式を避け、Machine値の名称化は単純な`if/elseif`で行う。解析エラー時は対象ファイルに触れていないことを確認してから再試行する。

## 2026-07-16: R0監査のサンドボックス回避中にLESSONSを0バイト化した

- 事象: Beacon-oldを作業ディレクトリにした監査コマンドと`apply_patch`が `CreateProcessWithLogonW failed: 5` で失敗した。代替のPowerShell追記で`-replace`構文を誤り、未設定値を`WriteAllText`へ渡して本ファイルを0バイト化した。
- 原因: 既知のWindowsサンドボックス制約に加え、復旧用コマンドを一度に組み立て、PowerShellの非終端エラー後も書き込みが継続する状態にした。
- 再発防止: 作業ディレクトリを本リポジトリへ固定する。編集前内容をGitで確認し、`$ErrorActionPreference = 'Stop'`、マーカー存在確認、生成文字列の検証を済ませてから1回だけ書き込む。旧リポジトリは絶対パスで参照する。

## 2026-07-16: 既定サンドボックスのcodex execがR0監査で何も実行できなかった

- 事象: `codex exec --model gpt-5.6-sol`（既定サンドボックス）が、本リポジトリへの書き込み・Beacon-oldのディレクトリ列挙/grep/ビルドのすべてを `CreateProcessWithLogonW failed: 5` で拒否され、Phase R0が変更ゼロで終了した。
- 原因: CodexのWindowsサンドボックスが補助プロセス起動を拒否する既知事象（Beacon-old側LESSONSに同種記録あり）。加えて作業リポジトリ外のBeacon-oldはworkspaceに含まれない。
- 再発防止: **この環境ではヘッドレスの `codex exec` を使わない**（2026-07-16 ユーザー指示）。CodexはユーザーがCodex側の環境で対話実行し、プロンプトはPROMPTS.mdから渡す。実行後に `git -C <Beacon-old> status` で無変更を検証する。PROMPTS.md冒頭の運用欄に反映済み。

## 2026-07-16: ローカルのフォルダ名とリポジトリ実体の不一致で誤push寸前だった

- 事象: `C:\Users\ha.takaku\Desktop\Project\Beacon`（旧WPF版チェックアウト）のoriginが、リポジトリ分離後の新 `Crowlxy/Beacon.git` を指したままだった。
- 原因: GitHub側でBeacon→Beacon-oldへ分離した際、ローカルのremote URLが未更新だった。
- 再発防止: originを `Beacon-old.git` へ修正済み。ローカルフォルダ名もリポジトリ名と一致させた（新=`...\Project\Beacon`、旧=`...\Project\Beacon-old`。2026-07-16リネーム）。push前に `git remote -v` を確認する。

## 2026-07-16: 旧WPF計画（Beacon-old docs/spotlight/）をWinUI再構築へ方針転換

- 事象: WPF+iNKORE前提の改修計画（Phase 0〜9）を進行中に、UI層のWinUI 3新規構築・独立リポジトリへ方針変更。旧Phase 1が未コミットのまま中断（Beacon-old側にのみ保存）。
- 原因: iNKORE.UI.WPF.Modernの商用ライセンス制約と、WPF表示層延命のコスト。
- 再発防止: UI基盤の依存はプロジェクト初期にライセンスと寿命を審査する（SPEC §8・DEPENDENCY_MAP運用に反映済み）。「設定値の適用経路を先に確認してから見た目を実装する」という旧計画の教訓はR4実装時に適用する。
