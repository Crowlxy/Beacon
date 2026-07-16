# ADR-0003: Flow互換プラグインは別プロセス Beacon.PluginHost.exe に隔離し、versioned JSON-RPC over Named Pipe で接続する

- 状態: **承認済み（2026-07-16 ユーザー承認）**。RPCライブラリ選定はR1で確定
- 日付: 2026-07-16

## 決定

1. Flow Launcher互換プラグイン（`Flow.Launcher.Plugin.dll` を参照する.NETプラグイン、およびJSON-RPC系プラグイン）は **`Beacon.PluginHost.exe`（別プロセス）** でロードする。WinUIプロセスへWPFアセンブリを一切ロードしない。
2. WinUI本体⇔PluginHost間は **バージョン付きJSON-RPC over Named Pipe**。ライブラリの第一候補は **StreamJsonRpc（MIT）** — Beacon-oldのCoreが2.22.11を使用し、`JsonRPCPluginV2`/`ProcessStreamPluginV2` でstdio+StreamJsonRpcの実績があることを確認済み（AUDIT.md）。新Beaconでは新規依存となるため、**R1でライセンス原文確認+DEPENDENCY_MAP記録の上で採用を確定**する。
3. PluginHostはWPFアセンブリ参照を持ってよい（`Result.Icon`デリゲート実行・`PreviewPanel`生成はHost内に閉じる。PreviewPanelはTier 4=初期非対応として結果データのみ返す）。
4. 実行モデル: 検索結果はDTOで逐次配信し、選択実行は `ExecutionToken` をHostへ返して**Host内で `Result.Action/AsyncAction` を起動**する。トークンはクエリセッションIDに紐づけ、古い/不正なトークンは実行しない。
5. Hostは本体から監視・再起動可能にする（タイムアウト、キャンセル、クラッシュ隔離、Job Objectによる道連れ終了）。Host停止時も本体の内蔵プロバイダー（Beacon.Core直結のProgram/Files/Calculator等）は動き続ける。
6. パイプ名はセッション単位のランダム要素を含め、同一ユーザーのみアクセス可能なACLを設定する。DataRootは起動引数で正規化済み絶対パスとして渡す（ADR-0004）。

## 理由

- Result/IPublicAPIのWPF汚染（AUDIT.md §2）により、プロセス内互換はWinUIでは成立しない。
- 別プロセス化は同時に、第三者プラグインのクラッシュ隔離・タイムアウト・将来のBeaconネイティブAPI移行の境界になる。
- StreamJsonRpcは双方向・キャンセル・逐次通知をサポートし、Beacon-oldでの実績がある。

## 帰結

- プロセス間往復のレイテンシが増える。逐次配信と内蔵プロバイダー優先で体感を守る（Gate Bで実測判定）。
- `ISettingProvider.CreateSettingPanel()`（WPF Control）はWinUIへ埋め込めない → 互換Tier 3の扱い（COMPATIBILITY.md）。JSONベース設定はWinUIで描画する。
- IPublicAPIのHost側実装は「Hostプロセス内で完結」「本体へRPC委譲」「非対応(no-op+理由表示)」に分類する（Phase R7で表を作る）。
