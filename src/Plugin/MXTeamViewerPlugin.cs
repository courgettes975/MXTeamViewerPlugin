namespace Loupedeck.MXTeamViewerPlugin
{
    using System;
    using System.Diagnostics;
    using System.IO;

    // Plugin entry point loaded by the Logi Plugin Service (Logi Options+).
    public class MXTeamViewerPlugin : Plugin
    {
        public override bool UsesApplicationApiOnly => true;

        public override bool HasNoApplication => true;

        public MXTeamViewerPlugin()
        {
            TvLog.Init(this.Log);
            this.Config = new ConfigStore();
            this.Agent = new AgentClient(FindPluginBinDirectory());
            this.TeamViewer = new TeamViewerController(() => this.Config.Current, this.Agent);
        }

        // The Logi Plugin Service does not always expose the real plugin folder through
        // Assembly.Location (observed: it returns the service folder). Probe, in order:
        // the assembly folder, the installed package folder, then the developer .link target.
        private static string FindPluginBinDirectory()
        {
            var candidates = new System.Collections.Generic.List<string>();
            var asm = typeof(MXTeamViewerPlugin).Assembly.Location;
            if (!string.IsNullOrEmpty(asm))
            {
                candidates.Add(Path.GetDirectoryName(asm));
            }
            var plugins = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Logi", "LogiPluginService", "Plugins");
            candidates.Add(Path.Combine(plugins, "MXTeamViewer", "bin"));
            try
            {
                var link = Path.Combine(plugins, "MXTeamViewerPlugin.link");
                if (File.Exists(link))
                {
                    candidates.Insert(0, Path.Combine(File.ReadAllText(link).Trim(), "bin"));
                }
            }
            catch
            {
                // ignore unreadable link
            }
            foreach (var dir in candidates)
            {
                if (!string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "agent", "MXTeamViewerAgent.exe")))
                {
                    return dir;
                }
            }
            return candidates.Count > 0 ? candidates[0] : AppContext.BaseDirectory;
        }

        internal ConfigStore Config { get; }

        internal AgentClient Agent { get; }

        internal TeamViewerController TeamViewer { get; }

        // Raised (thread pool) whenever the TeamViewer state or the configuration changed.
        internal event Action StateChanged;

        public override void Load()
        {
            try
            {
                this.Config.Load();
                this.Config.Changed += () => this.StateChanged?.Invoke();
                this.TeamViewer.StateChanged += _ => this.StateChanged?.Invoke();
                this.TeamViewer.Start();
                TvLog.Info($"MX TeamViewer {typeof(MXTeamViewerPlugin).Assembly.GetName().Version} loaded");

                if (!this.TeamViewer.Cli.IsInstalled)
                {
                    this.OnPluginStatusChanged(Loupedeck.PluginStatus.Warning, "TeamViewer introuvable. Renseignez teamviewerPath dans config.json.");
                }
                else if (!this.Agent.IsAvailable)
                {
                    this.OnPluginStatusChanged(Loupedeck.PluginStatus.Warning, "Agent d'automatisation absent : micro, audio, capture indisponibles. Réinstallez le plugin.");
                }

                // First launch: build the diagnostic report (auto-detection) and show it once.
                if (this.Config.CreatedOnThisRun)
                {
                    System.Threading.Tasks.Task.Delay(4000).ContinueWith(_ => this.ShowDiagnostics(runTests: false));
                }
            }
            catch (Exception ex)
            {
                TvLog.Error("Load failed", ex);
                this.OnPluginStatusChanged(Loupedeck.PluginStatus.Error, "Erreur au chargement : voir %LOCALAPPDATA%\\MXTeamViewerPlugin\\logs");
            }
        }

        public override void Unload()
        {
            this.TeamViewer.Dispose();
            this.Agent.Dispose();
            this.Config.Dispose();
        }

        internal ActionResult CycleDialMode()
        {
            var c = this.Config.Current;
            c.DialMode = c.DialMode switch { "zoom" => "volume", "volume" => "navigation", _ => "zoom" };
            this.Config.Save();
            this.StateChanged?.Invoke();
            return ActionResult.Ok("config", c.DialMode);
        }

        internal ActionResult ShowDiagnostics(bool runTests)
        {
            try
            {
                this.TeamViewer.RefreshNow();
                var path = DiagnosticsReport.Write(this, runTests);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
                return ActionResult.Ok("diagnostics", path);
            }
            catch (Exception ex)
            {
                TvLog.Error("Diagnostics failed", ex);
                return ActionResult.Fail(ex.Message, "diagnostics");
            }
        }
    }
}
