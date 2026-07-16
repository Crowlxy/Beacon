# 失敗記録簿（LESSONS）

失敗した実行・実装は必ずここへ記録し、再発させない。**Claude・Codexとも作業開始前に本ファイルを読むこと。**
旧WPF計画時代の記録は Beacon-old の `docs/spotlight/LESSONS.md` にあり、**環境系の教訓は引き続き有効**（特に: ビルド前にOutput/Debug使用中プロセスを停止 / ログはFileShare.ReadWriteで読む / 大量警告時はerror行だけ抽出 / ライセンスは原文確認 / rg検索は単純な語へ分割 / BOM保持）。

記録形式（新しいものを上へ）:

```
## YYYY-MM-DD: 一行要約
- 事象: 何が起きたか（エラーメッセージ・症状）
- 原因: 根本原因（未確定なら未確定と書く）
- 再発防止: 次から何をするか（可能ならSPEC/AGENTS/PROMPTSへ反映済みの旨を書く）
```

---

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
