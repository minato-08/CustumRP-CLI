using System.Buffers.Binary;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CustomRPC.CLI
{
    // ── 公開データ型 ────────────────────────────────────────────────────────────

    public record DiscordUser(string Id, string Username);

    /// <summary>
    /// Discord に送信するアクティビティ情報。
    /// NuGet 版では使えなかった Name / Type を含む完全版。
    /// </summary>
    public sealed class DiscordActivity
    {
        /// <summary>0=Playing 1=Streaming 2=Listening 3=Watching 5=Competing</summary>
        public int Type { get; set; }

        /// <summary>「〇〇をプレイ中」の〇〇部分を上書きする名前。空なら Discord アプリ名がそのまま使われる。</summary>
        public string? Name { get; set; }

        public string? Details { get; set; }
        public string? State   { get; set; }

        public long? TimestampStart { get; set; }
        public long? TimestampEnd   { get; set; }

        public string? LargeImage { get; set; }
        public string? LargeText  { get; set; }
        public string? SmallImage { get; set; }
        public string? SmallText  { get; set; }

        public int PartySize { get; set; }
        public int PartyMax  { get; set; }

        public (string Label, string Url)[]? Buttons { get; set; }
    }

    // ── IPC クライアント ────────────────────────────────────────────────────────

    /// <summary>
    /// Discord IPC プロトコルの自前実装。
    /// Windows: 名前付きパイプ  macOS/Linux: Unix ドメインソケット
    /// </summary>
    public sealed class DiscordIpcClient : IDisposable
    {
        private readonly string _clientId;
        private readonly int    _pipeNumber;

        private Stream?  _stream;
        private Thread?  _readThread;
        private volatile bool _running;
        private int _nonceCounter;

        // ── 公開プロパティ ──────────────────────────────────────────────────────
        public bool        IsConnected { get; private set; }
        public DiscordUser? CurrentUser { get; private set; }

        // ── イベント ────────────────────────────────────────────────────────────
        public event Action<DiscordUser>? OnReady;
        public event Action?              OnDisconnected;
        public event Action<int, string>? OnError;
        public event Action<string>?      OnLog;

        // ── コンストラクタ ──────────────────────────────────────────────────────
        public DiscordIpcClient(string clientId, int pipeNumber = -1)
        {
            _clientId   = clientId;
            _pipeNumber = pipeNumber;
        }

        // ── 接続 ────────────────────────────────────────────────────────────────
        public bool Connect()
        {
            try
            {
                _stream = OpenPipe();
                if (_stream is null) return false;

                // ハンドシェイク（opcode 0）
                SendPacket(0, new JsonObject { ["v"] = 1, ["client_id"] = _clientId });

                _running = true;
                _readThread = new Thread(ReadLoop)
                {
                    IsBackground = true,
                    Name = "DiscordIPC-Read"
                };
                _readThread.Start();
                return true;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Connect error: {ex.Message}");
                return false;
            }
        }

        // ── プレゼンス操作 ──────────────────────────────────────────────────────
        public void SetActivity(DiscordActivity a)
        {
            SendFrame("SET_ACTIVITY", new JsonObject
            {
                ["pid"]      = Environment.ProcessId,
                ["activity"] = BuildActivity(a),
            });
        }

        public void ClearActivity()
        {
            SendFrame("SET_ACTIVITY", new JsonObject
            {
                ["pid"] = Environment.ProcessId,
            });
        }

        // ── 切断 ────────────────────────────────────────────────────────────────
        public void Disconnect()
        {
            _running = false;
            try { _stream?.Close(); } catch { }
            IsConnected = false;
        }

        public void Dispose() => Disconnect();

        // ────────────────────────────────────────────────────────────────────────
        // パイプ / ソケット検索
        // ────────────────────────────────────────────────────────────────────────
        private Stream? OpenPipe()
        {
            int lo = _pipeNumber >= 0 ? _pipeNumber : 0;
            int hi = _pipeNumber >= 0 ? _pipeNumber : 9;

            for (int i = lo; i <= hi; i++)
            {
                var s = TryOpenPipe(i);
                if (s is not null)
                {
                    OnLog?.Invoke($"Discord IPC パイプ #{i} に接続しました");
                    return s;
                }
            }
            return null;
        }

        private Stream? TryOpenPipe(int n)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var p = new NamedPipeClientStream(".", $"discord-ipc-{n}",
                        PipeDirection.InOut, PipeOptions.None);
                    p.Connect(500);
                    return p;
                }
                catch { return null; }
            }
            else
            {
                foreach (var dir in GetUnixDirs())
                {
                    var path = Path.Combine(dir, $"discord-ipc-{n}");
                    try
                    {
                        var sock = new Socket(
                            AddressFamily.Unix,
                            SocketType.Stream,
                            ProtocolType.Unspecified);
                        sock.Connect(new UnixDomainSocketEndPoint(path));
                        return new NetworkStream(sock, ownsSocket: true);
                    }
                    catch { }
                }
                return null;
            }
        }

        private static IEnumerable<string> GetUnixDirs()
        {
            // macOS: $TMPDIR は /var/folders/…/T/ などユーザー固有のディレクトリ
            var tmpdir = Environment.GetEnvironmentVariable("TMPDIR")?.TrimEnd('/');
            if (!string.IsNullOrEmpty(tmpdir)) yield return tmpdir;

            // Linux: $XDG_RUNTIME_DIR
            var xdg = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            if (!string.IsNullOrEmpty(xdg)) yield return xdg;

            yield return "/tmp";
            yield return "/var/run";
        }

        // ────────────────────────────────────────────────────────────────────────
        // 受信ループ
        // ────────────────────────────────────────────────────────────────────────
        private void ReadLoop()
        {
            try
            {
                while (_running && _stream is not null)
                {
                    var header = ReadExact(8);
                    if (header is null) break;

                    int opcode = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0, 4));
                    int length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4));

                    var body = ReadExact(length);
                    if (body is null) break;

                    HandlePacket(opcode, Encoding.UTF8.GetString(body));
                }
            }
            catch (Exception ex)
            {
                if (_running) OnLog?.Invoke($"受信エラー: {ex.Message}");
            }

            IsConnected = false;
            if (_running)
            {
                _running = false;
                OnDisconnected?.Invoke();
            }
        }

        private void HandlePacket(int opcode, string json)
        {
            OnLog?.Invoke($"← op={opcode} {json}");

            // opcode 2 = Close（Discord 側からの切断）
            if (opcode == 2)
            {
                using var doc = JsonDocument.Parse(json);
                int    code = doc.RootElement.TryGetProperty("code",    out var c) ? c.GetInt32()    : 0;
                string msg  = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString()!  : "";
                OnError?.Invoke(code, msg);
                Disconnect();
                OnDisconnected?.Invoke();
                return;
            }

            if (opcode != 1) return; // Ping/Pong 等は無視

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            string? evt = root.TryGetProperty("evt", out var evtEl) ? evtEl.GetString() : null;

            if (evt == "READY" && root.TryGetProperty("data", out var data))
            {
                IsConnected = true;
                if (data.TryGetProperty("user", out var userEl))
                {
                    var id = userEl.TryGetProperty("id",       out var idEl) ? idEl.GetString() ?? "" : "";
                    var un = userEl.TryGetProperty("username", out var unEl) ? unEl.GetString() ?? "" : "";
                    CurrentUser = new DiscordUser(id, un);
                    OnReady?.Invoke(CurrentUser);
                }
            }
            else if (evt == "ERROR" && root.TryGetProperty("data", out var errData))
            {
                int    code = errData.TryGetProperty("code",    out var c) ? c.GetInt32()   : 0;
                string msg  = errData.TryGetProperty("message", out var m) ? m.GetString()! : "";
                OnError?.Invoke(code, msg);
            }
        }

        // ────────────────────────────────────────────────────────────────────────
        // 送信ヘルパー
        // ────────────────────────────────────────────────────────────────────────
        private void SendPacket(int opcode, JsonNode payload)
        {
            if (_stream is null) throw new InvalidOperationException("未接続");

            byte[] body   = Encoding.UTF8.GetBytes(payload.ToJsonString());
            byte[] header = new byte[8];
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0, 4), opcode);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4, 4), body.Length);

            OnLog?.Invoke($"→ op={opcode} {Encoding.UTF8.GetString(body)}");

            lock (this)
            {
                _stream.Write(header);
                _stream.Write(body);
                _stream.Flush();
            }
        }

        private void SendFrame(string cmd, JsonObject args)
        {
            string nonce = Interlocked.Increment(ref _nonceCounter).ToString();
            SendPacket(1, new JsonObject
            {
                ["cmd"]   = cmd,
                ["args"]  = args,
                ["nonce"] = nonce,
            });
        }

        // ────────────────────────────────────────────────────────────────────────
        // アクティビティ JSON 構築
        // ────────────────────────────────────────────────────────────────────────
        private static JsonObject BuildActivity(DiscordActivity a)
        {
            var obj = new JsonObject { ["type"] = a.Type };

            // name を設定すると Discord 側のアプリ名を上書き表示できる
            if (!string.IsNullOrEmpty(a.Name))    obj["name"]    = a.Name;
            if (!string.IsNullOrEmpty(a.Details)) obj["details"] = a.Details;
            if (!string.IsNullOrEmpty(a.State))   obj["state"]   = a.State;

            // タイムスタンプ
            if (a.TimestampStart.HasValue || a.TimestampEnd.HasValue)
            {
                var ts = new JsonObject();
                if (a.TimestampStart.HasValue) ts["start"] = a.TimestampStart.Value;
                if (a.TimestampEnd.HasValue)   ts["end"]   = a.TimestampEnd.Value;
                obj["timestamps"] = ts;
            }

            // 画像アセット
            bool hasAssets = !string.IsNullOrEmpty(a.LargeImage) || !string.IsNullOrEmpty(a.LargeText)
                          || !string.IsNullOrEmpty(a.SmallImage) || !string.IsNullOrEmpty(a.SmallText);
            if (hasAssets)
            {
                var assets = new JsonObject();
                if (!string.IsNullOrEmpty(a.LargeImage)) assets["large_image"] = a.LargeImage;
                if (!string.IsNullOrEmpty(a.LargeText))  assets["large_text"]  = a.LargeText;
                if (!string.IsNullOrEmpty(a.SmallImage)) assets["small_image"] = a.SmallImage;
                if (!string.IsNullOrEmpty(a.SmallText))  assets["small_text"]  = a.SmallText;
                obj["assets"] = assets;
            }

            // パーティ
            if (a.PartySize > 0 && a.PartyMax > 0)
            {
                obj["party"] = new JsonObject
                {
                    ["id"]   = "party",
                    ["size"] = new JsonArray(a.PartySize, a.PartyMax),
                };
            }

            // ボタン
            if (a.Buttons?.Length > 0)
            {
                var btns = new JsonArray();
                foreach (var (label, url) in a.Buttons)
                    btns.Add(new JsonObject { ["label"] = label, ["url"] = url });
                obj["buttons"] = btns;
            }

            return obj;
        }

        // ────────────────────────────────────────────────────────────────────────
        // IO ヘルパー
        // ────────────────────────────────────────────────────────────────────────
        private byte[]? ReadExact(int count)
        {
            var buf    = new byte[count];
            int offset = 0;
            while (offset < count && _stream is not null)
            {
                int read = _stream.Read(buf, offset, count - offset);
                if (read == 0) return null;
                offset += read;
            }
            return buf;
        }
    }
}
