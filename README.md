# CustomRP CLI

[![.NET](https://img.shields.io/badge/.NET-8%2B-512BD4)](https://dotnet.microsoft.com/download)
[![Platform](https://img.shields.io/badge/platform-macOS%20%7C%20Linux%20%7C%20Windows-lightgrey)]()

**macOS / Linux / Windows** で動く Discord Rich Presence マネージャーの CLI 版です。  
[maximmax42/Discord-CustomRP](https://github.com/maximmax42/Discord-CustomRP)（Windows GUI 版）のコアロジックを移植しています。  
`.crp` プリセットファイルは **Windows 版と完全互換** です。

---

## 必要環境

| 用途 | 必要なもの |
|---|---|
| ビルド | [.NET SDK 8 以上](https://dotnet.microsoft.com/download) |
| 実行（自己完結バイナリ） | 不要（単体で動く） |
| Discord 連携 | Discord デスクトップアプリ（起動済みであること） |

---

## インストール

### macOS に .NET をインストール

```bash
brew install dotnet
```

### ソースからビルド

```bash
git clone https://github.com/your-username/CustomRP-CLI.git
cd CustomRP-CLI
dotnet build
```

### 自己完結バイナリを生成（推奨）

```bash
# macOS (Apple Silicon)
dotnet publish -c Release -r osx-arm64 --self-contained true -o ./publish

# macOS (Intel)
dotnet publish -c Release -r osx-x64 --self-contained true -o ./publish

# Linux (x64)
dotnet publish -c Release -r linux-x64 --self-contained true -o ./publish

# Windows (x64)
dotnet publish -c Release -r win-x64 --self-contained true -o ./publish
```

生成した `./publish/customrp` は .NET 不要で単体で動きます。

```bash
# PATH に追加（zsh 例）
echo 'export PATH="$HOME/Desktop/GitHub/CustomRP-CLI/publish:$PATH"' >> ~/.zshrc
source ~/.zshrc
```

---

## クイックスタート

```bash
# 1. 対話モードで起動
dotnet run

# 2. ワンライナーで即接続
dotnet run -- --id 123456789012345678 --details "作業中" --state "集中モード" --connect

# 3. バックグラウンドで常駐
dotnet run -- --daemon --id 123456789012345678 --details "作業中" &
```

> **Application ID の取得**  
> [Discord Developer Portal](https://discord.com/developers/applications) でアプリを作成し、「General Information」の **APPLICATION ID** をコピーしてください。

---

## 使い方

### 対話モード

引数なしで起動するとメニューが表示されます。

```
╔════════════════════════════════════════╗
║   CustomRP CLI  —  Discord Rich RP     ║
╚════════════════════════════════════════╝

  ステータス: 切断済み

  Application ID: 123456789012345678
  タイプ        : Playing (0)
  Details       : 作業中
  State         : 集中モード
  大画像        : my_icon
  ボタン1       : GitHub  https://github.com/...

  ─────────────────────────────────────────
  [c] 接続   [d] 切断   [u] プレゼンス更新
  [s] 設定を編集   [p] プリセット読込   [P] プリセット保存
  [q] 終了
```

| キー | 動作 |
|---|---|
| `c` | Discord に接続 |
| `d` | 切断（Rich Presence をクリア） |
| `u` | 設定変更後にプレゼンスを更新 |
| `s` | 設定エディタを開く |
| `p` | `.crp` プリセットファイルを読み込む |
| `P` | 現在の設定を `.crp` として保存 |
| `q` | 終了（設定を自動保存） |

#### 設定エディタ（`s` キー）

```
  ── 設定を編集 ──

  [1] Application ID    : 123456789012345678
  [2] タイプ             : Playing (0)
  [3] Details           : 作業中
  [4] State             : 集中モード
  [5] 大画像キー         : my_icon
  [6] 大画像テキスト     : ゲーム名
  [7] 小画像キー         :
  [8] 小画像テキスト     :
  [9] ボタン1 テキスト   : GitHub
  [A] ボタン1 URL       : https://github.com/yourname
  [B] ボタン2 テキスト   :
  [C] ボタン2 URL       :
  [D] パイプ番号         : 自動
  [E] タイムスタンプ     : SinceLastConnection
  [F] 自動接続           : 無効

  [S] 保存して戻る   [Q] 保存せずに戻る  >
```

編集したいキー（1〜F）を押すと入力プロンプトが表示されます。  
`S` で保存して戻る、`Q` で変更を破棄して戻ります。

---

### 引数モード

```bash
# 基本
customrp --id 123456789012345678 --details "作業中" --state "集中" --connect

# 画像・ボタン付き
customrp \
  --id 123456789012345678 \
  --details "コーディング中" \
  --state "CustomRP を作ってる" \
  --large-image my_icon \
  --large-text "VS Code" \
  --button1-text "GitHub" \
  --button1-url "https://github.com/yourname" \
  --connect

# プリセットを読み込んで接続
customrp --preset ~/Downloads/game.crp --connect
```

---

### Daemon モード

UI を表示せずにバックグラウンドで動作します。

```bash
# バックグラウンドで起動
customrp --id 123456789012345678 --details "作業中" --daemon &

# プリセット + daemon
customrp --preset ~/game.crp --daemon &

# 停止
kill $(pgrep customrp)
```

#### macOS ログイン時に自動起動（LaunchAgent）

`~/Library/LaunchAgents/com.yourname.customrp.plist` を作成：

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.yourname.customrp</string>
    <key>ProgramArguments</key>
    <array>
        <string>/Users/yourname/Desktop/GitHub/CustomRP-CLI/publish/customrp</string>
        <string>--daemon</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
</dict>
</plist>
```

```bash
launchctl load   ~/Library/LaunchAgents/com.yourname.customrp.plist  # 登録
launchctl unload ~/Library/LaunchAgents/com.yourname.customrp.plist  # 解除
```

---

### プリセットファイル（.crp）

Windows 版 CustomRP の `.crp` ファイル（XML形式）をそのまま読み書きできます。

```bash
# 読み込んで接続
customrp --preset ~/game.crp --connect

# 対話モードでも [p] 読込 / [P] 保存 が使える
```

プリセットには Details / State / 画像 / ボタン / タイムスタンプが含まれます。  
Application ID とパイプ番号は `config.json` の値が優先されます。

---

## 全オプション一覧

```
使い方: customrp [オプション]

  --config <path>         設定ファイルのパス（既定: ~/.config/customrp/config.json）
  --preset <path>         .crp プリセットファイルを読み込む

  --id <id>               Discord Application ID
  --type <0-5|name>       アクティビティタイプ（下表参照）
  --details <text>        Details テキスト（最大 128 文字）
  --state <text>          State テキスト（最大 128 文字）
  --large-image <key>     大画像のキー名または URL
  --large-text <text>     大画像のツールチップ
  --small-image <key>     小画像のキー名または URL
  --small-text <text>     小画像のツールチップ
  --button1-text <text>   ボタン1 のラベル
  --button1-url <url>     ボタン1 の URL
  --button2-text <text>   ボタン2 のラベル
  --button2-url <url>     ボタン2 の URL
  --pipe <-1..9>          Discord パイプ番号（-1 = 自動）

  --connect, -c           起動時に自動接続
  --daemon                UI なしでバックグラウンド実行
  --version, -v           バージョンを表示
  --help, -h              このヘルプを表示
```

### `--type` の値

| 指定値 | Discord での表示 |
|---|---|
| `0` または `playing` | 🎮 Playing |
| `1` または `streaming` | 📡 Streaming |
| `2` または `listening` | 🎵 Listening to |
| `3` または `watching` | 📺 Watching |
| `5` または `competing` | 🏆 Competing in |

### タイムスタンプモード（設定エディタの `[E]`）

| 値 | 説明 |
|---|---|
| `0` / `SinceLastConnection` | 最後に接続した時刻から |
| `1` / `SinceStartup` | アプリ起動時刻から |
| `2` / `LocalTime` | 現地時刻を表示 |
| `3` / `Custom` | 指定した日時（設定エディタで変更） |
| `4` / `SincePresenceUpdate` | プレゼンス更新時刻から |

---

## 設定ファイル

`~/.config/customrp/config.json` に自動保存されます。直接編集も可能です。

```json
{
  "id": "123456789012345678",
  "pipe": -1,
  "autoConnect": false,
  "type": 0,
  "details": "作業中",
  "state": "集中モード",
  "largeKey": "my_icon",
  "largeText": "アイコンの説明",
  "smallKey": "",
  "smallText": "",
  "button1Text": "GitHub",
  "button1Url": "https://github.com/yourname",
  "button2Text": "",
  "button2Url": "",
  "timestamps": 0
}
```

ログは `~/.config/customrp/logs/YYYY-MM-DD.log` に出力されます。

---

## Windows版との違い

| 機能 | Windows GUI版 | CLI版 |
|---|---|---|
| Discord RPC 接続・切断 | ✅ | ✅ |
| 自動再接続（30回・10秒間隔） | ✅ | ✅ |
| プリセット読込・保存 | ✅ | ✅（.crp 互換） |
| タイムスタンプ（5種類） | ✅ | ✅ |
| 画像・ボタン | ✅ | ✅ |
| macOS / Linux 対応 | ❌ | ✅ |
| ActivityType（Playing 等） | ✅ | ⚠️ 設定保存のみ※ |
| DetailsUrl / StateUrl | ✅ | ⚠️ 設定保存のみ※ |
| GUI・トレイアイコン | ✅ | ❌ |
| ログイン時自動起動 | ✅（レジストリ） | ✅（LaunchAgent） |
| 画像アセット一覧取得 | ✅ | ❌ |
| アップデート通知 | ✅ | ❌ |

> ※ **設定保存のみ** = `config.json` と `.crp` には正しく保存され Windows 版と共有できますが、
> macOS/Linux 向け NuGet ライブラリが未対応のため Discord への送信はされません。

---

## ライセンス

MIT — 元プロジェクト [maximmax42/Discord-CustomRP](https://github.com/maximmax42/Discord-CustomRP) の MIT ライセンスに準拠します。
