namespace Loupedeck.MXTeamViewerPlugin
{
    using System;
    using System.Threading.Tasks;

    // Rotary controls (MX Creative DIALPAD dial/roller, Loupedeck encoders).
    // NOTE: the MX Creative Keypad has no dial; on the keypad use the zoom.* / vol.* / dial.mode keys.
    //
    //   dial.auto       mode chosen in config.json "dialMode" (cycle it with the "Mode molette" key)
    //   dial.zoom       rotate: zoom +/-        press: 100 %
    //   dial.volume     rotate: remote volume   press: remote audio mute/unmute
    //   dial.navigation rotate: vertical scroll press: cycle Ajuster -> 100 % -> Plein écran
    public sealed class TeamViewerDial : PluginDynamicAdjustment
    {
        private int _viewCycle;

        public TeamViewerDial()
            : base(true)
        {
            this.AddParameter("dial.auto", "Molette TeamViewer (mode configurable)", "Molette");
            this.AddParameter("dial.zoom", "Molette : Zoom (appui = 100 %)", "Molette");
            this.AddParameter("dial.volume", "Molette : Volume distant (appui = muet)", "Molette");
            this.AddParameter("dial.navigation", "Molette : Navigation (appui = Ajuster/100 %/Plein écran)", "Molette");
        }

        private MXTeamViewerPlugin TvPlugin => this.Plugin as MXTeamViewerPlugin;

        private string ModeOf(string parameter) =>
            parameter == "dial.auto" ? (this.TvPlugin?.Config.Current.DialMode ?? "zoom") : parameter.Replace("dial.", "");

        protected override bool OnLoad()
        {
            if (this.TvPlugin != null)
            {
                this.TvPlugin.StateChanged += this.OnStateChanged;
            }
            return true;
        }

        protected override bool OnUnload()
        {
            if (this.TvPlugin != null)
            {
                this.TvPlugin.StateChanged -= this.OnStateChanged;
            }
            return true;
        }

        private void OnStateChanged()
        {
            foreach (var p in new[] { "dial.auto", "dial.zoom", "dial.volume", "dial.navigation" })
            {
                this.AdjustmentValueChanged(p);
            }
        }

        protected override void ApplyAdjustment(string actionParameter, int diff)
        {
            var tv = this.TvPlugin?.TeamViewer;
            if (tv == null || diff == 0)
            {
                return;
            }
            var mode = this.ModeOf(actionParameter);
            Task.Run(() =>
            {
                try
                {
                    switch (mode)
                    {
                        case "volume":
                            tv.StepRemoteVolume(diff);
                            break;
                        case "navigation":
                            // Wheel up = content moves down: one detent up scrolls up.
                            tv.Scroll(-diff * this.TvPlugin.Config.Current.ScrollLinesPerDetent);
                            break;
                        default:
                            var n = Math.Min(Math.Abs(diff), 5);
                            for (var i = 0; i < n; i++)
                            {
                                if (diff > 0) { tv.ZoomIn(); } else { tv.ZoomOut(); }
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    TvLog.Warning("Dial action failed", ex);
                }
                this.AdjustmentValueChanged(actionParameter);
            });
        }

        protected override void RunCommand(string actionParameter)
        {
            var tv = this.TvPlugin?.TeamViewer;
            if (tv == null)
            {
                return;
            }
            var mode = this.ModeOf(actionParameter);
            Task.Run(() =>
            {
                try
                {
                    switch (mode)
                    {
                        case "volume":
                            if (tv.State.RemoteAudio == Tri.Off) { tv.UnmuteRemoteAudio(); } else { tv.MuteRemoteAudio(); }
                            break;
                        case "navigation":
                            this._viewCycle = (this._viewCycle + 1) % 3;
                            var r = this._viewCycle switch
                            {
                                0 => tv.FitToWindow(),
                                1 => tv.SetZoom(100),
                                _ => tv.ToggleFullscreen(),
                            };
                            if (!r.Success && this._viewCycle != 2)
                            {
                                // No shortcut configured: fall back to window restore/maximise.
                                tv.ToggleFullscreen();
                            }
                            break;
                        default:
                            tv.SetZoom(100);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    TvLog.Warning("Dial press failed", ex);
                }
                this.AdjustmentValueChanged(actionParameter);
            });
        }

        protected override string GetAdjustmentValue(string actionParameter)
        {
            var plugin = this.TvPlugin;
            if (plugin == null)
            {
                return "";
            }
            var s = plugin.TeamViewer.State;
            return this.ModeOf(actionParameter) switch
            {
                "volume" => s.RemoteAudio == Tri.Off ? "Muet" : s.RemoteVolume >= 0 ? $"{Math.Round(s.RemoteVolume * 100)} %" : "Vol ?",
                "navigation" => s.InSession ? "Défil." : "—",
                _ => plugin.TeamViewer.IsZoomConfigured ? "Zoom" : "Zoom N/D",
            };
        }
    }
}
