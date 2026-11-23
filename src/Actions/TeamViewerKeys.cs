namespace Loupedeck.MXTeamViewerPlugin
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading.Tasks;

    // One PluginDynamicCommand exposing every action as a parameter (each one is its own draggable
    // action in Logi Options+), plus 9 contextual keys "Contexte 1..9" whose function follows the
    // TeamViewer state (profiles.idle / profiles.session in config.json).
    //
    //  - presses run on the thread pool (a controller call can wait for TeamViewer to start);
    //  - one press at a time per key, 250 ms debounce;
    //  - failures are shown on the key for 2.5 s (orange badge) and logged;
    //  - "Déconnecter": with disconnectProtection, the first press arms, the second within
    //    disconnectConfirmMs disconnects.
    public sealed class TeamViewerKeys : PluginDynamicCommand
    {
        private const string ContextPrefix = "ctx.";
        private const int ContextSlots = 9;

        private readonly ConcurrentDictionary<string, DateTime> _lastPress = new();
        private readonly ConcurrentDictionary<string, bool> _busy = new();
        private readonly ConcurrentDictionary<string, (DateTime until, string text)> _flash = new();
        private readonly ConcurrentDictionary<string, DateTime> _armed = new();
        private string _lastFingerprint = "";

        public TeamViewerKeys()
            : base()
        {
            foreach (var a in ActionCatalog.All)
            {
                this.AddParameter(a.Id, a.DisplayName, a.Group);
            }
            for (var i = 1; i <= ContextSlots; i++)
            {
                this.AddParameter(ContextPrefix + i, $"Contexte {i} (auto : inactif / en session)", "Contextuel");
            }
        }

        private MXTeamViewerPlugin TvPlugin => this.Plugin as MXTeamViewerPlugin;

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
            var plugin = this.TvPlugin;
            if (plugin == null)
            {
                return;
            }
            var recording = plugin.TeamViewer.State.Recording == Tri.On;
            var fp = plugin.TeamViewer.State.Fingerprint + plugin.Config.Current.DialMode;
            if (fp == this._lastFingerprint && !recording)
            {
                return;
            }
            this._lastFingerprint = fp;
            this.ActionImageChanged(); // all parameters
        }

        // Resolves a contextual slot to the action currently assigned to it.
        private KeyAction Resolve(string parameter)
        {
            if (parameter != null && parameter.StartsWith(ContextPrefix, StringComparison.Ordinal)
                && int.TryParse(parameter.AsSpan(ContextPrefix.Length), out var slot))
            {
                var plugin = this.TvPlugin;
                if (plugin == null)
                {
                    return null;
                }
                var profile = plugin.TeamViewer.State.InSession ? plugin.Config.Current.Profiles.Session : plugin.Config.Current.Profiles.Idle;
                return slot - 1 < profile.Count ? ActionCatalog.Find(profile[slot - 1]) : null;
            }
            return ActionCatalog.Find(parameter);
        }

        protected override void RunCommand(string actionParameter)
        {
            var plugin = this.TvPlugin;
            var action = this.Resolve(actionParameter);
            if (plugin == null || action == null)
            {
                return;
            }
            var now = DateTime.UtcNow;
            if (this._lastPress.TryGetValue(actionParameter, out var last) && (now - last).TotalMilliseconds < 250)
            {
                return;
            }
            this._lastPress[actionParameter] = now;

            if (action.NeedsDoublePress && plugin.Config.Current.DisconnectProtection)
            {
                var window = plugin.Config.Current.DisconnectConfirmMs;
                if (!this._armed.TryGetValue(action.Id, out var armedAt) || (now - armedAt).TotalMilliseconds > window)
                {
                    if (!plugin.TeamViewer.State.InSession)
                    {
                        this.Flash(actionParameter, "Aucune session");
                        return;
                    }
                    this._armed[action.Id] = now;
                    this.Flash(actionParameter, "Encore 1x", window);
                    return;
                }
                this._armed.TryRemove(action.Id, out _);
            }

            if (!this._busy.TryAdd(actionParameter, true))
            {
                return;
            }
            Task.Run(() =>
            {
                try
                {
                    var result = action.Press(new ActionContext(plugin));
                    if (result != null && !result.Success)
                    {
                        this.Flash(actionParameter, result.Method == "none" ? "N/D" : "Erreur");
                    }
                }
                catch (Exception ex)
                {
                    TvLog.Error($"Action {action.Id} crashed", ex);
                    this.Flash(actionParameter, "Erreur");
                }
                finally
                {
                    this._busy.TryRemove(actionParameter, out _);
                    this.ActionImageChanged(actionParameter);
                }
            });
        }

        private void Flash(string parameter, string text, int ms = 2500)
        {
            this._flash[parameter] = (DateTime.UtcNow.AddMilliseconds(ms), text);
            this.ActionImageChanged(parameter);
            Task.Delay(ms + 50).ContinueWith(_ => this.ActionImageChanged(parameter));
        }

        protected override string GetCommandDisplayName(string actionParameter, PluginImageSize imageSize)
        {
            var a = this.Resolve(actionParameter);
            return a?.DisplayName ?? actionParameter;
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            try
            {
                var plugin = this.TvPlugin;
                var action = this.Resolve(actionParameter);
                if (plugin == null || action == null)
                {
                    return Icons.Render(imageSize, new Icons.Spec { Glyph = Glyph.Empty, Label = "—", Fg = Palette.Grey });
                }
                var spec = action.View(new ActionContext(plugin));
                if (this._flash.TryGetValue(actionParameter, out var flash) && flash.until > DateTime.UtcNow)
                {
                    spec.Label = flash.text;
                    spec.Frame = Palette.Orange;
                }
                return Icons.Render(imageSize, spec);
            }
            catch (Exception ex)
            {
                TvLog.Warning($"Render failed for {actionParameter}", ex);
                return Icons.Render(imageSize, new Icons.Spec { Glyph = Glyph.Empty, Label = "Erreur", Fg = Palette.Red });
            }
        }
    }
}
