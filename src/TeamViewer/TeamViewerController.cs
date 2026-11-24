namespace Loupedeck.MXTeamViewerPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    // Composite ITeamViewerController: owns the state polling loop and routes every call to the best
    // available method. Changing a method = changing one line here, actions are untouched.
    internal sealed class TeamViewerController : ITeamViewerController, IDisposable
    {
        private const int TickMs = 1000;

        private readonly Func<PluginConfig> _config;
        private readonly TeamViewerCliController _cli;
        private readonly TeamViewerUiAutomationController _uia;
        private readonly WindowsAudioController _audio;
        private readonly SessionWindowController _window;
        private readonly Timer _timer;
        private readonly object _tickLock = new();

        private TeamViewerState _state = TeamViewerState.Empty;
        private WindowsAudioController.Snapshot _lastAudio = new();
        private long _tick;
        private DateTime _recordingRequestedAt = DateTime.MinValue;
        private bool _recordingRequested;
        private (DateTime at, bool value) _meetingCache = (DateTime.MinValue, false);

        public TeamViewerController(Func<PluginConfig> config, AgentClient agent)
        {
            this._config = config;
            this.Agent = agent;
            this._cli = new TeamViewerCliController(config);
            this._uia = new TeamViewerUiAutomationController(agent, config);
            this._audio = new WindowsAudioController(agent, config);
            this._window = new SessionWindowController(agent);
            this.Api = new TeamViewerApiController(config);
            this._timer = new Timer(_ => this.Poll(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public event Action<TeamViewerState> StateChanged;

        public AgentClient Agent { get; }

        public TeamViewerApiController Api { get; }

        public TeamViewerCliController Cli => this._cli;

        public TeamViewerUiAutomationController Uia => this._uia;

        public WindowsAudioController Audio => this._audio;

        public TeamViewerState State => this._state;

        public string LastAudioDetails => this._lastAudio.Details;

        public List<string> LastWindows { get; private set; } = new();

        private PluginConfig C => this._config();

        public string RecordingsFolder => TeamViewerLocator.FindRecordingsFolder(this.C.RecordingsPath);

        public void Start() => this._timer.Change(200, TickMs);

        public void RefreshNow() => this.Poll(force: true);

        // ---------------------------------------------------------------- state polling ------

        private void Poll(bool force = false)
        {
            if (!Monitor.TryEnter(this._tickLock))
            {
                return; // previous tick still running (agent slow): skip
            }
            try
            {
                this._tick++;
                var scan = TeamViewerWindows.Run(this.C.Windows);
                this.LastWindows = scan.AllWindows;

                // Audio sessions only matter while TeamViewer runs; every 2 s is enough.
                if (scan.Running && (force || this._tick % 2 == 0))
                {
                    this._lastAudio = this._audio.Get();
                }
                else if (!scan.Running)
                {
                    this._lastAudio = new WindowsAudioController.Snapshot();
                }

                var (recording, latest) = scan.Sessions.Count > 0
                    ? TeamViewerLocator.DetectRecording(this.RecordingsFolder)
                    : (Tri.Off, "");
                if (recording != Tri.On && this._recordingRequested && (DateTime.UtcNow - this._recordingRequestedAt).TotalSeconds < 10)
                {
                    recording = Tri.On; // grace period: the .tvs file appears a few seconds later
                }
                if (scan.Sessions.Count == 0)
                {
                    this._recordingRequested = false; // TeamViewer stops recording with the session
                }

                var mic = this.ComputeMic(this._lastAudio, out var micSource);
                var next = new TeamViewerState
                {
                    Installed = this._cli.IsInstalled,
                    Running = scan.Running,
                    Pid = scan.Pid,
                    MainWindow = scan.Main,
                    Sessions = scan.Sessions,
                    MeetingActive = scan.Meeting,
                    Microphone = mic,
                    MicrophoneSource = micSource,
                    RemoteAudio = this._lastAudio.AppRender,
                    RemoteVolume = this._lastAudio.AppVolume,
                    WindowsMicrophone = this._lastAudio.WindowsMic,
                    Recording = recording,
                    LatestRecording = latest,
                };
                var changed = next.Fingerprint != this._state.Fingerprint;
                if (next.InSession != this._state.InSession)
                {
                    TvLog.Info(next.InSession ? $"Session detected: {next.ActiveSession?.RemoteName}" : "No session");
                }
                this._state = next;
                if (changed || force)
                {
                    this.StateChanged?.Invoke(next);
                }
            }
            catch (Exception ex)
            {
                TvLog.Warning("State poll failed", ex);
            }
            finally
            {
                Monitor.Exit(this._tickLock);
            }
        }

        private Tri ComputeMic(WindowsAudioController.Snapshot a, out string source)
        {
            switch (this.C.Microphone.Mode?.ToLowerInvariant())
            {
                case "windows":
                    source = "windows";
                    return a.WindowsMic;
                case "teamviewerorwindows":
                    if (a.AppMic != Tri.Unknown)
                    {
                        source = "teamviewer";
                        return a.AppMic;
                    }
                    source = "windows";
                    return a.WindowsMic;
                default:
                    source = "teamviewer";
                    return a.AppMic;
            }
        }

        private ActionResult Done(ActionResult r, string action)
        {
            if (r.Success)
            {
                TvLog.Info($"{action}: {r}");
            }
            else
            {
                TvLog.Warning($"{action}: {r}");
            }
            ThreadPool.QueueUserWorkItem(_ => { Thread.Sleep(400); this.Poll(force: true); });
            return r;
        }

        // ------------------------------------------------------------- launch / windows ------

        public ActionResult Launch()
        {
            // Not running -> start it; running (even minimised or in the tray) -> show it.
            return this._state.Running ? this.BringToFront() : this.Done(this._cli.Launch(), "Launch");
        }

        public ActionResult BringToFront()
        {
            var s = this._state;
            if (!s.Running)
            {
                return this.Done(this._cli.Launch(), "BringToFront(launch)");
            }
            var target = s.MainWindow;
            if (target == IntPtr.Zero)
            {
                // Main window hidden in the tray: starting TeamViewer.exe again shows it.
                return this.Done(this._cli.Launch(), "BringToFront(show)");
            }
            return this.Done(Win32.Activate(target) ? ActionResult.Ok("win32") : ActionResult.Fail("SetForegroundWindow refusé", "win32"), "BringToFront");
        }

        private IntPtr WaitForMainWindow(int timeoutMs)
        {
            var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < until)
            {
                var scan = TeamViewerWindows.Run(this.C.Windows);
                if (scan.Main != IntPtr.Zero)
                {
                    return scan.Main;
                }
                Thread.Sleep(250);
            }
            return IntPtr.Zero;
        }

        // "Nouvelle session": clicks the real "Nouvelle session" button of the Sessions page, which creates a
        // session. The button only exists on the Sessions tab, so that tab is selected first when needed.
        public ActionResult OpenNewSession()
        {
            if (!this._state.Running || this._state.MainWindow == IntPtr.Zero)
            {
                this._cli.Launch();
            }
            var main = this.WaitForMainWindow(10000);
            if (main == IntPtr.Zero)
            {
                return this.Done(ActionResult.Fail("fenêtre principale TeamViewer introuvable", "win32"), "NewSession");
            }
            Win32.Activate(main);

            var r = this._uia.ClickNewSession(main);
            if (!r.Success)
            {
                // Probably on another tab (ID TeamViewer): open Sessions, give the page time to render, retry.
                if (this._uia.OpenSessionsTab(main).Success)
                {
                    for (var i = 0; i < 6 && !r.Success; i++)
                    {
                        Thread.Sleep(400);
                        r = this._uia.ClickNewSession(main);
                    }
                }
            }
            return this.Done(r, "NewSession");
        }

        // Puts the cursor in "Rechercher et se connecter" (type an ID, then Enter): what "Nouvelle session"
        // used to do. Still used by the quick-connect key when no favourite ID is configured.
        public ActionResult FocusConnectField()
        {
            if (!this._state.Running || this._state.MainWindow == IntPtr.Zero)
            {
                this._cli.Launch();
            }
            var main = this.WaitForMainWindow(10000);
            if (main == IntPtr.Zero)
            {
                return this.Done(ActionResult.Fail("fenêtre principale TeamViewer introuvable", "win32"), "FocusConnectField");
            }
            Win32.Activate(main);
            var r = this._uia.FocusSearchField(main);
            if (!r.Success)
            {
                r = this._window.SendKeys(main, this.C.Keys.SearchAndConnect, "shortcut");
            }
            return this.Done(r, "FocusConnectField");
        }

        public ActionResult Connect(string deviceId) => this.Done(this._cli.Connect(deviceId), "Connect");

        public bool IsMeetingAvailable(bool refresh = false)
        {
            if (!refresh && (DateTime.UtcNow - this._meetingCache.at).TotalSeconds < 60)
            {
                return this._meetingCache.value;
            }
            var main = this._state.MainWindow;
            if (main == IntPtr.Zero)
            {
                return this._meetingCache.value; // unknown: keep last answer
            }
            var value = this._uia.MeetingAvailable(main);
            this._meetingCache = (DateTime.UtcNow, value);
            return value;
        }

        public ActionResult CreateMeeting()
        {
            if (!this._state.Running)
            {
                this._cli.Launch();
            }
            var main = this.WaitForMainWindow(10000);
            if (main == IntPtr.Zero)
            {
                return this.Done(ActionResult.Fail("fenêtre principale introuvable", "uia"), "CreateMeeting");
            }
            Win32.Activate(main);
            if (!this.IsMeetingAvailable(refresh: true))
            {
                return this.Done(ActionResult.Unavailable("Réunion absente de cette version/licence TeamViewer"), "CreateMeeting");
            }
            return this.Done(this._uia.OpenMeeting(main), "CreateMeeting");
        }

        public ActionResult Disconnect() => this.Done(this._window.Close(this._state.ActiveSession), "Disconnect");

        // ---------------------------------------------------------------------- audio ------

        private ActionResult SetMic(bool mute)
        {
            var mode = this.C.Microphone.Mode?.ToLowerInvariant();
            if (mode == "windows")
            {
                return this._audio.SetWindowsMic(mute);
            }
            var r = this._audio.SetAppMic(mute);
            if (!r.Success && mode == "teamviewerorwindows")
            {
                r = this._audio.SetWindowsMic(mute);
            }
            return r;
        }

        public ActionResult MuteMicrophone() => this.Done(this.SetMic(true), "MuteMicrophone");

        public ActionResult UnmuteMicrophone() => this.Done(this.SetMic(false), "UnmuteMicrophone");

        public Tri GetMicrophoneState() => this._state.Microphone;

        public ActionResult SetWindowsMicrophone(bool mute) => this.Done(this._audio.SetWindowsMic(mute), "WindowsMic");

        public ActionResult MuteRemoteAudio() => this.Done(this._audio.SetAppRender(true), "MuteRemoteAudio");

        public ActionResult UnmuteRemoteAudio() => this.Done(this._audio.SetAppRender(false), "UnmuteRemoteAudio");

        public Tri GetRemoteAudioState() => this._state.RemoteAudio;

        public ActionResult StepRemoteVolume(int detents) => this.Done(this._audio.StepVolume(detents * this.C.VolumeStep), "RemoteVolume");

        // ------------------------------------------------------------------ recording ------

        public Tri GetRecordingState() => this._state.Recording;

        public ActionResult StartRecording()
        {
            if (!this._state.InSession)
            {
                return ActionResult.Fail("aucune session à enregistrer", "guard");
            }
            if (this._state.Recording == Tri.On || (DateTime.UtcNow - this._recordingRequestedAt).TotalSeconds < 5)
            {
                return ActionResult.Fail("enregistrement déjà en cours", "guard"); // anti double start
            }
            var r = this.ShortcutOrScript(this.C.Keys.RecordStart, this.C.Scripts.RecordStart, this._state.ActiveSession.Handle);
            if (r.Success)
            {
                this._recordingRequested = true;
                this._recordingRequestedAt = DateTime.UtcNow;
            }
            return this.Done(r, "StartRecording");
        }

        public ActionResult StopRecording()
        {
            if (this._state.Recording != Tri.On && !this._recordingRequested)
            {
                return ActionResult.Fail("aucun enregistrement en cours", "guard");
            }
            var r = this.ShortcutOrScript(this.C.Keys.RecordStop, this.C.Scripts.RecordStop, this._state.ActiveSession?.Handle ?? IntPtr.Zero);
            if (r.Success)
            {
                this._recordingRequested = false;
                this._recordingRequestedAt = DateTime.MinValue;
            }
            return this.Done(r, "StopRecording");
        }

        public ActionResult OpenRecordings() =>
            this.Done(this._cli.OpenFolder(this.RecordingsFolder, this._state.LatestRecording), "OpenRecordings");

        // ------------------------------------------------------------ capture / view ------

        public ActionResult TakeScreenshot()
        {
            var s = this._state;
            var target = s.ActiveSession?.Handle ?? s.MainWindow;
            var folder = ConfigStore.Expand(this.C.CapturesPath);
            return this.Done(this._window.Capture(target, folder), s.InSession ? "Capture(session)" : "Capture(main)");
        }

        private ActionResult ShortcutOrScript(string chord, string script, IntPtr target)
        {
            if (!string.IsNullOrWhiteSpace(chord))
            {
                return this._window.SendKeys(target, chord);
            }
            if (ScriptRunner.IsConfigured(script))
            {
                return ScriptRunner.Run(script, this.C.AutoHotkeyPath, "script");
            }
            return ActionResult.Unavailable("aucune méthode officielle : configurer keys/scripts dans config.json");
        }

        private IntPtr SessionHandle => this._state.ActiveSession?.Handle ?? IntPtr.Zero;

        public bool IsZoomConfigured => !string.IsNullOrWhiteSpace(this.C.Keys.ZoomIn) || ScriptRunner.IsConfigured(this.C.Scripts.ZoomIn);

        public bool IsRecordingConfigured => !string.IsNullOrWhiteSpace(this.C.Keys.RecordStart) || ScriptRunner.IsConfigured(this.C.Scripts.RecordStart);

        public bool IsFullscreenNative => !string.IsNullOrWhiteSpace(this.C.Keys.Fullscreen) || ScriptRunner.IsConfigured(this.C.Scripts.Fullscreen);

        public ActionResult SetZoom(int percent) =>
            percent == 100
                ? this.Done(this.ShortcutOrScript(this.C.Keys.Zoom100, this.C.Scripts.Zoom100, this.SessionHandle), "Zoom100")
                : ActionResult.Unavailable("seul 100 % est supporté");

        public ActionResult ZoomIn() => this.Done(this.ShortcutOrScript(this.C.Keys.ZoomIn, this.C.Scripts.ZoomIn, this.SessionHandle), "ZoomIn");

        public ActionResult ZoomOut() => this.Done(this.ShortcutOrScript(this.C.Keys.ZoomOut, this.C.Scripts.ZoomOut, this.SessionHandle), "ZoomOut");

        public ActionResult ToggleFullscreen()
        {
            if (this.IsFullscreenNative)
            {
                return this.Done(this.ShortcutOrScript(this.C.Keys.Fullscreen, this.C.Scripts.Fullscreen, this.SessionHandle), "Fullscreen");
            }
            return this.Done(this._window.ToggleMaximize(this._state.ActiveSession), "Fullscreen(window)");
        }

        public ActionResult FitToWindow() =>
            this.Done(this.ShortcutOrScript(this.C.Keys.FitToWindow, "", this.SessionHandle), "FitToWindow");

        public ActionResult Scroll(int detents) => this._window.Scroll(this._state.ActiveSession, detents);

        // --------------------------------------------------------------- capabilities ------

        public IReadOnlyList<Capability> GetCapabilities()
        {
            var s = this._state;
            var installed = this._cli.IsInstalled;
            var agent = this.Agent.IsAvailable;
            var a = this._lastAudio;
            Capability Cap(string f, bool ok, string m, string n = "") => new() { Feature = f, Available = ok, Method = m, Note = n };
            return new List<Capability>
            {
                Cap("TeamViewer détecté", installed, "registre", this._cli.ExecutablePath ?? ""),
                Cap("Lancement application", installed, "TeamViewer.exe"),
                Cap("Premier plan / restauration", true, "Win32"),
                Cap("Connexion ID", installed, this.C.ConnectMethod == "uri" ? "URI teamviewer10://" : "CLI -i <ID>"),
                Cap("Écran nouvelle session", agent, "UI Automation (search-input-field) / Ctrl+K"),
                Cap("Réunion", s.MainWindow != IntPtr.Zero && this.IsMeetingAvailable(), "UI Automation (id=meeting)", s.MainWindow == IntPtr.Zero ? "fenêtre principale non visible" : ""),
                Cap("UI Automation (agent)", agent && this.Agent.Call(MXTeamViewer.Shared.AgentCommands.Ping).Ok, "MXTeamViewerAgent.exe", this.Agent.ExePath),
                Cap("Contrôle microphone par API", false, "-", "aucune API TeamViewer"),
                Cap("Micro TeamViewer (session audio)", a.AppMic != Tri.Unknown, "Core Audio", a.AppMic == Tri.Unknown ? "aucun flux micro TeamViewer actif" : ""),
                Cap("Micro Windows (option)", a.WindowsMic != Tri.Unknown, "Core Audio endpoint"),
                Cap("Son distant", a.AppRender != Tri.Unknown, "Core Audio", a.AppRender == Tri.Unknown ? "aucun flux audio TeamViewer actif" : ""),
                Cap("Enregistrements (dossier)", true, "Explorateur", this.RecordingsFolder),
                Cap("Démarrer/arrêter enregistrement", this.IsRecordingConfigured, "raccourci/script", this.IsRecordingConfigured ? "" : "non configuré (pas d'API)"),
                Cap("Détection enregistrement", true, "fichier .tvs en écriture"),
                Cap("Capture fenêtre de session", agent, "PrintWindow"),
                Cap("Déconnexion (fenêtre)", true, "WM_CLOSE"),
                Cap("Plein écran", true, this.IsFullscreenNative ? "raccourci/script" : "maximiser la fenêtre (fallback)"),
                Cap("Zoom", this.IsZoomConfigured, "raccourci/script", this.IsZoomConfigured ? "" : "non configuré (pas de raccourci documenté)"),
                Cap("Défilement molette", true, "WM_MOUSEWHEEL"),
                Cap("Web API", this.Api.Enabled && this.Api.HasToken, "REST", this.Api.Enabled ? "" : "désactivée"),
            };
        }

        public void Dispose() => this._timer.Dispose();
    }
}
