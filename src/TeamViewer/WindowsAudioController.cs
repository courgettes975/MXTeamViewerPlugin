namespace Loupedeck.MXTeamViewerPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using MXTeamViewer.Shared;

    // Windows Core Audio, per process (executed by the agent):
    //   capture sessions of TeamViewer.exe  -> "Micro TeamViewer"
    //   render sessions of TeamViewer.exe   -> "Son distant" (+ volume)
    //   default capture endpoint            -> "Micro Windows" (optional, every app)
    internal sealed class WindowsAudioController
    {
        private readonly AgentClient _agent;
        private readonly Func<PluginConfig> _config;

        public WindowsAudioController(AgentClient agent, Func<PluginConfig> config)
        {
            this._agent = agent;
            this._config = config;
        }

        public sealed class Snapshot
        {
            public Tri AppMic = Tri.Unknown;
            public Tri AppRender = Tri.Unknown;
            public float AppVolume = -1;
            public Tri WindowsMic = Tri.Unknown;
            public string Details = "";
        }

        private Dictionary<string, string> Base() => new() { ["processes"] = string.Join("|", this._config().AudioProcesses) };

        public Snapshot Get()
        {
            var s = new Snapshot();
            var r = this._agent.Call(AgentCommands.AudioGet, this.Base(), 3000);
            if (!r.Ok)
            {
                s.Details = r.Message;
                return s;
            }
            bool B(string k) => r.Data.TryGetValue(k, out var v) && v == "true";
            // "On" means the sound/mic is live, "Off" means muted.
            if (B("capture.found"))
            {
                s.AppMic = B("capture.muted") ? Tri.Off : Tri.On;
            }
            if (B("render.found"))
            {
                s.AppRender = B("render.muted") ? Tri.Off : Tri.On;
                if (r.Data.TryGetValue("render.volume", out var v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                {
                    s.AppVolume = f;
                }
            }
            if (B("winmic.found"))
            {
                s.WindowsMic = B("winmic.muted") ? Tri.Off : Tri.On;
            }
            s.Details = $"capture: {Get(r, "capture.sessions")} | render: {Get(r, "render.sessions")}";
            return s;
        }

        private static string Get(AgentResponse r, string key) => r.Data.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : "-";

        public ActionResult SetAppMic(bool mute) => this.Send(AgentCommands.AudioSetAppCapture, mute, "coreaudio-session");

        public ActionResult SetAppRender(bool mute) => this.Send(AgentCommands.AudioSetAppRender, mute, "coreaudio-session");

        public ActionResult SetWindowsMic(bool mute)
        {
            var r = this._agent.Call(AgentCommands.AudioSetWindowsMic, new() { ["mute"] = mute ? "true" : "false" });
            return r.Ok ? ActionResult.Ok("coreaudio-endpoint") : ActionResult.Fail(r.Message, "coreaudio-endpoint");
        }

        public ActionResult StepVolume(float delta)
        {
            var args = this.Base();
            args["delta"] = delta.ToString("0.000", CultureInfo.InvariantCulture);
            var r = this._agent.Call(AgentCommands.AudioStepAppRender, args);
            return r.Ok ? ActionResult.Ok("coreaudio-session", r.Data.GetValueOrDefault("volume", "")) : ActionResult.Fail(r.Message, "coreaudio-session");
        }

        private ActionResult Send(string cmd, bool mute, string method)
        {
            var args = this.Base();
            args["mute"] = mute ? "true" : "false";
            var r = this._agent.Call(cmd, args);
            return r.Ok ? ActionResult.Ok(method, r.Message) : ActionResult.Fail(r.Message, method);
        }
    }
}
