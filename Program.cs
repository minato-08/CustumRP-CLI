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
                var key = Console.ReadKey(true);
                switch (char.ToLower(key.KeyChar))
                {
                    case 'c': rpc!.Connect();    Pause(400); break;
                    case 'd': rpc!.Disconnect(); Pause(200); break;
                    case 'u':
                        if (rpc!.State == RpcState.Connected) { rpc.SetPresence(); Pause(300); }
                        break;
                    case 's': EditSettings(); break;
                    case 'p': LoadPreset();   break;
                    case 'P': SavePreset();   break;
                    case 'q': return;
                }
            }
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

                var fields = new (char Key, string Label, string Value, string Field)[]
                {
                    ('1', "Application ID", config.Id,           "id"),
                    ('2', "タイプ",          TypeName(config.Type),"type"),
                    ('3', "Details",        config.Details,      "details"),
                    ('4', "State",          config.State,        "state"),
                    ('5', "大画像キー",      config.LargeKey,    "largeKey"),
                    ('6', "大画像テキスト", config.LargeText,    "largeText"),
                    ('7', "小画像キー",      config.SmallKey,    "smallKey"),
                    ('8', "小画像テキスト", config.SmallText,    "smallText"),
                    ('9', "ボタン1 テキスト",config.Button1Text, "button1Text"),
                    ('A', "ボタン1 URL",    config.Button1Url,  "button1Url"),
                    ('B', "ボタン2 テキスト",config.Button2Text, "button2Text"),
                    ('C', "ボタン2 URL",    config.Button2Url,  "button2Url"),
                    ('D', "パイプ番号",     config.Pipe == -1 ? "自動" : config.Pipe.ToString(), "pipe"),
                    ('E', "タイムスタンプ", ((TimestampType)config.Timestamps).ToString(), "timestamps"),
                    ('F', "自動接続",       config.AutoConnect ? "有効" : "無効", "autoConnect"),
                };

                foreach (var (k, lbl, val, _) in fields)
                {
                    WriteColor($"  [{k}] ", ConsoleColor.DarkCyan);
                    Console.Write($"{lbl,-18}: ");
                    WriteColor(string.IsNullOrEmpty(val) ? "(未設定)" : val, ConsoleColor.White);
                    Console.WriteLine();
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
        static void PrintField(string label, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            WriteColor($"  {label,-14}: ", ConsoleColor.DarkGray);
            Console.WriteLine(value.Length > 58 ? value[..55] + "…" : value);
        }

        static void MenuKey(char key, string label, bool enabled)
        {
            if (enabled)
            {
                WriteColor($"  [{key}] ", ConsoleColor.White);
                Console.Write($"{label}   ");
            }
            else
            {
                WriteColor($"  [{key}] {label}   ", ConsoleColor.DarkGray);
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
