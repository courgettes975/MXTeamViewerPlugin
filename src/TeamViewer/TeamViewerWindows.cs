namespace Loupedeck.MXTeamViewerPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    // Classifies the top-level windows of TeamViewer.exe (rules from config.json, verified on 15.83.7):
    //   main window    : class MainWindowOne
    //   remote session : class sciter-frame, title "<device> - TeamViewer"
    //   meeting        : class BlizzMeetingCommandBar / ParticipantsTabContainer
    // Session order: the foreground session first, then the most recently seen.
    internal static class TeamViewerWindows
    {
        public sealed class Scan
        {
            public bool Running;
            public int Pid;
            public IntPtr Main;
            public List<SessionWindow> Sessions = new();
            public bool Meeting;
            public List<string> AllWindows = new();
        }

        public static Scan Run(WindowRules rules)
        {
            var scan = new Scan();
            var processes = Process.GetProcessesByName("TeamViewer");
            var pids = new HashSet<uint>(processes.Select(p => (uint)p.Id));
            scan.Running = pids.Count > 0;
            scan.Pid = processes.FirstOrDefault()?.Id ?? 0;
            foreach (var p in processes)
            {
                p.Dispose();
            }
            if (!scan.Running)
            {
                return scan;
            }

            var foreground = Win32.GetForegroundWindow();
            SessionWindow foregroundSession = null;
            foreach (var hwnd in Win32.TopLevelWindows())
            {
                if (!pids.Contains(Win32.ProcessIdOf(hwnd)) || !Win32.IsWindowVisible(hwnd))
                {
                    continue;
                }
                var cls = Win32.ClassOf(hwnd);
                var title = Win32.TitleOf(hwnd);
                scan.AllWindows.Add($"0x{hwnd.ToInt64():X} class={cls} title=[{title}]");

                if (rules.MainWindowClasses.Contains(cls, StringComparer.OrdinalIgnoreCase))
                {
                    scan.Main = hwnd;
                }
                else if (rules.MeetingWindowClasses.Contains(cls, StringComparer.OrdinalIgnoreCase))
                {
                    scan.Meeting = true;
                }
                else if (rules.SessionWindowClasses.Contains(cls, StringComparer.OrdinalIgnoreCase)
                    && (string.IsNullOrEmpty(rules.SessionTitleSuffix) || title.EndsWith(rules.SessionTitleSuffix, StringComparison.OrdinalIgnoreCase)))
                {
                    var name = string.IsNullOrEmpty(rules.SessionTitleSuffix) ? title : title[..^rules.SessionTitleSuffix.Length];
                    var session = new SessionWindow
                    {
                        Handle = hwnd,
                        Title = title,
                        RemoteName = name.Trim(),
                        Minimized = Win32.IsIconic(hwnd),
                        Maximized = Win32.IsZoomed(hwnd),
                    };
                    if (hwnd == foreground)
                    {
                        foregroundSession = session;
                    }
                    else
                    {
                        scan.Sessions.Add(session); // EnumWindows returns Z-order: topmost first
                    }
                }
            }
            if (foregroundSession != null)
            {
                scan.Sessions.Insert(0, foregroundSession);
            }
            return scan;
        }
    }
}
