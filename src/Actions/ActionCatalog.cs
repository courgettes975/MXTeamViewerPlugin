namespace Loupedeck.MXTeamViewerPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // Every key action in one table: id, name in Logi Options+, group, what a press does and how
    // the LCD looks for the current state. Used by the individual keys AND the contextual keys.
    internal sealed class KeyAction
    {
        public string Id { get; init; }
        public string DisplayName { get; init; }
        public string Group { get; init; }
        public Func<ActionContext, ActionResult> Press { get; init; }
        public Func<ActionContext, Icons.Spec> View { get; init; }
        public bool NeedsDoublePress { get; init; }
    }

    internal sealed class ActionContext
    {
        public ActionContext(MXTeamViewerPlugin plugin)
        {
            this.Plugin = plugin;
        }

        public MXTeamViewerPlugin Plugin { get; }
        public TeamViewerController Tv => this.Plugin.TeamViewer;
        public TeamViewerState S => this.Plugin.TeamViewer.State;
        public PluginConfig Config => this.Plugin.Config.Current;
    }

    internal static class ActionCatalog
    {
        public const string GroupMain = "TeamViewer";
        public const string GroupSession = "Session";
        public const string GroupFavorites = "Favoris";
        public const string GroupView = "Affichage";
        public const string GroupTools = "Outils";

        public static readonly IReadOnlyList<KeyAction> All = Build().ToList();

        public static KeyAction Find(string id) => All.FirstOrDefault(a => a.Id == id);

        private static Icons.Spec V(Glyph g, string label, BitmapColor? fg = null, BitmapColor? bg = null, bool strike = false, BitmapColor? frame = null) =>
            new() { Glyph = g, Label = label, Fg = fg ?? Palette.Text, Bg = bg ?? Palette.Background, Strike = strike, Frame = frame };

        private static Icons.Spec Dim(Icons.Spec v)
        {
            v.Fg = Palette.Grey;
            return v;
        }

        private static IEnumerable<KeyAction> Build()
        {
            yield return new KeyAction
            {
                Id = "tv.open", DisplayName = "TeamViewer (lancer / premier plan)", Group = GroupMain,
                Press = c => c.Tv.Launch(),
                View = c => V(Glyph.TeamViewer, "TeamViewer", frame: c.S.Running ? Palette.Blue : null),
            };
            yield return new KeyAction
            {
                Id = "tv.newsession", DisplayName = "Nouvelle session", Group = GroupMain,
                Press = c => c.Tv.OpenNewSession(),
                View = c => V(Glyph.NewSession, "Nouvelle"),
            };
            yield return new KeyAction
            {
                Id = "tv.quick", DisplayName = "Connexion rapide (ID favori)", Group = GroupMain,
                Press = c => string.IsNullOrEmpty(c.Config.QuickConnect.TeamviewerId) ? c.Tv.FocusConnectField() : c.Tv.Connect(c.Config.QuickConnect.TeamviewerId),
                View = c => FavoriteView(c, c.Config.QuickConnect, Glyph.QuickConnect, "Rapide"),
            };
            yield return new KeyAction
            {
                Id = "tv.meeting", DisplayName = "Créer réunion (si disponible)", Group = GroupMain,
                Press = c => c.Tv.CreateMeeting(),
                View = c => c.Tv.IsMeetingAvailable() || c.S.MainWindow == IntPtr.Zero
                    ? V(Glyph.Meeting, "Réunion", frame: c.S.MeetingActive ? Palette.Green : null)
                    : Badge(Dim(V(Glyph.Meeting, "Réunion")), "N/D"),
            };

            // ---- session ---------------------------------------------------------------------
            yield return new KeyAction
            {
                Id = "tv.mic", DisplayName = "Micro ON/OFF (TeamViewer)", Group = GroupSession,
                Press = c => c.S.Microphone == Tri.Off ? c.Tv.UnmuteMicrophone() : c.Tv.MuteMicrophone(),
                View = c => c.S.Microphone switch
                {
                    Tri.On => V(Glyph.Mic, "Micro ON", Palette.Green),
                    Tri.Off => V(Glyph.Mic, "Micro OFF", Palette.Red, strike: true),
                    _ => Dim(V(Glyph.Mic, "Micro ?")),
                },
            };
            yield return new KeyAction
            {
                Id = "tv.mic.windows", DisplayName = "Micro Windows ON/OFF (toutes applis)", Group = GroupSession,
                Press = c => c.Tv.SetWindowsMicrophone(c.S.WindowsMicrophone != Tri.Off),
                View = c => c.S.WindowsMicrophone switch
                {
                    Tri.On => V(Glyph.Mic, "Win ON", Palette.Text),
                    Tri.Off => V(Glyph.Mic, "Win OFF", Palette.Red, strike: true),
                    _ => Dim(V(Glyph.Mic, "Win ?")),
                },
            };
            yield return new KeyAction
            {
                Id = "tv.audio", DisplayName = "Son distant ON/OFF", Group = GroupSession,
                Press = c => c.S.RemoteAudio == Tri.Off ? c.Tv.UnmuteRemoteAudio() : c.Tv.MuteRemoteAudio(),
                View = c => c.S.RemoteAudio switch
                {
                    Tri.On => V(Glyph.Speaker, "Audio ON", Palette.Green),
                    Tri.Off => V(Glyph.Speaker, "Audio OFF", Palette.Red, strike: true),
                    _ => Dim(V(Glyph.Speaker, "Audio ?")),
                },
            };
            yield return new KeyAction
            {
                Id = "tv.record", DisplayName = "Démarrer / arrêter l'enregistrement", Group = GroupSession,
                Press = c => c.S.Recording == Tri.On ? c.Tv.StopRecording() : c.Tv.StartRecording(),
                View = c =>
                {
                    if (c.S.Recording == Tri.On)
                    {
                        // Light animation: the frame blinks once per second while recording.
                        var blink = DateTime.Now.Second % 2 == 0;
                        return V(Glyph.Stop, "Arrêter", frame: blink ? Palette.Red : null);
                    }
                    var v = V(Glyph.Record, "Enregistrer");
                    return c.Tv.IsRecordingConfigured ? (c.S.InSession ? v : Dim(v)) : Badge(Dim(v), "N/D");
                },
            };
            yield return new KeyAction
            {
                Id = "tv.recordings", DisplayName = "Enregistrements (dossier)", Group = GroupSession,
                Press = c => c.Tv.OpenRecordings(),
                View = c => V(Glyph.Folder, "Enregistr."),
            };
            yield return new KeyAction
            {
                Id = "tv.capture", DisplayName = "Capture d'écran (session)", Group = GroupSession,
                Press = c => c.Tv.TakeScreenshot(),
                View = c => c.S.InSession ? V(Glyph.Camera, "Capture") : Dim(V(Glyph.Camera, "Capture")),
            };
            yield return new KeyAction
            {
                Id = "tv.disconnect", DisplayName = "Déconnecter (session uniquement)", Group = GroupSession,
                NeedsDoublePress = true,
                Press = c => c.Tv.Disconnect(),
                View = c => c.S.InSession
                    ? V(Glyph.Disconnect, "Déconnecter", bg: Palette.Red)
                    : V(Glyph.Disconnect, "Déconnecter", Palette.Grey, Palette.DarkRed),
            };

            // ---- view ------------------------------------------------------------------------
            yield return new KeyAction
            {
                Id = "tv.fullscreen", DisplayName = "Plein écran", Group = GroupView,
                Press = c => c.Tv.ToggleFullscreen(),
                View = c =>
                {
                    var label = c.Tv.IsFullscreenNative ? "Plein écran" : "Fenêtre";
                    var v = V(Glyph.Fullscreen, label, frame: c.S.ActiveSession?.Maximized == true ? Palette.Blue : null);
                    return c.S.InSession ? v : Dim(v);
                },
            };
            yield return ZoomKey("zoom.in", "Zoom +", Glyph.ZoomIn, c => c.Tv.ZoomIn());
            yield return ZoomKey("zoom.out", "Zoom -", Glyph.ZoomOut, c => c.Tv.ZoomOut());
            yield return ZoomKey("zoom.100", "Zoom 100 %", Glyph.Zoom100, c => c.Tv.SetZoom(100));
            yield return new KeyAction
            {
                Id = "vol.up", DisplayName = "Volume distant +", Group = GroupView,
                Press = c => c.Tv.StepRemoteVolume(+1),
                View = c => VolumeView(c, Glyph.VolumeUp),
            };
            yield return new KeyAction
            {
                Id = "vol.down", DisplayName = "Volume distant -", Group = GroupView,
                Press = c => c.Tv.StepRemoteVolume(-1),
                View = c => VolumeView(c, Glyph.VolumeDown),
            };
            yield return new KeyAction
            {
                Id = "dial.mode", DisplayName = "Mode molette (zoom / volume / navigation)", Group = GroupView,
                Press = c => c.Plugin.CycleDialMode(),
                View = c => V(Glyph.Dial, c.Config.DialMode switch { "volume" => "Volume", "navigation" => "Navig.", _ => "Zoom" }),
            };

            // ---- favorites -------------------------------------------------------------------
            for (var i = 1; i <= 5; i++)
            {
                var index = i - 1;
                yield return new KeyAction
                {
                    Id = $"fav.{i}", DisplayName = $"Session favorite {i}", Group = GroupFavorites,
                    Press = c => index < c.Config.Favorites.Count && !string.IsNullOrEmpty(c.Config.Favorites[index].TeamviewerId)
                        ? c.Tv.Connect(c.Config.Favorites[index].TeamviewerId)
                        : ActionResult.Fail($"favori {index + 1} sans ID (config.json)", "config"),
                    View = c =>
                    {
                        var f = index < c.Config.Favorites.Count ? c.Config.Favorites[index] : new FavoriteConfig { Name = $"Favori {index + 1}" };
                        return FavoriteView(c, f, GlyphOf(f.Icon), f.Name);
                    },
                };
            }

            // ---- tools -----------------------------------------------------------------------
            yield return new KeyAction
            {
                Id = "tv.diagnostics", DisplayName = "Diagnostic MX TeamViewer", Group = GroupTools,
                Press = c => c.Plugin.ShowDiagnostics(runTests: false),
                View = c => V(Glyph.Diagnostics, "Diagnostic"),
            };
            yield return new KeyAction
            {
                Id = "tv.selftest", DisplayName = "Tester les actions (lecture seule)", Group = GroupTools,
                Press = c => c.Plugin.ShowDiagnostics(runTests: true),
                View = c => V(Glyph.Test, "Tester"),
            };
        }

        private static KeyAction ZoomKey(string id, string name, Glyph g, Func<ActionContext, ActionResult> press) => new()
        {
            Id = id, DisplayName = name, Group = GroupView, Press = press,
            View = c => c.Tv.IsZoomConfigured ? (c.S.InSession ? V(g, name) : Dim(V(g, name))) : Badge(Dim(V(g, name)), "N/D"),
        };

        private static Icons.Spec VolumeView(ActionContext c, Glyph g)
        {
            var label = c.S.RemoteVolume >= 0 ? $"{Math.Round(c.S.RemoteVolume * 100)} %" : "Vol ?";
            var v = V(g, label, c.S.RemoteAudio == Tri.Off ? Palette.Red : Palette.Text);
            return c.S.RemoteVolume >= 0 ? v : Dim(v);
        }

        private static Icons.Spec Badge(Icons.Spec v, string badge)
        {
            v.Badge = badge;
            return v;
        }

        private static Glyph GlyphOf(string icon) => (icon ?? "").ToLowerInvariant() switch
        {
            "laptop" => Glyph.Laptop,
            "server" => Glyph.Server,
            "client" => Glyph.Client,
            "star" => Glyph.Star,
            _ => Glyph.Desktop,
        };

        // Green = a session to this device is open, white = ready, grey = no ID configured.
        private static Icons.Spec FavoriteView(ActionContext c, FavoriteConfig f, Glyph g, string fallbackLabel)
        {
            var label = string.IsNullOrWhiteSpace(f.Name) ? fallbackLabel : f.Name;
            if (string.IsNullOrEmpty(f.TeamviewerId))
            {
                return Badge(V(g, label, Palette.Grey), "ID?");
            }
            var connected = c.S.Sessions.Any(s => Matches(s, f));
            return V(g, label, connected ? Palette.Green : Palette.Text, frame: connected ? Palette.Green : null);
        }

        private static bool Matches(SessionWindow s, FavoriteConfig f) =>
            (!string.IsNullOrWhiteSpace(f.Alias) && s.RemoteName.Equals(f.Alias.Trim(), StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(f.Name) && s.RemoteName.Equals(f.Name.Trim(), StringComparison.OrdinalIgnoreCase))
            || s.Title.Contains(f.TeamviewerId, StringComparison.Ordinal);
    }
}
