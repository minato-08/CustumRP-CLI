using System.Text.Json;
using System.Text.Json.Serialization;

namespace CustomRPC.CLI
{
    /// <summary>
    /// Application settings, stored as JSON at ~/.config/customrp/config.json.
    /// </summary>
    public class Config
    {
        // ── Connection ──────────────────────────────────────────────────────
        /// <summary>Discord Application ID. Leave empty to use the default CustomRP ID.</summary>
        public string Id { get; set; } = "";

        /// <summary>Discord pipe number (-1 = auto).</summary>
        public int Pipe { get; set; } = -1;

        /// <summary>Automatically connect when the app starts.</summary>
        public bool AutoConnect { get; set; } = false;

        // ── Activity ────────────────────────────────────────────────────────
        /// <summary>Activity type. 0=Playing 1=Streaming 2=Listening 3=Watching 5=Competing</summary>
        public int Type { get; set; } = 0;

        /// <summary>Status display type. 0=Name 1=Details 2=State</summary>
        public int Display { get; set; } = 0;

        /// <summary>Activity name shown in "Playing [Name]" etc.</summary>
        public string Name { get; set; } = "";

        // ── Details / State ─────────────────────────────────────────────────
        public string Details { get; set; } = "";
        public string DetailsUrl { get; set; } = "";
        public string State { get; set; } = "";
        public string StateUrl { get; set; } = "";

        // ── Party ───────────────────────────────────────────────────────────
        public int PartySize { get; set; } = 0;
        public int PartyMax { get; set; } = 0;

        // ── Timestamps ──────────────────────────────────────────────────────
        /// <summary>
        /// Timestamp mode.
        /// 0=SinceLastConnection 1=SinceStartup 2=LocalTime 3=Custom 4=SincePresenceUpdate
        /// </summary>
        public int Timestamps { get; set; } = 0;
        public DateTime CustomTimestamp { get; set; } = DateTime.Now;
        public bool CustomTimestampEndEnabled { get; set; } = false;
        public DateTime CustomTimestampEnd { get; set; } = DateTime.Now.AddHours(1);

        // ── Large image ─────────────────────────────────────────────────────
        public string LargeKey { get; set; } = "";
        public string LargeText { get; set; } = "";
        public string LargeUrl { get; set; } = "";

        // ── Small image ─────────────────────────────────────────────────────
        public string SmallKey { get; set; } = "";
        public string SmallText { get; set; } = "";
        public string SmallUrl { get; set; } = "";

        // ── Buttons ─────────────────────────────────────────────────────────
        public string Button1Text { get; set; } = "";
        public string Button1Url { get; set; } = "";
        public string Button2Text { get; set; } = "";
        public string Button2Url { get; set; } = "";

        // ── Paths ───────────────────────────────────────────────────────────
        [JsonIgnore]
        public static string DefaultConfigPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", "customrp", "config.json");

        [JsonIgnore]
        public static string DefaultLogsPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", "customrp", "logs");

        // ── I/O ─────────────────────────────────────────────────────────────
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };

        public static Config Load(string? path = null)
        {
            string p = path ?? DefaultConfigPath;
            if (!File.Exists(p)) return new Config();
            try
            {
                return JsonSerializer.Deserialize<Config>(File.ReadAllText(p), JsonOpts) ?? new Config();
            }
            catch
            {
                Console.Error.WriteLine($"Warning: Could not read config at {p}, using defaults.");
                return new Config();
            }
        }

        public void Save(string? path = null)
        {
            string p = path ?? DefaultConfigPath;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(p)!);
                File.WriteAllText(p, JsonSerializer.Serialize(this, JsonOpts));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Could not save config: {ex.Message}");
            }
        }
    }
}
