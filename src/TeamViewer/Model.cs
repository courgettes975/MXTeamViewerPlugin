namespace Loupedeck.MXTeamViewerPlugin
{
    using System;
    using System.Collections.Generic;

    public enum Tri { Unknown, On, Off }

    // Result of every controller call: what happened and which method was used (shown in logs and
    // briefly on the key when it fails).
    public sealed class ActionResult
    {
        public bool Success { get; init; }
        public string Message { get; init; }
        public string Method { get; init; }

        public static ActionResult Ok(string method, string message = "") => new() { Success = true, Method = method, Message = message };

        public static ActionResult Fail(string message, string method = "") => new() { Success = false, Method = method, Message = message };

        public static ActionResult Unavailable(string message) => new() { Success = false, Method = "none", Message = message };

        public override string ToString() => $"{(this.Success ? "OK" : "FAIL")} [{this.Method}] {this.Message}";
    }

    public sealed class SessionWindow
    {
        public IntPtr Handle { get; init; }
        public string Title { get; init; }
        public string RemoteName { get; init; }
        public bool Minimized { get; init; }
        public bool Maximized { get; init; }
    }

    // Immutable snapshot published by the StateMonitor once per second.
    public sealed class TeamViewerState
    {
        public bool Installed { get; init; }
        public bool Running { get; init; }
        public int Pid { get; init; }
        public IntPtr MainWindow { get; init; }
        public IReadOnlyList<SessionWindow> Sessions { get; init; } = Array.Empty<SessionWindow>();
        public bool MeetingActive { get; init; }
        public Tri Microphone { get; init; }          // TeamViewer capture session (or Windows mic in windows mode)
        public string MicrophoneSource { get; init; } = "";
        public Tri RemoteAudio { get; init; }         // TeamViewer playback session
        public float RemoteVolume { get; init; } = -1;
        public Tri WindowsMicrophone { get; init; }
        public Tri Recording { get; init; }
        public string LatestRecording { get; init; } = "";

        public bool InSession => this.Sessions.Count > 0;

        public SessionWindow ActiveSession => this.Sessions.Count > 0 ? this.Sessions[0] : null;

        // Cheap fingerprint used to repaint keys only when something visible changed.
        public string Fingerprint =>
            $"{this.Running}|{this.Sessions.Count}|{this.ActiveSession?.Handle}|{this.ActiveSession?.Maximized}|{this.MeetingActive}|{this.Microphone}|{this.RemoteAudio}|{this.RemoteVolume:0.00}|{this.WindowsMicrophone}|{this.Recording}";

        public static readonly TeamViewerState Empty = new();
    }

    public sealed class Capability
    {
        public string Feature { get; init; }
        public bool Available { get; init; }
        public string Method { get; init; }
        public string Note { get; init; }
    }
}
