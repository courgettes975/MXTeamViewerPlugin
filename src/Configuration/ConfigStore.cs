namespace Loupedeck.MXTeamViewerPlugin
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.Encodings.Web;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    // Loads / creates config.json and reloads it automatically when the user edits it
    // (no need to restart Logi Options+).
    internal sealed class ConfigStore : IDisposable
    {
        private static readonly JsonSerializerOptions Json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };

        private FileSystemWatcher _watcher;
        private DateTime _lastReload;

        public ConfigStore()
        {
            this.Directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MXTeamViewerPlugin");
            this.FilePath = Path.Combine(this.Directory, "config.json");
        }

        public string Directory { get; }

        public string FilePath { get; }

        public bool CreatedOnThisRun { get; private set; }

        public PluginConfig Current { get; private set; } = new();

        public event Action Changed;

        public void Load()
        {
            System.IO.Directory.CreateDirectory(this.Directory);
            if (!File.Exists(this.FilePath))
            {
                this.Current = new PluginConfig();
                this.Save();
                this.CreatedOnThisRun = true;
                TvLog.Info("config.json created with defaults: " + this.FilePath);
            }
            else
            {
                this.Reload();
            }
            this.Watch();
        }

        public void Save()
        {
            File.WriteAllText(this.FilePath, JsonSerializer.Serialize(this.Current, Json));
        }

        private void Reload()
        {
            try
            {
                var text = File.ReadAllText(this.FilePath);
                var config = JsonSerializer.Deserialize<PluginConfig>(text, Json) ?? new PluginConfig();
                Normalize(config);
                this.Current = config;
                TvLog.MinimumLevel = Enum.TryParse<LogLevel>(config.LogLevel, true, out var level) ? level : LogLevel.Info;
                TvLog.Info("config.json loaded");
            }
            catch (Exception ex)
            {
                // Keep the previous (valid) configuration: a typo must not break the keys.
                TvLog.Error("config.json is invalid, previous configuration kept", ex);
            }
        }

        private static void Normalize(PluginConfig c)
        {
            c.Favorites ??= new();
            c.QuickConnect ??= new FavoriteConfig();
            foreach (var f in c.Favorites.Append(c.QuickConnect))
            {
                f.TeamviewerId = NormalizeId(f.TeamviewerId);
            }
            c.Microphone ??= new();
            c.Windows ??= new();
            c.Uia ??= new();
            c.Keys ??= new();
            c.Scripts ??= new();
            c.Profiles ??= new();
            c.Profiles.Idle ??= new ContextProfiles().Idle;
            c.Profiles.Session ??= new ContextProfiles().Session;
            c.AudioProcesses = c.AudioProcesses?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? new();
            if (c.AudioProcesses.Count == 0)
            {
                c.AudioProcesses.Add("TeamViewer");
            }
            c.VolumeStep = Math.Clamp(c.VolumeStep, 0.01f, 0.25f);
            c.ScrollLinesPerDetent = Math.Clamp(c.ScrollLinesPerDetent, 1, 10);
            c.DisconnectConfirmMs = Math.Clamp(c.DisconnectConfirmMs, 500, 5000);
        }

        // TeamViewer IDs are digits ("123 456 789" -> "123456789"). Anything else is rejected so
        // that a config value can never inject extra command-line arguments.
        public static string NormalizeId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return "";
            }
            var digits = new string(id.Where(char.IsDigit).ToArray());
            var onlyDigitsAndSeparators = id.All(ch => char.IsDigit(ch) || ch == ' ' || ch == '-' || ch == '.');
            return onlyDigitsAndSeparators && digits.Length is >= 6 and <= 12 ? digits : "";
        }

        public static string Expand(string path) =>
            string.IsNullOrWhiteSpace(path) ? path : Environment.ExpandEnvironmentVariables(path.Trim());

        private void Watch()
        {
            try
            {
                this._watcher = new FileSystemWatcher(this.Directory, "config.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true,
                };
                this._watcher.Changed += (_, _) =>
                {
                    // Editors write several times in a row.
                    if ((DateTime.UtcNow - this._lastReload).TotalMilliseconds < 500)
                    {
                        return;
                    }
                    this._lastReload = DateTime.UtcNow;
                    System.Threading.Thread.Sleep(150);
                    this.Reload();
                    this.Changed?.Invoke();
                };
            }
            catch (Exception ex)
            {
                TvLog.Warning("config.json watcher unavailable", ex);
            }
        }

        public void Dispose() => this._watcher?.Dispose();
    }
}
