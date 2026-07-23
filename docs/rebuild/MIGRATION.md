# MIGRATION — 旧WPF版（Beacon-old）からのデータ移行（Phase R7）

## 1. 移行元と移行先

| 移行元（旧WPF版の実行環境） | 移行先（新Portable版） |
|---|---|
| `%APPDATA%\Beacon\Settings\Settings.json` | `<BeaconRoot>\Data\Settings\`（新スキーマへ変換） |
| 履歴・選択回数・TopMostRecord（保存形式はR0で確認） | `<BeaconRoot>\Data\History\` |
| 旧ポータブル `<旧BeaconRoot>\UserData\` | 同上（`.dead`指標を尊重） |
| 移行しない: テーマ設定・UI寸法設定（QueryBoxFontSize等）・時計/サウンド設定 | 新版に該当機能がない（固定デザイン） |

第三者プラグイン・プラグイン設定は対象外（2026-07-20ユーザー決定）。引き継ぐ値の確定表は§5。

## 2. 移行フロー（一度だけ・明示的）

1. 初回起動時に旧データ（`%APPDATA%\Beacon` または旧`UserData`）を検出したら、**対象と移行先を表示して確認を求める**（自動では移行しない）
2. 移行前バックアップ: 旧データを `<BeaconRoot>\Data\Backup\legacy-<日付>\` へコピー
3. 変換・コピー → 整合性確認（ファイル数・主要キーの読み戻し）
4. 失敗時ロールバック: 新Data側の書き込みを消して未移行状態へ戻す。旧版はそのまま使い続けられる
5. `Data\State\migration.json` に移行バージョン・日時・結果を記録（再実行防止）
6. **元データはユーザー確認なしで削除しない**（移行成功後も残す。削除はユーザー操作）

## 3. 並行運用の安全規則

- 新旧でファイルを共有しない（ADR-0004 §4）。旧版は`%APPDATA%\Beacon`を使い続け、新版は触らない（読み取りは移行時のみ）
- 開発中identifier（`Beacon.Next` / Mutex・パイプ別名）により単一インスタンス機構が新旧で衝突しないこと（R1で検証）
- ホットキーの二重登録: 新版初回起動時に旧版稼働を検出したら警告。開発中は既定をAlt+Shift+Space等にして回避可

## 4. プラグイン互換API（移行対象外）

- `Flow.Launcher.Plugin.dll` 互換層の公開APIに破壊的変更を入れない（COMPATIBILITY.md §3）
- `ResultKind` 等の追加は省略可能メンバーのみ（未指定=Unknown）

本節は将来互換を再開する場合の設計記録であり、Phase R7ではプラグイン本体・設定を検出、コピー、変換しない。

## 5. Phase R7 Settings.cs全プロパティ走査結果（2026-07-23）

移行元: Beacon-old/Flow.Launcher.Infrastructure/UserSettings/Settings.cs。JsonIgnoreの計算プロパティとメソッドは永続値でないため除外。

| 分類 | 旧プロパティ | 新キー / 理由 |
|---|---|---|
| 移行 | Hotkey | GlobalHotkey（空白を正規化） |
| 移行 | ColorScheme, Language | 同名。将来UI反映口用に保持 |
| 移行 | CustomShortcuts | LegacyCustomShortcuts。旧データを欠損させず保持 |
| 非移行: 新版固定UI | Theme, WindowSize, WindowHeightSize, ItemHeightSize, QueryBoxFontSize, ResultItemFontSize, ResultSubItemFontSize, QueryBoxFont, QueryBoxFontStyle, QueryBoxFontWeight, QueryBoxFontStretch, ResultFont, ResultFontStyle, ResultFontWeight, ResultFontStretch, ResultSubFont, ResultSubFontStyle, ResultSubFontWeight, ResultSubFontStretch, SettingWindowFont, SettingWindowWidth, SettingWindowHeight, SettingWindowTop, SettingWindowLeft, SettingWindowState, UseDropShadowEffect, BackdropType, UseGlyphIcons, UseAnimation, AnimationSpeed, CustomAnimationLength, ShowBadges, ShowBadgesGlobalOnly, ShowPlaceholder, PlaceholderText, ShowHomePage, ShowHistoryResultsForHomePage, MaxHistoryResultsToShowForHomePage, KeepMaxResults, MaxResultsToShow, WindowLeft, WindowTop, PreviousScreenWidth, PreviousScreenHeight, PreviousDpiX, PreviousDpiY, CustomWindowLeft, CustomWindowTop, SearchWindowScreen, SearchWindowAlign, CustomScreenNumber, ShowTaskbarWhenInvoked, ShowAtTopmost | 新版はDesignTokensと既存配置規則が正 |
| 非移行: 新版に機能なし | UseClock, UseDate, TimeFormat, DateFormat, UseSound, SoundVolume, AlwaysPreview, PreviewHotkey, EnableDialogJump, AutoDialogJump, ShowDialogJumpWindow, DialogJumpWindowPosition, DialogJumpResultBehaviour, DialogJumpFileResultBehaviour, DialogJumpHotkey, WMPInstalled | 対応機能なし |
| 非移行: 既存操作仕様が正 | OpenResultModifiers, ShowOpenResultHotkey, AutoCompleteHotkey, AutoCompleteHotkey2, SelectNextItemHotkey, SelectNextItemHotkey2, SelectPrevItemHotkey, SelectPrevItemHotkey2, SelectNextPageHotkey, SelectPrevPageHotkey, OpenContextMenuHotkey, SettingWindowHotkey, OpenHistoryHotkey, CycleHistoryUpHotkey, CycleHistoryDownHotkey, IgnoreHotkeysOnFullscreen, HideWhenDeactivated, LastQueryMode, HistoryStyle | SPEC §7を維持 |
| 非移行: 検索退行防止 | CustomExplorerIndex, CustomExplorerList, CustomBrowserIndex, CustomBrowserList, ShouldUsePinyin, UseDoublePinyin, DoublePinyinSchema, AlwaysStartEn, QuerySearchPrecision, IgnoreAccents, SearchQueryResultsWithDelay, SearchDelayTime, LeaveCmdOpen | 現行プロバイダー挙動を変更しない |
| 非移行: 配布/運用 | BeaconSettingsVersion, ReleaseNotesVersion, FirstLaunch, AutoRestartAfterChanging, ShowUnknownSourceWarning, AutoUpdatePlugins, AutoUpdates, DontPromptUpdateMsg, EnableUpdateLog, StartFlowLauncherOnSystemStartup, UseLogonTaskForStartup, HideOnStartup, HideNotifyIcon, LogLevel, Proxy, ActivateTimes | 新版のPortable・ログ規則が正 |
| 対象外: 第三者プラグイン | CustomPluginHotkeys, PluginSettings | 2026-07-20決定 |

Everything固有設定はこのSettings.csではなく旧Explorerプラグイン設定に属する。プラグイン設定移行の対象外決定と、現行Everything→Windows Index自動フォールバックを優先し、R7では移行しない。
