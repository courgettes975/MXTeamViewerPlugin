namespace Loupedeck.MXTeamViewerPlugin
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;

    // "MX TeamViewer Plugin Diagnostics": plain-text report written to
    // %LOCALAPPDATA%\MXTeamViewerPlugin\diagnostics.txt and opened in the default editor.
    // "Tester les actions" adds READ-ONLY self-tests (nothing is muted, clicked or closed).
    internal static class DiagnosticsReport
    {
        public static string Write(MXTeamViewerPlugin plugin, bool runTests)
        {
            var tv = plugin.TeamViewer;
            var s = tv.State;
            var c = plugin.Config.Current;
            var sb = new StringBuilder();
            void Line(string text = "") => sb.AppendLine(text);
            string Mark(bool ok) => ok ? "[OK]" : "[--]";

            Line("MX TeamViewer Plugin Diagnostics");
            Line($"Généré le {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Line(new string('=', 60));
            Line($"Version plugin       : {typeof(MXTeamViewerPlugin).Assembly.GetName().Version}");
            var exe = tv.Cli.ExecutablePath;
            Line($"Version TeamViewer   : {TeamViewerLocator.GetVersion(exe)} (langue {TeamViewerLocator.GetLanguage()})");
            Line($"Version Logi Plugin  : {LogiVersion()}");
            Line($"Chemin TeamViewer    : {exe ?? "introuvable"}");
            Line($"PID TeamViewer       : {(s.Running ? s.Pid.ToString() : "non lancé")}");
            Line($"Agent                : {tv.Agent.ExePath} ({(tv.Agent.IsRunning ? "actif" : "arrêté")})");
            Line($"Configuration        : {plugin.Config.FilePath}");
            Line($"Logs                 : {TvLog.Directory}");
            Line();
            Line("ÉTAT");
            Line($"  Session            : {(s.InSession ? string.Join(", ", s.Sessions.Select(x => x.RemoteName)) : "aucune")}");
            Line($"  Réunion active     : {(s.MeetingActive ? "oui" : "non")}");
            Line($"  Micro              : {s.Microphone} (source {s.MicrophoneSource}, mode {c.Microphone.Mode})");
            Line($"  Micro Windows      : {s.WindowsMicrophone}");
            Line($"  Son distant        : {s.RemoteAudio} (volume {(s.RemoteVolume >= 0 ? $"{s.RemoteVolume:P0}" : "?")})");
            Line($"  Enregistrement     : {s.Recording} ({tv.RecordingsFolder})");
            Line($"  Sessions audio     : {tv.LastAudioDetails}");
            Line();
            Line("TEAMVIEWER : FONCTIONS ET MÉTHODES");
            foreach (var cap in tv.GetCapabilities())
            {
                Line($"  {Mark(cap.Available)} {cap.Feature,-36} {cap.Method}{(string.IsNullOrEmpty(cap.Note) ? "" : "  - " + cap.Note)}");
            }
            Line();
            Line("LOGITECH");
            Line($"  {Mark(File.Exists(@"C:\Program Files\Logi\LogiPluginService\PluginApi.dll"))} Logi Plugin Service installé");
            Line($"  {Mark(Process.GetProcessesByName("logioptionsplus_agent").Length > 0 || Process.GetProcessesByName("LogiPluginService").Length > 0)} Logi Options+ / Plugin Service en cours");
            Line("  [i ] Présence du MX Creative Keypad : non exposée par le SDK au plugin (vérifier dans Options+).");
            Line("  [i ] Touches LCD : 9 (Keypad). Molette : uniquement sur le MX Creative Dialpad.");
            Line();
            Line("FENÊTRES TEAMVIEWER VISIBLES");
            foreach (var w in tv.LastWindows)
            {
                Line("  " + w);
            }
            Line();
            Line("FAVORIS (config.json)");
            Line($"  Connexion rapide : {Describe(c.QuickConnect)}");
            for (var i = 0; i < c.Favorites.Count; i++)
            {
                Line($"  Favori {i + 1}        : {Describe(c.Favorites[i])}");
            }

            if (runTests)
            {
                Line();
                Line("TESTS (lecture seule)");
                Test(sb, "Agent ping", () => tv.Agent.Call(MXTeamViewer.Shared.AgentCommands.Ping).Ok);
                Test(sb, "Lecture sessions audio", () => !string.IsNullOrEmpty(tv.Audio.Get().Details));
                Test(sb, "UIA: champ « Rechercher et se connecter »", () => s.MainWindow != IntPtr.Zero && tv.Uia.Exists(s.MainWindow, c.Uia.SearchField));
                Test(sb, "UIA: menu Réunion", () => s.MainWindow != IntPtr.Zero && tv.Uia.Exists(s.MainWindow, c.Uia.Meeting));
                Test(sb, "Dossier enregistrements existe", () => Directory.Exists(tv.RecordingsFolder));
                Test(sb, "Dossier captures accessible en écriture", () =>
                {
                    var dir = ConfigStore.Expand(c.CapturesPath);
                    Directory.CreateDirectory(dir);
                    var probe = Path.Combine(dir, ".write-test");
                    File.WriteAllText(probe, "ok");
                    File.Delete(probe);
                    return true;
                });
                Test(sb, "Raccourci recherche parsable", () => KeyChord.TryParse(c.Keys.SearchAndConnect, out _));
                Line($"  [i ] Web API : {tv.Api.CheckToken()}");
                if (s.MainWindow != IntPtr.Zero)
                {
                    Line();
                    Line("ARBRE UI AUTOMATION (fenêtre principale, extrait)");
                    Line(tv.Uia.Dump(s.MainWindow));
                }
                foreach (var session in s.Sessions)
                {
                    Line(tv.Uia.Dump(session.Handle));
                }
            }

            var path = Path.Combine(plugin.Config.Directory, "diagnostics.txt");
            File.WriteAllText(path, TvLog.Redact(sb.ToString()), new UTF8Encoding(true));
            TvLog.Info("Diagnostics written: " + path);
            return path;
        }

        private static string Describe(FavoriteConfig f) =>
            string.IsNullOrEmpty(f.TeamviewerId) ? $"{f.Name} (pas d'ID)" : $"{f.Name} -> {f.TeamviewerId}{(string.IsNullOrEmpty(f.Alias) ? "" : $" alias {f.Alias}")}";

        private static void Test(StringBuilder sb, string name, Func<bool> test)
        {
            bool ok;
            string error = "";
            try
            {
                ok = test();
            }
            catch (Exception ex)
            {
                ok = false;
                error = " - " + ex.Message;
            }
            sb.AppendLine($"  {(ok ? "[OK]" : "[KO]")} {name}{error}");
        }

        private static string LogiVersion()
        {
            try
            {
                return FileVersionInfo.GetVersionInfo(@"C:\Program Files\Logi\LogiPluginService\PluginApi.dll").FileVersion ?? "?";
            }
            catch
            {
                return "?";
            }
        }
    }
}
