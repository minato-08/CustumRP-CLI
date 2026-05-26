# CustomRP CLI

macOS / Linux / Windows 向けの **コマンドラインベース** Discord Rich Presence マネージャーです。
オリジナルの [CustomRP (Windows GUI版)](https://github.com/maximmax42/Discord-CustomRP) のロジックをそのまま移植しています。

## 必要環境

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（ビルド時）または .NET 8 Runtime（発行済みバイナリ実行時）
- Discord デスクトップアプリ（実行中である必要があります）

## インストール / ビルド

```bash
# このディレクトリで
cd CustomRPC.CLI

# ビルドして実行
dotnet run

# または自己完結型バイナリとして発行 (macOS arm64 例)
dotnet publish -c Release -r osx-arm64 --self-contained true -o ./publish
./publish/customrp --help
```

> **macOS で .NET をインストールするには**
> ```bash
> brew install dotnet
> ```
> または [公式サイト](https://dotnet.microsoft.com/download) からインストーラーをダウンロード。

## 使い方

### 対話モード（引数なし）

```bash
customrp
```

起動するとメニューが表示されます。

```
╔════════════════════════════════════════╗
║   CustomRP CLI  —  Discord Rich RP     ║
╚════════════════════════════════════════╝
  ステータス: 切断済み

  Application ID: (デフォルト)
  タイプ       : Playing (0)
  Details      : 作業中
  State        : 集中モード

  [c] 接続   [d] 切断   [u] プレゼンス更新
  [s] 設定を編集   [p] プリセット読込   [P] プリセット保存
  [q] 終了
```

### 引数モード（スクリプト連携向け）

```bash
# ワンショットで接続
customrp --id 123456789012345678 --details "作業中" --state "集中モード" --connect

# プリセットファイルを読み込んで接続
customrp --preset ~/my_game.crp --connect

# バックグラウンドで常駐（daemon モード）
customrp --daemon &

# プリセット + daemon
customrp --preset ~/my_game.crp --daemon
```

### 全オプション

| オプション | 説明 |
|---|---|
| `--config <path>` | 設定ファイルのパス（既定: `~/.config/customrp/config.json`） |
| `--preset <path>` | `.crp` プリセットファイルを読み込む（Windows 版と互換） |
| `--id <id>` | Discord Application ID |
| `--type <type>` | `Playing` / `Listening` / `Watching` / `Competing`（または 0-5） |
| `--name <name>` | アクティビティ名 |
| `--details <text>` | Details テキスト |
| `--state <text>` | State テキスト |
| `--large-image <key>` | 大画像キーまたは URL |
| `--large-text <text>` | 大画像ツールチップ |
| `--small-image <key>` | 小画像キーまたは URL |
| `--small-text <text>` | 小画像ツールチップ |
| `--button1-text <text>` | ボタン1 ラベル |
| `--button1-url <url>` | ボタン1 URL |
| `--button2-text <text>` | ボタン2 ラベル |
| `--button2-url <url>` | ボタン2 URL |
| `--pipe <-1..9>` | Discord パイプ番号（-1 = 自動） |
| `--connect` / `-c` | 起動時に自動接続 |
| `--daemon` | UI なしでバックグラウンド実行（`--connect` 相当） |
| `--version` / `-v` | バージョン表示 |
| `--help` / `-h` | ヘルプ表示 |

## 設定ファイル

`~/.config/customrp/config.json` に JSON 形式で保存されます。

```json
{
  "id": "123456789012345678",
  "pipe": -1,
  "autoConnect": false,
  "type": 0,
  "display": 0,
  "name": "",
  "details": "作業中",
  "detailsUrl": "",
  "state": "集中モード",
  "stateUrl": "",
  "partySize": 0,
  "partyMax": 0,
  "timestamps": 0,
  "largeKey": "my_icon",
  "largeText": "アイコンの説明",
  "smallKey": "",
  "smallText": "",
  "button1Text": "",
  "button1Url": "",
  "button2Text": "",
  "button2Url": ""
}
```

## プリセットファイル (.crp)

Windows 版の CustomRP と同じ XML 形式です。Windows 版で作ったプリセットをそのまま読み込めます。

## Windows 版との違い

| 機能 | Windows GUI版 | CLI版 |
|---|---|---|
| Discord RPC 接続 | ✅ | ✅ |
| プリセット読込/保存 | ✅ | ✅ |
| 自動再接続 | ✅ | ✅ |
| タイムスタンプ全種類 | ✅ | ✅ |
| 画像・ボタン | ✅ | ✅ |
| GUI / トレイアイコン | ✅ | ❌（CLI） |
| スタートアップ登録 | ✅（Windows 専用） | ❌ |
| 画像アセット取得 | ✅ | ❌ |
| 多言語対応 UI | ✅ | ❌ |

## ログ

`~/.config/customrp/logs/YYYY-MM-DD.log` に記録されます。
