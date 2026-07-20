# 失敗記録簿（LESSONS）

失敗した実行・実装は必ずここへ記録し、再発させない。**Claude・Codexとも作業開始前に本ファイルを読むこと。** クローズ済みの過去記録は [LESSONS-archive.md](LESSONS-archive.md)（必読ではない。記録前の重複検索と類似事象の調査時のみ参照）。
旧WPF計画時代の記録は Beacon-old の `docs/spotlight/LESSONS.md` にあり、**環境系の教訓は引き続き有効**（特に: ビルド前にOutput/Debug使用中プロセスを停止 / ログはFileShare.ReadWriteで読む / 大量警告時はerror行だけ抽出 / ライセンスは原文確認 / rg検索は単純な語へ分割 / BOM保持）。

記録ルール:
1. **記録前に本ファイルと LESSONS-archive.md を検索**し、同一原因のエントリが既にあれば新規エントリを作らない。既存エントリへ「再発: YYYY-MM-DD（反映漏れの箇所）」を追記する。再発＝前回の再発防止が効く場所に落ちていなかった証拠なので、その場で反映する
2. **再発防止は散文で終わらせない**。機械的に効く場所（スクリプト・PROMPTS.md・AGENTS.md・MSBuildターゲット等）があれば、そこへ反映してから記録を閉じる。「次から気をつける」だけの再発防止は不可
3. **クローズしたエントリはアーカイブへ移す**。再発防止の反映が完了した、または一過性で完了したエントリは、フェーズ完了時に LESSONS-archive.md へ移動する。機械反映できない一般教訓は下の「有効な教訓」へ一行で残す。本ファイルは常に「未解決＋直近フェーズ分」の分量に保つ

有効な教訓（アーカイブ済みエントリの一行要約。詳細は [LESSONS-archive.md](LESSONS-archive.md)）:

- SDK生成物（obj/bin配下）のパスは推測せず、実列挙してから検索する
- ネイティブ資源はProgram→App→Windowの所有鎖全体でIDisposable破棄経路を作り、CA1001/CA1806/CA1816を局所抑制で隠さない。非sealed型のDisposeはGC.SuppressFinalizeを呼ぶ
- Windowsオブジェクト名・パスをC#へ書くときはverbatim文字列（エスケープ段数に依存させない）
- NUnitテストは`using NUnit.Framework`を明示。外部ライブラリのAPIは実在オーバーロードを確認してから書く
- Codexへ渡すパッチにバッククォート・PowerShell行継続を含めない。複数行パッチはバッチを介さずArgumentListで単一引数として渡す
- 調査用の一時PowerShellでも複雑な式を避ける（switch式・foreach直後のパイプは解析エラー）
- ファイル上書き前にGitで現状確認し、`$ErrorActionPreference='Stop'`と生成文字列の検証を済ませてから1回だけ書く
- push前に`git remote -v`でoriginを確認する

## 2026-07-17: QueryOrchestratorの非キャンセル待機でCA2016

- 事象: Phase R2のReleaseビルドで`WaitToReadAsync()`がCA2016により1エラーとなった。
- 原因: 旧セッションを例外終了させずproducerのchannel完了まで読む設計で、意図的な`CancellationToken.None`を明示していなかった。
- 再発防止: 非キャンセル待機には`CancellationToken.None`を明示し、Releaseビルドのanalyzerを実装チェックとして維持（Coreへ反映済み）。

## 2026-07-17: Coreテストの固定期待値配列でCA1861

- 事象: CA2016修正後のReleaseビルドで、到着順テストの`new[] { "fast", "slow" }`がCA1861により1エラーとなった。
- 原因: 繰り返し評価されるNUnit制約へ固定配列を直接渡した。
- 再発防止: 少数要素の順序確認は個別assertとし、不要なstatic配列を追加しない（Core.Testsへ反映済み）。

記録形式（新しいものを上へ）:

```
## YYYY-MM-DD: 一行要約
- 事象: 何が起きたか（エラーメッセージ・症状）
- 原因: 根本原因（未確定なら未確定と書く）
- 再発防止: 次から何をするか（SPEC/AGENTS/PROMPTS/スクリプトへ反映済みの旨を書く）
```

---

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
- 再発防止: 必要最小限の昇格実行を使い、列挙済みの実在パスだけを読む。プロセス不在は正常終了として扱い、複数行パッチはバッチを介さずCodex実体へ単一引数で渡す。

## 2026-07-17: XBF修正後の再スモークでホットキー送信の競合とRPC結果未着を確認

- 事象: XBF同梱ZIP（16:24再ビルド）の再スモークで、XAML初期化〜ホットキー/トレイ登録〜RPC spike開始までログが揃ったが、`Hotkey or activation pipe` と `RPC incremental result` が15秒以内に出ず失敗した。ERRORログはなし。
- 原因: (1) Test-Portable.ps1がログファイル出現直後にホットキーを送信しており、`Hotkey and tray registered`（約150ms後）より早い。前日の再発防止「登録ログを待ってから送る」がスクリプトへ未反映だった。(2) RPC側は未確定だが、サーバーが`NotifyWithParameterObjectAsync`で名前付きパラメータ送信するのに対し、受信側`OnSearchResult(SearchResultDto result)`に`UseSingleObjectParameterDeserialization = true`がなく、バインド失敗で通知が黙って破棄されている可能性が高い。
- 再発防止: スモークは`Hotkey and tray registered`のログ行を確認してからホットキーを送る。StreamJsonRpcでDTO1個を通知する場合は受信側`[JsonRpcMethod]`に`UseSingleObjectParameterDeserialization = true`を必ず付ける。
