using System.Text.RegularExpressions;

namespace CustomRPC.CLI
{
    /// <summary>タイムスタンプの表示モード（元の Windows 版と同じ定義）。</summary>
    public enum TimestampType
    {
        SinceLastConnection  = 0,
        SinceStartup         = 1,
        LocalTime            = 2,
        Custom               = 3,
        SincePresenceUpdate  = 4,
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
    /// 自前の DiscordIpcClient を使用し、Name（ゲーム名上書き）や Type など
    /// NuGet 版ライブラリでは使えなかったフィールドを完全サポート。
    /// </summary>
    public sealed class RpcManager : IDisposable
    {
        // ── フィールド ──────────────────────────────────────────────────────────
        private DiscordIpcClient? _ipc;
        private Config _config;

        private readonly DateTime _startedAt   = DateTime.UtcNow;
        private          DateTime _connectedAt = DateTime.UtcNow;

        private readonly System.Timers.Timer _retryTimer = new(10_000) { AutoReset = false };
        private int _retryLeft = 30;

        /// <summary>Application ID が空のときに使うデフォルト値（元のアプリと同じ）。</summary>
        private const string DefaultId = "896771305108553788";

        // ── 公開プロパティ・イベント ────────────────────────────────────────────
        public RpcState State { get; private set; } = RpcState.Disconnected;

        public event Action<string>? OnStatus;        // ステータスメッセージ変化
        public event Action<string>? OnUserReady;     // 接続完了（ユーザー名を渡す）
        public event Action?         OnPresenceUpdated;
        public event Action?         OnDisconnected;

        // ── コンストラクタ ──────────────────────────────────────────────────────
        public RpcManager(Config config)
        {
            _config = config;
            _retryTimer.Elapsed += (_, _) =>
            {
                if (_retryLeft > 0) { _retryLeft--; Connect(); }
                else _retryLeft = 30;
            };
        }

        public void UpdateConfig(Config newConfig) => _config = newConfig;

        // ── 接続 ────────────────────────────────────────────────────────────────
        public bool Connect()
        {
            _ipc?.Disconnect();
            _ipc?.Dispose();

            string id     = string.IsNullOrEmpty(_config.Id) ? DefaultId : _config.Id;
            var    logger = new TimestampFileLogger(Config.DefaultLogsPath, LogLevel.Warning);

            _ipc = new DiscordIpcClient(id, _config.Pipe);

            _ipc.OnReady        += HandleReady;
            _ipc.OnDisconnected += HandleDisconnected;
            _ipc.OnError        += HandleError;
            _ipc.OnLog          += msg => logger.Trace(msg);

            State = RpcState.Connecting;
            OnStatus?.Invoke("接続中...");

            bool ok = _ipc.Connect();
            if (!ok)
            {
                State = RpcState.Error;
                OnStatus?.Invoke("接続失敗（Discord が起動しているか確認してください）");
                _retryTimer.Start();
            }
            return ok;
        }

        // ── 切断 ────────────────────────────────────────────────────────────────
        public void Disconnect()
        {
            _retryTimer.Stop();
            _retryLeft = 30;

            if (_ipc is not null)
            {
                _ipc.ClearActivity();
                _ipc.Disconnect();
            }

            State = RpcState.Disconnected;
            OnStatus?.Invoke("切断済み");
            OnDisconnected?.Invoke();
        }

        // ── Discord イベントハンドラ ────────────────────────────────────────────
        private void HandleReady(DiscordUser user)
        {
            _connectedAt = DateTime.UtcNow;
            State = RpcState.Connected;
            OnUserReady?.Invoke(user.Username);
            OnStatus?.Invoke($"接続済み ({user.Username})");
            SetPresence();
        }

        private void HandleDisconnected()
        {
            // 切断済み以外（= 予期しない切断）なら再接続を試みる
            if (State is RpcState.Connected or RpcState.Connecting or RpcState.Error)
            {
                State = RpcState.Error;
                OnStatus?.Invoke("切断されました。再接続を試みています...");
                _retryTimer.Start();
            }
        }

        private void HandleError(int code, string message)
        {
            State = RpcState.Error;
            OnStatus?.Invoke($"エラー ({code}): {message}");
            _retryTimer.Start();
        }

        // ── プレゼンス設定 ────────────────────────────────────────────────────────
        public bool SetPresence()
        {
            if (_ipc is null || !_ipc.IsConnected) return false;

            // Discord はテキストの先頭空白を消すので ZWS を前置して保持（元と同じ処理）
            const string NBSP = " ";
            const string ZWS  = "​";

            string FixLeadingSpace(string text, int max)
            {
                if (text.StartsWith(NBSP) && text.Length < max) return ZWS + text;
                if (text.StartsWith(NBSP + NBSP))               return ZWS + text[1..];
                return text;
            }

            string details = FixLeadingSpace(_config.Details.Trim(), 128);
            string state   = FixLeadingSpace(_config.State.Trim(),   128);

            if (_config.PartySize > _config.PartyMax) _config.PartyMax = _config.PartySize;

            // URL 正規化
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

            // Discord CDN を CustomRP プロキシ経由に変換（元の Proxify と同等）
            string Proxify(string key)
                => Regex.Replace(key,
                    @"//((cdn)|(media))\.discordapp\.((com)|(net))/",
                    "//customrp.xyz/proxy/");

            // ── DiscordActivity 構築 ──────────────────────────────────────────
            var activity = new DiscordActivity
            {
                Type    = _config.Type,
                Name    = string.IsNullOrEmpty(_config.Name) ? null : _config.Name,
                Details = string.IsNullOrEmpty(details) ? null : details,
                State   = string.IsNullOrEmpty(state)   ? null : state,
            };

            // パーティ
            if (_config.PartySize > 0 && _config.PartyMax > 0)
            {
                activity.PartySize = _config.PartySize;
                activity.PartyMax  = _config.PartyMax;
            }

            // 画像アセット
            try
            {
                bool CheckMpLength(string url, string label)
                {
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return true;
                    string mp = $"mp:external/43chars/{Uri.EscapeDataString(u.Query)}/{u.Scheme}/{u.IdnHost}{u.AbsolutePath}";
                    if (mp.Length > 256)
                    {
                        Console.Error.WriteLine($"[warn] {label}画像の URL が長すぎます（省略）。");
                        return false;
                    }
                    return true;
                }

                string largeKey = _config.LargeKey.Trim();
                string smallKey = _config.SmallKey.Trim();

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

                activity.LargeImage = string.IsNullOrEmpty(largeKey) ? null : Proxify(largeKey);
                activity.LargeText  = string.IsNullOrEmpty(_config.LargeText) ? null : _config.LargeText;
                activity.SmallImage = string.IsNullOrEmpty(smallKey) ? null : Proxify(smallKey);
                activity.SmallText  = string.IsNullOrEmpty(_config.SmallText) ? null : _config.SmallText;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[warn] 画像設定エラー: {ex.Message}");
            }

            // ボタン
            var buttons = new List<(string Label, string Url)>();
            try
            {
                string b1Url = NormalizeUrl(_config.Button1Url, 512);
                string b2Url = NormalizeUrl(_config.Button2Url, 512);
                if (!string.IsNullOrEmpty(_config.Button1Text) && !string.IsNullOrEmpty(b1Url))
                    buttons.Add((_config.Button1Text, b1Url));
                if (!string.IsNullOrEmpty(_config.Button2Text) && !string.IsNullOrEmpty(b2Url))
                    buttons.Add((_config.Button2Text, b2Url));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[warn] ボタン URL エラー: {ex.Message}");
            }
            if (buttons.Count > 0) activity.Buttons = [.. buttons];

            // タイムスタンプ
            static long ToUnix(DateTime dt) =>
                ((DateTimeOffset)dt.ToUniversalTime()).ToUnixTimeSeconds();

            long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            switch ((TimestampType)_config.Timestamps)
            {
                case TimestampType.SinceLastConnection:
                    activity.TimestampStart = ToUnix(_connectedAt); break;
                case TimestampType.SinceStartup:
                    activity.TimestampStart = ToUnix(_startedAt); break;
                case TimestampType.SincePresenceUpdate:
                    activity.TimestampStart = nowUnix; break;
                case TimestampType.LocalTime:
                    var now = DateTime.Now;
                    activity.TimestampStart = ToUnix(new DateTime(now.Year, now.Month, now.Day)); break;
                case TimestampType.Custom:
                    var start = _config.CustomTimestamp.ToUniversalTime();
                    var end   = _config.CustomTimestampEnd.ToUniversalTime();
                    if (_config.CustomTimestampEndEnabled)
                    {
                        activity.TimestampStart = ToUnix(start);
                        activity.TimestampEnd   = ToUnix(end);
                    }
                    else if (start < DateTime.UtcNow)
                    {
                        activity.TimestampStart = ToUnix(start);
                    }
                    else
                    {
                        activity.TimestampStart = nowUnix;
                        activity.TimestampEnd   = ToUnix(start);
                    }
                    break;
            }

            try
            {
                _ipc.SetActivity(activity);
                OnPresenceUpdated?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[error] プレゼンス送信エラー: {ex.Message}");
                return false;
            }
        }

        // ── IDisposable ──────────────────────────────────────────────────────────
        public void Dispose()
        {
            _retryTimer.Dispose();
            _ipc?.Disconnect();
            _ipc?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
