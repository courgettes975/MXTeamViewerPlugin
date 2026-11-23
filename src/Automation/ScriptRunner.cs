namespace Loupedeck.MXTeamViewerPlugin
{
    using System;
    using System.Diagnostics;
    using System.IO;

    // Last-resort hook (priority 5/6 of the spec): runs a user-provided PowerShell, AutoHotkey v2
    // or executable file. Only absolute paths from config.json; no argument is built from user text.
    internal static class ScriptRunner
    {
        public static bool IsConfigured(string path) => !string.IsNullOrWhiteSpace(path);

        public static ActionResult Run(string configuredPath, string autoHotkeyPath, string method)
        {
            var path = ConfigStore.Expand(configuredPath);
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) || !File.Exists(path))
            {
                return ActionResult.Fail($"Script introuvable: {path}", method);
            }
            ProcessStartInfo psi;
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".ps1":
                    psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"));
                    psi.ArgumentList.Add("-NoProfile");
                    psi.ArgumentList.Add("-NonInteractive");
                    psi.ArgumentList.Add("-ExecutionPolicy");
                    psi.ArgumentList.Add("Bypass");
                    psi.ArgumentList.Add("-WindowStyle");
                    psi.ArgumentList.Add("Hidden");
                    psi.ArgumentList.Add("-File");
                    psi.ArgumentList.Add(path);
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    break;
                case ".ahk":
                    var ahk = ConfigStore.Expand(autoHotkeyPath);
                    if (string.IsNullOrWhiteSpace(ahk))
                    {
                        // .ahk association installed by AutoHotkey v2.
                        psi = new ProcessStartInfo(path) { UseShellExecute = true };
                    }
                    else
                    {
                        psi = new ProcessStartInfo(ahk) { UseShellExecute = false };
                        psi.ArgumentList.Add(path);
                    }
                    break;
                case ".exe":
                case ".cmd":
                case ".bat":
                    psi = new ProcessStartInfo(path) { UseShellExecute = false, CreateNoWindow = true };
                    break;
                default:
                    return ActionResult.Fail("Extension de script non supportée (.ps1, .ahk, .exe, .cmd)", method);
            }
            try
            {
                Process.Start(psi)?.Dispose();
                TvLog.Info($"Script started: {path}");
                return ActionResult.Ok(method);
            }
            catch (Exception ex)
            {
                TvLog.Warning("Script failed: " + path, ex);
                return ActionResult.Fail(ex.Message, method);
            }
        }
    }
}
