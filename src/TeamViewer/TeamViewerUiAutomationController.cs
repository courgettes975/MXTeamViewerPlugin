namespace Loupedeck.MXTeamViewerPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using MXTeamViewer.Shared;

    // Microsoft UI Automation on the TeamViewer MAIN window (executed by the agent).
    // Verified on 15.83.7: the main window is a web UI exposing AutomationIds such as
    // "search-input-field" (Rechercher et se connecter) and "meeting" (Réunion).
    // Remote session windows (sciter-frame) expose only 2 anonymous panes: no UIA control there.
    internal sealed class TeamViewerUiAutomationController
    {
        private readonly AgentClient _agent;
        private readonly Func<PluginConfig> _config;

        public TeamViewerUiAutomationController(AgentClient agent, Func<PluginConfig> config)
        {
            this._agent = agent;
            this._config = config;
        }

        public bool AgentAvailable => this._agent.IsAvailable;

        private static Dictionary<string, string> Args(IntPtr hwnd, ElementSelector selector) => new()
        {
            ["hwnd"] = hwnd.ToInt64().ToString(CultureInfo.InvariantCulture),
            ["automationId"] = selector?.AutomationId ?? "",
            ["names"] = string.Join("|", selector?.Names ?? new List<string>()),
        };

        public bool Exists(IntPtr mainWindow, ElementSelector selector) =>
            mainWindow != IntPtr.Zero && this._agent.Call(AgentCommands.UiaExists, Args(mainWindow, selector), 4000).Ok;

        public ActionResult Invoke(IntPtr mainWindow, ElementSelector selector)
        {
            var r = this._agent.Call(AgentCommands.UiaInvoke, Args(mainWindow, selector), 4000);
            return r.Ok ? ActionResult.Ok("uia", r.Message) : ActionResult.Fail(r.Message, "uia");
        }

        public ActionResult Focus(IntPtr mainWindow, ElementSelector selector)
        {
            var r = this._agent.Call(AgentCommands.UiaFocus, Args(mainWindow, selector), 4000);
            return r.Ok ? ActionResult.Ok("uia", r.Message) : ActionResult.Fail(r.Message, "uia");
        }

        public ActionResult FocusSearchField(IntPtr mainWindow) => this.Focus(mainWindow, this._config().Uia.SearchField);

        public bool MeetingAvailable(IntPtr mainWindow) => this.Exists(mainWindow, this._config().Uia.Meeting);

        public ActionResult OpenMeeting(IntPtr mainWindow) => this.Invoke(mainWindow, this._config().Uia.Meeting);

        public string Dump(IntPtr hwnd)
        {
            var r = this._agent.Call(AgentCommands.UiaDump, new() { ["hwnd"] = hwnd.ToInt64().ToString(CultureInfo.InvariantCulture) }, 8000);
            return r.Ok && r.Data.TryGetValue("text", out var t) ? t : r.Message;
        }
    }
}
