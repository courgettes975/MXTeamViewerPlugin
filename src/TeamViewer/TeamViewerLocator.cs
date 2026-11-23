namespace Loupedeck.MXTeamViewerPlugin
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using Microsoft.Win32;

    // Finds TeamViewer.exe, its version and the session recordings folder.
    internal static class TeamViewerLocator
    {
        public static string FindExecutable(string configured)
        {
            var value = ConfigStore.Expand(configured);
            if (!string.IsNullOrWhiteSpace(value) && !value.Equals("AUTO", StringComparison.OrdinalIgnoreCase))
            {
                return File.Exists(value) ? value : null;
            }
            foreach (var key in new[] { @"SOFTWARE\TeamViewer", @"SOFTWARE\WOW6432Node\TeamViewer" })
            {
                using var k = Registry.LocalMachine.OpenSubKey(key);
                if (k?.GetValue("InstallationDirectory") is string dir)
                {
                    var exe = Path.Combine(dir, "TeamViewer.exe");
                    if (File.Exists(exe))
                    {
                        return exe;
                    }
                }
            }
            foreach (var root in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
            {
                var exe = Path.Combine(Environment.GetFolderPath(root), "TeamViewer", "TeamViewer.exe");
                if (File.Exists(exe))
                {
                    return exe;
                }
            }
            try
            {
                return Process.GetProcessesByName("TeamViewer").Select(p => p.MainModule?.FileName).FirstOrDefault(File.Exists);
            }
            catch
            {
                return null;
            }
        }

        public static string GetVersion(string exe)
        {
            try
            {
                return string.IsNullOrEmpty(exe) ? "" : FileVersionInfo.GetVersionInfo(exe).FileVersion ?? "";
            }
            catch
            {
                return "";
            }
        }

        public static string GetLanguage()
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\TeamViewer");
            return k?.GetValue("LastSelectedLanguage") as string ?? "";
        }

        // The default recordings folder is not documented by TeamViewer. Order:
        // 1. config.json recordingsPath  2. a known folder that already contains *.tvs
        // 3. Documents\TeamViewer (shown even if empty, so the user can set it in TeamViewer).
        public static string FindRecordingsFolder(string configured)
        {
            var value = ConfigStore.Expand(configured);
            if (!string.IsNullOrWhiteSpace(value) && !value.Equals("AUTO", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var candidates = new[]
            {
                Path.Combine(docs, "TeamViewer"),
                Path.Combine(docs, "TeamViewer Recordings"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "TeamViewer"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TeamViewer"),
                docs,
            };
            foreach (var c in candidates)
            {
                try
                {
                    if (Directory.Exists(c) && Directory.EnumerateFiles(c, "*.tvs", SearchOption.TopDirectoryOnly).Any())
                    {
                        return c;
                    }
                }
                catch
                {
                    // access denied etc.
                }
            }
            return candidates[0];
        }

        // A .tvs file written in the last few seconds => a recording is in progress.
        public static (Tri state, string latest) DetectRecording(string folder, int activeWindowSeconds = 6)
        {
            try
            {
                if (!Directory.Exists(folder))
                {
                    return (Tri.Unknown, "");
                }
                var latest = new DirectoryInfo(folder).EnumerateFiles("*.tvs", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
                if (latest == null)
                {
                    return (Tri.Off, "");
                }
                latest.Refresh();
                var active = (DateTime.UtcNow - latest.LastWriteTimeUtc).TotalSeconds <= activeWindowSeconds;
                return (active ? Tri.On : Tri.Off, latest.FullName);
            }
            catch
            {
                return (Tri.Unknown, "");
            }
        }
    }
}
