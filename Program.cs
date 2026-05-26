using System.Text;

namespace CustomRPC.CLI
{
    static class Program
    {
        // ── 共有ステート（RPC バックグラウンドスレッドが書き込む） ──────────────
        static string configPath = Config.DefaultConfigPath;
        static Config config = new();
        static RpcManager? rpc;

        static volatile string statusText   = "切断済み";
        static volatile string? connectedUser = null;

        // ── エントリポイント ──────────────────────────────────────────────────
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            // ── 第1パス：--help / --version / --config ───────────────────────
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--help": case "-h": PrintHelp(); return;
                    case "--version": case "-v": Console.WriteLine("CustomRP CLI 1.19.2"); return;
                    case "--config" when i + 1 < args.Length: configPath = args[++i]; break;
                }
            }

            // ── JSON 設定を読み込む ───────────────────────────────────────────
            config = Config.Load(configPath);

            // ── 第2パス：接続フラグ・プリセット・フィールド上書き ────────────────
            bool autoConnect = config.AutoConnect;
            bool daemon      = false;
            string? presetArg = null;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--preset"      when i + 1 < args.Length: presetArg         = args[++i]; break;
                    case "--connect": case "-c":                    autoConnect       = true; break;
                    case "--daemon":                                daemon = autoConnect = true; break;
                    case "--id"          when i + 1 < args.Length: config.Id         = args[++i]; break;
                    case "--type"        when i + 1 < args.Length: ParseType(args[++i]); break;
                    case "--name"        when i + 1 < args.Length: config.Name       = args[++i]; break;
                    case "--details"     when i + 1 < args.Length: config.Details    = args[++i]; break;
                    case "--state"       when i + 1 < args.Length: config.State      = args[++i]; break;
                    case "--large-image" when i + 1 < args.Length: config.LargeKey   = args[++i]; break;
                    case "--large-text"  when i + 1 < args.Length: config.LargeText  = args[++i]; break;
                    case "--small-image" when i + 1 < args.Length: config.SmallKey   = args[++i]; break;
                    case "--small-text"  when i + 1 < args.Length: config.SmallText  = args[++i]; break;
                    case "--button1-text"when i + 1 < args.Length: config.Button1Text= args[++i]; break;
                    case "--button1-url" when i + 1 < args.Length: config.Button1Url = args[++i]; break;
                    case "--button2-text"when i + 1 < args.Length: config.Button2Text= args[++i]; break;
                    case "--button2-url" when i + 1 < args.Length: config.Button2Url = args[++i]; break;
                    case "--pipe" when i + 1 < args.Length && int.TryParse(args[i + 1], out int p):
                        config.Pipe = p; i++; break;
                }
            }

            // プリセットを適用（ID / Pipe / AutoConnect は Config の値を優先）
            if (presetArg is not null)
            {
                var preset = Preset.Load(presetArg);
                if (preset.HasValue)
                {
                    preset.Value.ApplyTo(config);
                    if (!daemon) Console.WriteLine($"プリセット読み込み完了: {presetArg}");
                }
                else
                {
                    Console.Error.WriteLine($"プリセットの読み込みに失敗しました: {presetArg}");
                }
            }

            // ── RpcManager セットアップ ──────────────────────────────────────
            rpc = new RpcManager(config);
            rpc.OnStatus       += s  => statusText    = s;
            rpc.OnUserReady    += u  => connectedUser = u;
            rpc.OnDisconnected += () => connectedUser = null;

            // ── Ctrl+C ハンドラ ──────────────────────────────────────────────
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                rpc?.Disconnect();
                config.Save(configPath);
                Console.WriteLine("\n終了しました。");
                Environment.Exit(0);
            };

            if (autoConnect) rpc.Connect();

            // ── Daemon モード：UI なしでブロック ─────────────────────────────
            if (daemon)
            {
                Console.WriteLine("CustomRP がバックグラウンドで実行中です。Ctrl+C で終了。");
                Console.WriteLine($"設定ファイル: {configPath}");
                Thread.Sleep(Timeout.Infinite);
                return;
            }

            // ── 対話モード ────────────────────────────────────────────────────
            RunInteractive();
            rpc.Disconnect();
            config.Save(configPath);
        }

        // ── 対話ループ ────────────────────────────────────────────────────────
        static void RunInteractive()
        {
            while (true)
            {
                RenderMain();

                // Bug1修正: ReadKey でブロックせず 500ms ごとに再描画
                // → 接続完了・失敗などのバックグラウンド状態変化が即座に反映される
                char ch = WaitForKey(refreshMs: 500);
                if (ch == '\0') continue; // タイムアウト → 再描画のみ

                switch (char.ToLower(ch))
                {
                    case 'c':
                        // Bug2修正: 接続中・接続済みのときは無視
                        if (rpc!.State is RpcState.Disconnected or RpcState.Error)
                            rpc.Connect();
                        break;
                    case 'd':
                        // Bug2修正: 切断済みのときは無視
                        if (rpc!.State is not RpcState.Disconnected)
                            rpc.Disconnect();
                        break;
                    case 'u':
                        if (rpc!.State == RpcState.Connected) rpc.SetPresence();
                        break;
                    case 's': EditSettings(); break;
                    case 'p': LoadPreset();   break;
                    case 'P': SavePreset();   break;
                    case 'q': return;
                }
            }
        }

        /// <summary>
        /// キー入力を最大 <paramref name="refreshMs"/> ms 待つ。
        /// タイムアウトしたら '\0' を返す（呼び出し側は再描画すればよい）。
        /// </summary>
        static char WaitForKey(int refreshMs = 500)
        {
            var deadline = DateTime.Now.AddMilliseconds(refreshMs);
            while (DateTime.Now < deadline)
            {
                if (Console.KeyAvailable)
                    return Console.ReadKey(true).KeyChar;
                Thread.Sleep(30);
            }
            return '\0';
        }

        // ── メイン画面描画 ────────────────────────────────────────────────────
        static void RenderMain()
        {
            Console.Clear();

            // ヘッダー
            WriteColor("╔════════════════════════════════════════╗\n", ConsoleColor.Cyan);
            WriteColor("║   CustomRP CLI  —  Discord Rich RP     ║\n", ConsoleColor.Cyan);
            WriteColor("╚════════════════════════════════════════╝\n", ConsoleColor.Cyan);
            Console.WriteLine();

            // ステータス
            Console.Write("  ステータス: ");
            (Console.ForegroundColor, string label) = rpc!.State switch
            {
                RpcState.Connected  => (ConsoleColor.Green,  statusText),
                RpcState.Connecting => (ConsoleColor.Yellow, statusText),
                RpcState.Error      => (ConsoleColor.Red,    statusText),
                _                   => (ConsoleColor.Gray,   statusText),
            };
            Console.WriteLine(label);
            Console.ResetColor();
            Console.WriteLine();

            // 設定サマリー
            PrintField("Application ID", string.IsNullOrEmpty(config.Id) ? "(デフォルト)" : config.Id);
            PrintField("タイプ",    TypeName(config.Type));
            if (!string.IsNullOrEmpty(config.Name))
                PrintField("ゲーム名",   config.Name);
            PrintField("Details",  config.Details);
            PrintField("State",    config.State);
            if (!string.IsNullOrEmpty(config.LargeKey))   PrintField("大画像",   config.LargeKey);
            if (!string.IsNullOrEmpty(config.SmallKey))   PrintField("小画像",   config.SmallKey);
            if (!string.IsNullOrEmpty(config.Button1Text))PrintField("ボタン1",  $"{config.Button1Text}  {config.Button1Url}");
            if (!string.IsNullOrEmpty(config.Button2Text))PrintField("ボタン2",  $"{config.Button2Text}  {config.Button2Url}");

            // メニュー
            Console.WriteLine();
            WriteColor("  ─────────────────────────────────────────\n", ConsoleColor.DarkGray);
            bool connected = rpc.State == RpcState.Connected;
            MenuKey('c', "接続",          !connected);
            MenuKey('d', "切断",           connected);
            MenuKey('u', "プレゼンス更新", connected);
            Console.WriteLine();
            MenuKey('s', "設定を編集",    true);
            MenuKey('p', "プリセット読込", true);
            MenuKey('P', "プリセット保存", true);
            MenuKey('q', "終了",          true);
            Console.WriteLine();
        }

        // ── 設定エディタ ──────────────────────────────────────────────────────
        static void EditSettings()
        {
            while (true)
            {
                Console.Clear();
                WriteColor("  ── 設定を編集 ──\n\n", ConsoleColor.Cyan);

                var fields = new (char Key, string Label, string Value, string Field, string Desc)[]
                {
                    ('1', "Application ID",  config.Id,           "id",          "Discord Developer Portal のアプリ ID。空欄 = CustomRP デフォルト ID"),
                    ('2', "タイプ",          TypeName(config.Type),"type",        "0=Playing  1=Streaming  2=Listening  3=Watching  5=Competing"),
                    ('3', "ゲーム名 (Name)", config.Name,         "name",        "「〇〇をプレイ中」の名前部分。未設定 = Discord アプリ名をそのまま表示"),
                    ('4', "Details",        config.Details,      "details",     "プレゼンスの1行目（最大 128 文字）"),
                    ('5', "State",          config.State,        "state",       "プレゼンスの2行目（最大 128 文字）"),
                    ('6', "大画像キー",      config.LargeKey,    "largeKey",    "Developer Portal の画像キー名、または画像の直接 URL"),
                    ('7', "大画像テキスト", config.LargeText,    "largeText",   "大画像にカーソルを当てると表示されるテキスト（最大 128 文字）"),
                    ('8', "小画像キー",      config.SmallKey,    "smallKey",    "大画像の右下に重なる小さな画像のキーまたは URL"),
                    ('9', "小画像テキスト", config.SmallText,    "smallText",   "小画像のホバーテキスト（最大 128 文字）"),
                    ('A', "ボタン1 テキスト",config.Button1Text, "button1Text", "ボタンに表示するラベル（最大 32 文字）。URL も設定すると表示される"),
                    ('B', "ボタン1 URL",    config.Button1Url,  "button1Url",  "ボタンをクリックで開く URL（https:// が必要）"),
                    ('C', "ボタン2 テキスト",config.Button2Text, "button2Text", "2つ目のボタンのラベル（最大 32 文字）"),
                    ('D', "ボタン2 URL",    config.Button2Url,  "button2Url",  "2つ目のボタンの URL"),
                    ('E', "パイプ番号",     config.Pipe == -1 ? "自動" : config.Pipe.ToString(), "pipe",        "Discord の接続パイプ番号。通常は「自動」(-1)でOK"),
                    ('F', "タイムスタンプ", ((TimestampType)config.Timestamps).ToString(), "timestamps",  "経過時間の表示方法（0〜4）。0=最終接続から 1=起動から 2=現地時刻 4=更新から"),
                    ('G', "自動接続",       config.AutoConnect ? "有効" : "無効", "autoConnect", "起動時に自動で Discord に接続する（有効 / 無効）"),
                };

                foreach (var (k, lbl, val, _, desc) in fields)
                {
                    WriteColor($"  [{k}] ", ConsoleColor.DarkCyan);
                    Console.Write(PadRight(lbl, 16) + ": ");
                    WriteColor(string.IsNullOrEmpty(val) ? "(未設定)" : val, ConsoleColor.White);
                    Console.WriteLine();
                    WriteColor($"      {desc}\n", ConsoleColor.DarkGray);
                }

                Console.WriteLine();
                WriteColor("  [S] 保存して戻る   [Q] 保存せずに戻る  > ", ConsoleColor.DarkGray);

                char ch = char.ToUpper(Console.ReadKey(true).KeyChar);
                Console.WriteLine();

                if (ch == 'Q') break;
                if (ch == 'S') { config.Save(configPath); rpc?.UpdateConfig(config); break; }

                var match = fields.FirstOrDefault(f => f.Key == ch);
                if (match == default) continue;

                EditField(match.Field, match.Label);
                rpc?.UpdateConfig(config);
            }
        }

        static void EditField(string fieldKey, string label)
        {
            Console.Write($"  新しい {label}: ");
            string? input = Console.ReadLine();
            if (input is null) return;
            switch (fieldKey)
            {
                case "id":          config.Id         = input; break;
                case "type":        ParseType(input); break;
                case "name":        config.Name       = input; break;
                case "details":     config.Details    = input; break;
                case "state":       config.State      = input; break;
                case "largeKey":    config.LargeKey   = input; break;
                case "largeText":   config.LargeText  = input; break;
                case "smallKey":    config.SmallKey   = input; break;
                case "smallText":   config.SmallText  = input; break;
                case "button1Text": config.Button1Text= input; break;
                case "button1Url":  config.Button1Url = input; break;
                case "button2Text": config.Button2Text= input; break;
                case "button2Url":  config.Button2Url = input; break;
                case "pipe":
                    config.Pipe = input is "auto" or "自動" or "-1"
                        ? -1 : int.TryParse(input, out int p) ? p : config.Pipe;
                    break;
                case "timestamps":
                    if (int.TryParse(input, out int ts)) config.Timestamps = ts;
                    else if (Enum.TryParse<TimestampType>(input, true, out var tt)) config.Timestamps = (int)tt;
                    break;
                case "autoConnect":
                    config.AutoConnect = input is "1" or "true" or "yes" or "有効" or "y";
                    break;
            }
        }

        // ── プリセット I/O ─────────────────────────────────────────────────────
        static void LoadPreset()
        {
            Console.Clear();
            WriteColor("  ── プリセット読込 ──\n\n", ConsoleColor.Cyan);
            Console.Write("  .crp ファイルのパス: ");
            string? path = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(path)) return;

            var preset = Preset.Load(path);
            if (preset is null)
            {
                WriteColor("  読み込みに失敗しました。\n", ConsoleColor.Red);
                Pause(1500); return;
            }
            preset.Value.ApplyTo(config);
            config.Save(configPath);
            rpc?.UpdateConfig(config);
            WriteColor("  プリセットを適用しました。\n", ConsoleColor.Green);
            Pause(1000);
        }

        static void SavePreset()
        {
            Console.Clear();
            WriteColor("  ── プリセット保存 ──\n\n", ConsoleColor.Cyan);
            Console.Write("  保存先パス (.crp): ");
            string? path = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(path)) return;
            if (!path.EndsWith(".crp", StringComparison.OrdinalIgnoreCase)) path += ".crp";

            bool ok = Preset.FromConfig(config).Save(path);
            WriteColor(ok ? $"  保存しました: {path}\n" : "  保存に失敗しました。\n",
                       ok ? ConsoleColor.Green : ConsoleColor.Red);
            Pause(1200);
        }

        // ── ヘルプテキスト ────────────────────────────────────────────────────
        static void PrintHelp() => Console.WriteLine(@"
使い方: customrp [オプション]

オプション:
  --config <path>         設定ファイルのパス (既定: ~/.config/customrp/config.json)
  --preset <path>         .crp プリセットファイルを読み込む（Windows 版と互換）

  --id <id>               Discord Application ID
  --type <0-5|name>       タイプ: 0=Playing 1=Streaming 2=Listening 3=Watching 5=Competing
  --details <text>        Details テキスト
  --state <text>          State テキスト
  --large-image <key>     大画像キー / URL
  --large-text <text>     大画像ツールチップ
  --small-image <key>     小画像キー / URL
  --small-text <text>     小画像ツールチップ
  --button1-text <text>   ボタン1 ラベル
  --button1-url <url>     ボタン1 URL
  --button2-text <text>   ボタン2 ラベル
  --button2-url <url>     ボタン2 URL
  --pipe <-1..9>          Discord パイプ番号 (-1 = 自動)

  --connect, -c           起動時に自動接続
  --daemon                バックグラウンドで実行（UI なし）
  --version, -v           バージョンを表示
  --help, -h              このヘルプを表示

使用例:
  customrp --id 123456789 --details ""作業中"" --state ""集中"" --connect
  customrp --preset ~/my_game.crp --daemon
  customrp                              （対話モードで起動）

設定ファイル: ~/.config/customrp/config.json
ログ:         ~/.config/customrp/logs/
");

        // ── ユーティリティ ────────────────────────────────────────────────────

        /// <summary>
        /// Bug3修正: 全角文字（CJK）は端末幅 2 を占めるため、パディング計算を表示幅ベースにする。
        /// </summary>
        static int DisplayWidth(string s) =>
            s.Sum(c => c >= 0x1100 &&
                (c <= 0x115F || c == 0x2329 || c == 0x232A ||
                 (c >= 0x2E80 && c <= 0x303E) ||
                 (c >= 0x3040 && c <= 0xA4CF) ||
                 (c >= 0xAC00 && c <= 0xD7A3) ||
                 (c >= 0xF900 && c <= 0xFAFF) ||
                 (c >= 0xFE10 && c <= 0xFE19) ||
                 (c >= 0xFE30 && c <= 0xFE6F) ||
                 (c >= 0xFF00 && c <= 0xFF60) ||
                 (c >= 0xFFE0 && c <= 0xFFE6)) ? 2 : 1);

        static string PadRight(string s, int width)
        {
            int pad = width - DisplayWidth(s);
            return pad > 0 ? s + new string(' ', pad) : s;
        }

        static void PrintField(string label, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            WriteColor($"  {PadRight(label, 14)}: ", ConsoleColor.DarkGray);
            Console.WriteLine(value.Length > 58 ? value[..55] + "…" : value);
        }

        static void MenuKey(char key, string label, bool enabled)
        {
            // ラベル幅を全角対応で固定（最長ラベル「プレゼンス更新」= 表示幅 12）
            string padded = PadRight(label, 10);
            if (enabled)
            {
                WriteColor($"  [{key}] ", ConsoleColor.White);
                Console.Write($"{padded}  ");
            }
            else
            {
                WriteColor($"  [{key}] {padded}  ", ConsoleColor.DarkGray);
            }
        }

        static void WriteColor(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ResetColor();
        }

        static void Pause(int ms) => Thread.Sleep(ms);

        /// <summary>タイプ番号を表示名に変換（DiscordRPC.ActivityType enum は NuGet 版に存在しないので手動マッピング）。</summary>
        static string TypeName(int t) => t switch
        {
            0 => "Playing (0)",
            1 => "Streaming (1)",
            2 => "Listening (2)",
            3 => "Watching (3)",
            5 => "Competing (5)",
            _ => $"Unknown ({t})",
        };

        /// <summary>タイプ文字列をパースして config.Type に設定する。</summary>
        static void ParseType(string s)
        {
            if (int.TryParse(s, out int n)) { config.Type = n; return; }
            config.Type = s.ToLowerInvariant() switch
            {
                "playing"   => 0,
                "streaming" => 1,
                "listening" => 2,
                "watching"  => 3,
                "competing" => 5,
                _           => config.Type,
            };
        }
    }
}
