namespace Loupedeck.MXTeamViewerPlugin
{
    using System;
    using System.Globalization;
    using System.IO;
    using MXTeamViewer.Shared;

    // Win32 control of the remote session WINDOW (not of TeamViewer's internal toolbar, which is
    // not reachable by any official or UIA means on 15.83):
    //   Disconnect   : WM_CLOSE to the session window only (TeamViewer keeps running)
    //   Window mode  : maximise / restore (fallback for "plein écran" when no shortcut is set)
    //   Scroll       : WM_MOUSEWHEEL to the session window (forwarded to the remote device)
    //   Keys         : focus the session window, then SendInput of a configured shortcut
    //   Capture      : PNG of the session window (agent)
    internal sealed class SessionWindowController
    {
        private readonly AgentClient _agent;

        public SessionWindowController(AgentClient agent)
        {
            this._agent = agent;
        }

        public ActionResult Close(SessionWindow session)
        {
            if (session == null || !Win32.IsWindow(session.Handle))
            {
                return ActionResult.Fail("aucune session active", "win32");
            }
            Win32.PostMessage(session.Handle, Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            return ActionResult.Ok("win32-close", session.RemoteName);
        }

        public ActionResult ToggleMaximize(SessionWindow session)
        {
            if (session == null)
            {
                return ActionResult.Fail("aucune session active", "win32");
            }
            Win32.ShowWindow(session.Handle, Win32.IsZoomed(session.Handle) ? Win32.SW_RESTORE : Win32.SW_MAXIMIZE);
            Win32.Activate(session.Handle);
            return ActionResult.Ok("win32-maximize");
        }

        public ActionResult Scroll(SessionWindow session, int detents)
        {
            if (session == null)
            {
                return ActionResult.Fail("aucune session active", "win32");
            }
            Win32.PostWheel(session.Handle, detents);
            return ActionResult.Ok("win32-wheel");
        }

        public ActionResult SendKeys(IntPtr target, string chord, string method = "shortcut")
        {
            if (!KeyChord.TryParse(chord, out var keys))
            {
                return ActionResult.Unavailable("raccourci non configuré");
            }
            if (target != IntPtr.Zero && !Win32.Activate(target))
            {
                TvLog.Debug("SendKeys: target window could not be activated, sending anyway");
            }
            System.Threading.Thread.Sleep(60);
            return Win32.SendChord(keys) ? ActionResult.Ok(method, chord) : ActionResult.Fail("SendInput refusé", method);
        }

        public ActionResult Capture(IntPtr hwnd, string folder)
        {
            if (hwnd == IntPtr.Zero)
            {
                return ActionResult.Fail("aucune fenêtre TeamViewer", "capture");
            }
            var path = Path.Combine(folder, $"TeamViewer_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
            var r = this._agent.Call(AgentCommands.CaptureWindow, new()
            {
                ["hwnd"] = hwnd.ToInt64().ToString(CultureInfo.InvariantCulture),
                ["path"] = path,
            }, 8000);
            return r.Ok ? ActionResult.Ok("capture", path) : ActionResult.Fail(r.Message, "capture");
        }
    }
}
