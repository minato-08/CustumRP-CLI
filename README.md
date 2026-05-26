# CustomRP CLI

[![.NET](https://img.shields.io/badge/.NET-10%2B-512BD4)](https://dotnet.microsoft.com/download)
[![Platform](https://img.shields.io/badge/platform-macOS%20%7C%20Linux%20%7C%20Windows-lightgrey)]()

**macOS / Linux / Windows** で動く Discord Rich Presence マネージャーの CLI 版です。  
[maximmax42/Discord-CustomRP](https://github.com/maximmax42/Discord-CustomRP)（Windows GUI 版）のコアロジックを移植しています。  
`.crp` プリセットファイルは **Windows 版と完全互換** です。

外部ライブラリへの依存は **ゼロ** です。Discord IPC プロトコルを直接実装しているため、
ゲーム名の上書き（`Name`）やアクティビティタイプ（Playing / Listening 等）も完全対応しています。

---

## 必要環境

| 用途 | 必要なもの |
|---|---|
| ビルド | [.NET SDK 10 以上](https://dotnet.microsoft.com/download) |
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
git clone https://github.com/minato-08/CustumRP-CLI.git
cd CustumRP-CLI
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
echo 'export PATH="$HOME/Desktop/GitHub/CustumRP-CLI/publish:$PATH"' >> ~/.zshrc
source ~/.zshrc
```

---

## クイックスタート

```bash
# 1. 対話モードで起動
dotnet run

# 2. ワンライナーで即接続
dotnet run -- --id 123456789012345678 --name "VS Code" --details "コーディング中" --connect

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

  ステータス: 接続済み (username)

  Application ID: 123456789012345678
  タイプ        : Playing (0)
  ゲーム名      : VS Code
  Details       : コーディング中
  State         : CustomRP を作ってる

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

  [1] Application ID: 123456789012345678
      Discord Developer Portal のアプリ ID。空欄 = CustomRP デフォルト ID
  [2] タイプ        : Playing (0)
      0=Playing  1=Streaming  2=Listening  3=Watching  5=Competing
  [3] ゲーム名 (Name): VS Code
      「〇〇をプレイ中」の名前部分。未設定 = Discord アプリ名をそのまま表示
  [4] Details       : コーディング中
      プレゼンスの1行目（最大 128 文字）
  [5] State         : CustomRP を作ってる
      プレゼンスの2行目（最大 128 文字）
  ...
  [S] 保存して戻る   [Q] 保存せずに戻る  >
```

編集したいキー（1〜G）を押すと入力プロンプトが表示されます。  
`S` で保存して戻る、`Q` で変更を破棄して戻ります。

---

### 引数モード

```bash
# 基本
customrp --id 123456789012345678 --name "VS Code" --details "作業中" --connect

# 画像・ボタン付き
customrp \
  --id 123456789012345678 \
  --name "VS Code" \
  --type watching \
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
        <string>/Users/yourname/Desktop/GitHub/CustumRP-CLI/publish/customrp</string>
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
  --name <text>           ゲーム名の上書き（「〇〇をプレイ中」の〇〇部分）
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

---

## 設定リファレンス

設定は `~/.config/customrp/config.json` に保存されます。直接編集も可能です。

```json
{
  "id": "",
  "pipe": -1,
  "autoConnect": false,
  "type": 0,
  "name": "",
  "details": "",
  "state": "",
  "largeKey": "",
  "largeText": "",
  "smallKey": "",
  "smallText": "",
  "button1Text": "",
  "button1Url": "",
  "button2Text": "",
  "button2Url": "",
  "timestamps": 0,
  "customTimestamp": "2026-01-01T00:00:00",
  "customTimestampEndEnabled": false,
  "customTimestampEnd": "2026-01-01T01:00:00"
}
```

### 各フィールドの説明

#### 接続設定

| フィールド | 型 | 説明 |
|---|---|---|
| `id` | string | Discord Application ID。[Developer Portal](https://discord.com/developers/applications) で取得。空欄にすると CustomRP 公式の ID を使用。 |
| `pipe` | int | Discord の接続パイプ番号（0〜9）。`-1` で自動検索（通常はこれでOK）。Discord を複数インスタンス起動している場合のみ変更。 |
| `autoConnect` | bool | `true` にするとアプリ起動時に自動で Discord に接続する。 |

#### アクティビティ

| フィールド | 型 | 説明 |
|---|---|---|
| `type` | int | アクティビティの種類。`0`=Playing / `1`=Streaming / `2`=Listening to / `3`=Watching / `5`=Competing in |
| `name` | string | 「〇〇をプレイ中」の **〇〇部分** を上書きする名前。空欄にすると Discord アプリ名（Developer Portal で登録した名前）がそのまま表示される。 |
| `details` | string | プレゼンスの **1行目**。何をしているかを短く記入（最大 128 文字）。 |
| `state` | string | プレゼンスの **2行目**。状態・補足情報を記入（最大 128 文字）。 |

#### 画像

画像は Discord Developer Portal のアプリページで「Rich Presence → Art Assets」から事前にアップロードして登録する必要があります。  
直接 URL（`https://...`）でも指定できます。

| フィールド | 型 | 説明 |
|---|---|---|
| `largeKey` | string | **大画像** のキー名、または画像の直接 URL。 |
| `largeText` | string | 大画像にカーソルを当てると表示されるツールチップ（最大 128 文字）。 |
| `smallKey` | string | **小画像** のキー名または URL。大画像の右下に小さく表示される。 |
| `smallText` | string | 小画像のツールチップ（最大 128 文字）。 |

#### ボタン

プレゼンスにボタンを最大 2 つ表示できます。テキストと URL の両方を設定したときのみ表示されます。

| フィールド | 型 | 説明 |
|---|---|---|
| `button1Text` | string | ボタン1 のラベル（最大 32 文字）。 |
| `button1Url` | string | ボタン1 をクリックしたときに開く URL（`https://` 必須）。 |
| `button2Text` | string | ボタン2 のラベル（最大 32 文字）。 |
| `button2Url` | string | ボタン2 の URL。 |

#### タイムスタンプ

Discord のプレゼンスに経過時間カウンターを表示する機能です。

| `timestamps` の値 | 説明 |
|---|---|
| `0` — SinceLastConnection | Discord に最後に接続した時刻からの経過時間 |
| `1` — SinceStartup | アプリを起動した時刻からの経過時間 |
| `2` — LocalTime | 現地時刻（時計のように現在時刻を表示） |
| `3` — Custom | `customTimestamp` で指定した日時からの経過時間（または終了時刻まで） |
| `4` — SincePresenceUpdate | プレゼンスを更新するたびにリセットされる経過時間 |

`timestamps: 3`（Custom）のとき：
- `customTimestamp` — 開始日時（ISO 8601 形式）
- `customTimestampEndEnabled` — `true` にすると終了時刻も指定できる
- `customTimestampEnd` — 終了日時（`true` のときのみ有効）

---

## Windows版との違い

| 機能 | Windows GUI版 | CLI版 |
|---|---|---|
| Discord RPC 接続・切断 | ✅ | ✅ |
| 自動再接続（30回・10秒間隔） | ✅ | ✅ |
| プリセット読込・保存 | ✅ | ✅（.crp 互換） |
| タイムスタンプ（5種類） | ✅ | ✅ |
| 画像・ボタン | ✅ | ✅ |
| ゲーム名上書き（Name） | ✅ | ✅ |
| ActivityType（Playing 等） | ✅ | ✅ |
| macOS / Linux 対応 | ❌ | ✅ |
| GUI・トレイアイコン | ✅ | ❌ |
| ログイン時自動起動 | ✅（レジストリ） | ✅（LaunchAgent） |
| 画像アセット一覧取得 | ✅ | ❌ |
| アップデート通知 | ✅ | ❌ |

---

## ログ

`~/.config/customrp/logs/YYYY-MM-DD.log` に出力されます。  
Discord IPC の通信ログが記録されるため、接続トラブルの調査に使えます。

---

## ライセンス

MIT — 元プロジェクト [maximmax42/Discord-CustomRP](https://github.com/maximmax42/Discord-CustomRP) の MIT ライセンスに準拠します。
