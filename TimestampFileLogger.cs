using DiscordRPC.Logging;

namespace CustomRPC.CLI
{
    /// <summary>
    /// Logs Discord RPC messages to dated files in the config logs directory.
    /// Ported from the original Windows-only version — no changes needed.
    /// </summary>
    public class TimestampFileLogger : ILogger
    {
        public LogLevel Level { get; set; }

        private readonly string logsPath;
        private readonly object fileLock = new();

        public TimestampFileLogger(string path, LogLevel level = LogLevel.Warning)
        {
            Level = level;
            logsPath = path;
            Directory.CreateDirectory(path);
        }

        private void Write(string tag, LogLevel logLevel, string message, object[] args)
        {
            if (Level > logLevel) return;
            string line = $"[{DateTime.Now:HH:mm:ss}] {tag}: {(args.Length > 0 ? string.Format(message, args) : message)}";
            string file = Path.Combine(logsPath, $"{DateTime.Now:yyyy-MM-dd}.log");
            lock (fileLock)
            {
                try { File.AppendAllText(file, line + Environment.NewLine); }
                catch { /* best-effort */ }
            }
        }

        public void Trace(string message, params object[] args)   => Write("TRACE", LogLevel.Trace,   message, args);
        public void Info(string message, params object[] args)    => Write(" INFO", LogLevel.Info,    message, args);
        public void Warning(string message, params object[] args) => Write(" WARN", LogLevel.Warning, message, args);
        public void Error(string message, params object[] args)   => Write("ERROR", LogLevel.Error,   message, args);
    }
}
