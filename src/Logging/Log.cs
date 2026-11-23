namespace Loupedeck.MXTeamViewerPlugin
{
    using System;
    using System.IO;
    using System.Text.RegularExpressions;

    public enum LogLevel { Debug = 0, Info = 1, Warning = 2, Error = 3 }

    // Plugin log: %LOCALAPPDATA%\MXTeamViewerPlugin\logs\plugin-YYYYMMDD.log (7 days kept),
    // also forwarded to the Logi Plugin Service log. Every line goes through Redact(): anything that
    // looks like a password, token or bearer header is masked before it is written anywhere.
    internal static class TvLog
    {
        private static readonly object Sync = new();
        private static PluginLogFile _service;
        private static string _dir;

        public static LogLevel MinimumLevel { get; set; } = LogLevel.Info;

        public static string Directory => _dir;

        public static void Init(PluginLogFile service)
        {
            _service = service;
            _dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MXTeamViewerPlugin", "logs");
            try
            {
                System.IO.Directory.CreateDirectory(_dir);
                foreach (var f in System.IO.Directory.GetFiles(_dir, "plugin-*.log"))
                {
                    if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-7))
                    {
                        File.Delete(f);
                    }
                }
            }
            catch
            {
                // Logging must never break the plugin.
            }
        }

        public static void Debug(string text) => Write(LogLevel.Debug, text, null);

        public static void Info(string text) => Write(LogLevel.Info, text, null);

        public static void Warning(string text, Exception ex = null) => Write(LogLevel.Warning, text, ex);

        public static void Error(string text, Exception ex = null) => Write(LogLevel.Error, text, ex);

        private static readonly Regex Secrets = new(
            @"(?i)(\b(?:password|passwd|pwd|token|secret|bearer|authorization)\b|(?<!\S)--?p(?:assword)?(?=\s))(\s*[:=]?\s*)(""[^""]*""|\S+)",
            RegexOptions.Compiled);

        public static string Redact(string text) =>
            string.IsNullOrEmpty(text) ? text : Secrets.Replace(text, m => m.Groups[1].Value + m.Groups[2].Value + "***");

        private static void Write(LogLevel level, string text, Exception ex)
        {
            if (level < MinimumLevel)
            {
                return;
            }
            var safe = Redact(text) + (ex == null ? "" : " | " + ex.GetType().Name + ": " + Redact(ex.Message));
            try
            {
                switch (level)
                {
                    case LogLevel.Error: _service?.Error(safe); break;
                    case LogLevel.Warning: _service?.Warning(safe); break;
                    case LogLevel.Info: _service?.Info(safe); break;
                    default: _service?.Verbose(safe); break;
                }
                if (_dir == null)
                {
                    return;
                }
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level.ToString().ToUpperInvariant()}] {safe}{Environment.NewLine}";
                lock (Sync)
                {
                    File.AppendAllText(Path.Combine(_dir, $"plugin-{DateTime.Now:yyyyMMdd}.log"), line);
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}
