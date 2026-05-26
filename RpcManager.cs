using DiscordRPC;
using DiscordRPC.Logging;
using System.Text.RegularExpressions;
using DButton = DiscordRPC.Button;

namespace CustomRPC.CLI
{
    /// <summary>
    /// タイムスタンプの表示モード（元の Windows 版と同じ定義）。
    /// </summary>
    public enum TimestampType
    {
        SinceLastConnection = 0,
        SinceStartup = 1,
        LocalTime = 2,
        Custom = 3,
        SincePresenceUpdate = 4,
    }

    public enum RpcState
    {
        Disconnected,
        Connecting,
        Connected,
        Error,
    }

    /// <summary>
    /// Discord Rich Presence の接続・プレゼンス管理クラス。
    /// 元の MainForm.cs のコアロジックを移植（UI 呼び出しをイベントに置換）。
    ///
    /// ※ NuGet 版 DiscordRichPresence (1.2.1.24) は Windows 版のローカル DLL より機能が少ないため、
    ///    ActivityType / StatusDisplay / DetailsUrl / StateUrl / LargeImageUrl は非対応。
    ///    これらの値は Config/Preset に保存されるので Windows 版プリセットとの互換性は維持される。
    /// </summary>
    public sealed class RpcManager : IDisposable
    {
        // ── フィールド ──────────────────────────────────────────────────────
        private DiscordRpcClient? client;
        private Config config;

        private readonly DateTime startedAt = DateTime.UtcNow;
        private DateTime connectedAt = DateTime.UtcNow;

        private readonly System.Timers.Timer retryTimer = new(10_000) { AutoReset = false };
        private int retryLeft = 30;

        /// <summary>Application ID が空のときに使うデフォルト値（元のアプリと同じ）。</summary>
        private const string DefaultId = "896771305108553788";

        // ── 公開プロパティ・イベント ─────────────────────────────────────────
        public RpcState State { get; private set; } = RpcState.Disconnected;

        public event Action<string>? OnStatus;       // ステータスメッセージ変化
        public event Action<string>? OnUserReady;    // 接続完了（ユーザー名を渡す）
        public event Action? OnPresenceUpdated;      // プレゼンス送信 ACK
        public event Action? OnDisconnected;

        // ── コンストラクタ ───────────────────────────────────────────────────
        public RpcManager(Config config)
        {
            this.config = config;
            retryTimer.Elapsed += (_, _) =>
            {
                if (retryLeft > 0) { retryLeft--; Connect(); }
                else retryLeft = 30;
            };
        }

        public void UpdateConfig(Config newConfig) => config = newConfig;

        // ── 接続 / 切断 ──────────────────────────────────────────────────────
        public bool Connect()
        {
            if (client is { IsDisposed: false }) client.Dispose();

            string id = string.IsNullOrEmpty(config.Id) ? DefaultId : config.Id;
            var logger = new TimestampFileLogger(Config.DefaultLogsPath, LogLevel.Warning);

            // パイプ番号を指定する場合は 5 引数コンストラクタを使う
            client = new DiscordRpcClient(id, config.Pipe, logger, autoEvents: true, client: null);

            client.OnReady            += HandleReady;
            client.OnPresenceUpdate   += HandlePresenceUpdate;
            client.OnError            += HandleError;
            client.OnConnectionFailed += HandleConnectionFailed;

            State = RpcState.Connecting;
            OnStatus?.Invoke("接続中...");

            bool ok = client.Initialize();
            if (!ok)
            {
                State = RpcState.Error;
                OnStatus?.Invoke("初期化失敗（Discord が起動しているか確認してください）");
            }
            return ok;
        }

        public void Disconnect()
        {
            retryTimer.Stop();
            retryLeft = 30;

            if (client is { IsDisposed: false })
            {
                client.ClearPresence();
                client.Dispose();
            }

            State = RpcState.Disconnected;
            OnStatus?.Invoke("切断済み");
            OnDisconnected?.Invoke();
        }

        // ── Discord イベントハンドラ ──────────────────────────────────────────
        private void HandleReady(object sender, DiscordRPC.Message.ReadyMessage args)
        {
            connectedAt = DateTime.UtcNow;
            State = RpcState.Connected;
            string user = args.User.Username;
            OnUserReady?.Invoke(user);
            OnStatus?.Invoke($"接続済み ({user})");
            SetPresence();
        }

        private void HandlePresenceUpdate(object sender, DiscordRPC.Message.PresenceMessage args)
        {
            State = RpcState.Connected;
            retryTimer.Stop();
            OnPresenceUpdated?.Invoke();
        }

        private void HandleError(object sender, DiscordRPC.Message.ErrorMessage args)
        {
            State = RpcState.Error;
            OnStatus?.Invoke($"エラー: {args.Message}");
            retryTimer.Start();
        }

        private void HandleConnectionFailed(object sender, DiscordRPC.Message.ConnectionFailedMessage args)
        {
            State = RpcState.Error;
            OnStatus?.Invoke("接続失敗（Discord が起動しているか確認してください）");
            retryTimer.Start();
        }

        // ── プレゼンス設定 ────────────────────────────────────────────────────
        /// <summary>
        /// 現在の Config 内容を Discord に送信する。
        /// 元の MainForm.SetPresence() から移植。
        /// </summary>
        public bool SetPresence()
        {
            if (client is null or { IsDisposed: true }) return false;

            // Discord はテキストの先頭空白を消すので ZWS を前置して保持する（元と同じ処理）
            const string NBSP = " ";
            const string ZWS  = "​";

            string FixLeadingSpace(string text, int max)
            {
                if (text.StartsWith(NBSP) && text.Length < max) return ZWS + text;
                if (text.StartsWith(NBSP + NBSP))               return ZWS + text[1..];
                return text;
            }

            string details = FixLeadingSpace(config.Details.Trim(), 128);
            string state   = FixLeadingSpace(config.State.Trim(),   128);

            if (config.PartySize > config.PartyMax) config.PartyMax = config.PartySize;

            // URL 正規化（元の ProcessURL と同等）
            string NormalizeUrl(string url, int max)
            {
                if (string.IsNullOrWhiteSpace(url)) return "";
                if (!url.Contains("://")) url = "https://" + url;
                try
                {
                    if (Uri.TryCreate(url, UriKind.Absolute, out var u))
                        url = u.AbsoluteUri.Replace(u.Host, u.IdnHost);
                }
                catch { }
                return url[..Math.Min(max, url.Length)];
            }

            // Discord CDN URL をプロキシ経由に変換（元の Proxify と同等）
            string Proxify(string key)
                => Regex.Replace(key,
                    @"//((cdn)|(media))\.discordapp\.((com)|(net))/",
                    "//customrp.xyz/proxy/");

            // ── RichPresence 構築 ─────────────────────────────────────────
            // ※ NuGet 版ライブラリは Type / Name / StatusDisplay / DetailsUrl / StateUrl 非対応。
            //   プリセット保存時には Config に値は残るため Windows 版との互換性は保たれる。
            var rp = new RichPresence
            {
                Details = details,
                State   = state,
                Party   = (config.PartySize > 0 && config.PartyMax > 0)
                    ? new Party { ID = "CustomRP", Size = config.PartySize, Max = config.PartyMax }
                    : null,
            };

            // 画像アセット
            // ※ NuGet 版は LargeImageUrl / SmallImageUrl プロパティ非存在のため画像 URL は省略
            try
            {
                bool CheckMpLength(string url, string label)
                {
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return true;
                    string mp = $"mp:external/43 characters that probably represent an id/{Uri.EscapeDataString(u.Query)}/{u.Scheme}/{(u.IdnHost == "media.discordapp.net" ? "cdn.discordapp.com" : u.IdnHost)}{u.AbsolutePath}";
                    if (mp.Length > 256)
                    {
                        Console.Error.WriteLine($"[warn] {label}画像の URL が長すぎます（省略）。");
                        return false;
                    }
                    return true;
                }

                string largeKey = config.LargeKey.Trim();
                string smallKey = config.SmallKey.Trim();

                // URL 形式の画像キーは正規化してから検証
                if (Uri.TryCreate(largeKey, UriKind.Absolute, out var lu))
                {
                    if (!CheckMpLength(largeKey, "大")) largeKey = "";
                    else largeKey = lu.AbsoluteUri.Replace(lu.Host, lu.IdnHost);
                }
                if (Uri.TryCreate(smallKey, UriKind.Absolute, out var su))
                {
                    if (!CheckMpLength(smallKey, "小")) smallKey = "";
                    else smallKey = su.AbsoluteUri.Replace(su.Host, su.IdnHost);
                }

                bool hasAssets = !string.IsNullOrEmpty(largeKey) || !string.IsNullOrEmpty(config.LargeText)
                              || !string.IsNullOrEmpty(smallKey) || !string.IsNullOrEmpty(config.SmallText);

                if (hasAssets)
                {
                    rp.Assets = new Assets
                    {
                        LargeImageKey  = Proxify(largeKey),
                        LargeImageText = config.LargeText,
                        SmallImageKey  = Proxify(smallKey),
                        SmallImageText = config.SmallText,
                    };
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[warn] 画像設定エラー: {ex.Message}");
            }

            // ボタン
            var buttons = new List<DButton>();
            try
            {
                string b1Url = NormalizeUrl(config.Button1Url, 512);
                string b2Url = NormalizeUrl(config.Button2Url, 512);
                if (!string.IsNullOrEmpty(config.Button1Text) && !string.IsNullOrEmpty(b1Url))
                    buttons.Add(new DButton { Label = config.Button1Text, Url = b1Url });
                if (!string.IsNullOrEmpty(config.Button2Text) && !string.IsNullOrEmpty(b2Url))
                    buttons.Add(new DButton { Label = config.Button2Text, Url = b2Url });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[warn] ボタン URL エラー: {ex.Message}");
            }
            if (buttons.Count > 0) rp.Buttons = [.. buttons];

            // タイムスタンプ
            switch ((TimestampType)config.Timestamps)
            {
                case TimestampType.SinceLastConnection:
                    rp.Timestamps = new Timestamps { Start = connectedAt }; break;
                case TimestampType.SinceStartup:
                    rp.Timestamps = new Timestamps { Start = startedAt }; break;
                case TimestampType.SincePresenceUpdate:
                    rp.Timestamps = Timestamps.Now; break;
                case TimestampType.LocalTime:
                    rp.Timestamps = new Timestamps
                    {
                        Start = DateTime.UtcNow.Subtract(
                            new TimeSpan(DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second))
                    };
                    break;
                case TimestampType.Custom:
                    var start = config.CustomTimestamp.ToUniversalTime();
                    var end   = config.CustomTimestampEnd.ToUniversalTime();
                    rp.Timestamps = config.CustomTimestampEndEnabled
                        ? new Timestamps { Start = start, End = end }
                        : (start < DateTime.UtcNow
                            ? new Timestamps { Start = start }
                            : new Timestamps { Start = DateTime.UtcNow, End = start });
                    break;
            }

            try
            {
                client.SetPresence(rp);
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[error] プレゼンス送信エラー: {ex.Message}");
                return false;
            }
        }

        // ── IDisposable ──────────────────────────────────────────────────────
        public void Dispose()
        {
            retryTimer.Dispose();
            if (client is { IsDisposed: false }) client.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
