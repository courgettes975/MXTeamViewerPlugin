namespace Loupedeck.MXTeamViewerPlugin
{
    using System;
    using System.Diagnostics;
    using System.IO;

    // Process / command-line / URI level control of TeamViewer.
    //   Launch           : TeamViewer.exe
    //   Connect(id)      : TeamViewer.exe -i <ID>   (documented command-line parameter)
    //                      or teamviewer10://control?device=<ID> (URI handler registered by the installer)
    // No password is ever passed: TeamViewer asks for it itself.
    internal sealed class TeamViewerCliController
    {
        private readonly Func<PluginConfig> _config;

        public TeamViewerCliController(Func<PluginConfig> config)
        {
            this._config = config;
        }

        public string ExecutablePath => TeamViewerLocator.FindExecutable(this._config().TeamviewerPath);

        public bool IsInstalled => this.ExecutablePath != null;

        public ActionResult Launch()
        {
            var exe = this.ExecutablePath;
            if (exe == null)
            {
                return ActionResult.Fail("TeamViewer introuvable (teamviewerPath)", "cli");
            }
            try
            {
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe)! })?.Dispose();
                return ActionResult.Ok("cli", "launched");
            }
            catch (Exception ex)
            {
                return ActionResult.Fail(ex.Message, "cli");
            }
        }

        public ActionResult Connect(string deviceId)
        {
            var id = ConfigStore.NormalizeId(deviceId);
            if (id.Length == 0)
            {
                return ActionResult.Fail("ID TeamViewer invalide ou vide", "cli");
            }
            if (string.Equals(this._config().ConnectMethod, "uri", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Process.Start(new ProcessStartInfo($"teamviewer10://control?device={id}") { UseShellExecute = true })?.Dispose();
                    return ActionResult.Ok("uri");
                }
                catch (Exception ex)
                {
                    TvLog.Warning("URI connect failed, falling back to CLI", ex);
                }
            }
            var exe = this.ExecutablePath;
            if (exe == null)
            {
                return ActionResult.Fail("TeamViewer introuvable", "cli");
            }
            try
            {
                var psi = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! };
                psi.ArgumentList.Add("-i");
                psi.ArgumentList.Add(id); // digits only (NormalizeId) => no argument injection
                Process.Start(psi)?.Dispose();
                return ActionResult.Ok("cli", "-i " + id);
            }
            catch (Exception ex)
            {
                return ActionResult.Fail(ex.Message, "cli");
            }
        }

        public ActionResult OpenFolder(string folder, string latestFile)
        {
            try
            {
                if (!string.IsNullOrEmpty(latestFile) && File.Exists(latestFile))
                {
                    var psi = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"));
                    psi.ArgumentList.Add("/select," + latestFile);
                    Process.Start(psi)?.Dispose();
                    return ActionResult.Ok("explorer", latestFile);
                }
                if (!Directory.Exists(folder))
                {
                    folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true })?.Dispose();
                return ActionResult.Ok("explorer", folder);
            }
            catch (Exception ex)
            {
                return ActionResult.Fail(ex.Message, "explorer");
            }
        }
    }
}
