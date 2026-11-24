namespace Loupedeck.MXTeamViewerPlugin
{
    using System.Collections.Generic;

    // config.json (%LOCALAPPDATA%\MXTeamViewerPlugin\config.json). Contains NO secret:
    // TeamViewer IDs are not credentials. Passwords are never supported; the optional Web API token
    // lives in the DPAPI store (see SecretStore / scripts\Set-ApiToken.ps1).
    public sealed class PluginConfig
    {
        public string TeamviewerPath { get; set; } = "AUTO";
        public string RecordingsPath { get; set; } = "AUTO";
        public string CapturesPath { get; set; } = @"%USERPROFILE%\Documents\TeamViewer Captures";
        public string LogLevel { get; set; } = "Info";

        // "cli" = TeamViewer.exe -i <ID> (documented), "uri" = teamviewer10://control?device=<ID>.
        public string ConnectMethod { get; set; } = "cli";

        // zoom | volume | navigation (default mode of the generic "Molette TeamViewer" adjustment).
        public string DialMode { get; set; } = "zoom";

        public bool DisconnectProtection { get; set; } = true;
        public int DisconnectConfirmMs { get; set; } = 2000;

        public MicrophoneConfig Microphone { get; set; } = new();
        public List<string> AudioProcesses { get; set; } = new() { "TeamViewer" };
        public float VolumeStep { get; set; } = 0.05f;
        public int ScrollLinesPerDetent { get; set; } = 1;

        public FavoriteConfig QuickConnect { get; set; } = new() { Name = "Connexion rapide", Icon = "star" };

        public List<FavoriteConfig> Favorites { get; set; } = new()
        {
            new() { Name = "PC Bureau", Icon = "desktop" },
            new() { Name = "PC Portable", Icon = "laptop" },
            new() { Name = "Serveur", Icon = "server" },
            new() { Name = "Client 1", Icon = "client" },
            new() { Name = "Client 2", Icon = "client" },
        };

        public WindowRules Windows { get; set; } = new();
        public UiaSelectors Uia { get; set; } = new();
        public KeyBindings Keys { get; set; } = new();
        public ScriptHooks Scripts { get; set; } = new();
        public string AutoHotkeyPath { get; set; } = "";

        public bool ApiEnabled { get; set; } = false;

        // Contextual keys "Contexte 1..9": action ids shown when TeamViewer is idle / in a session.
        public ContextProfiles Profiles { get; set; } = new();
    }

    public sealed class ContextProfiles
    {
        public List<string> Idle { get; set; } = new()
        {
            "tv.open", "tv.quick", "fav.1", "fav.2", "tv.recordings", "tv.newsession", "fav.3", "tv.meeting", "tv.diagnostics",
        };

        public List<string> Session { get; set; } = new()
        {
            "tv.mic", "tv.audio", "tv.capture", "tv.record", "tv.fullscreen", "zoom.in", "tv.disconnect", "zoom.out", "tv.open",
        };
    }

    public sealed class MicrophoneConfig
    {
        // teamviewer         : mute only TeamViewer's capture session (default)
        // windows            : mute the Windows default microphone (every application!)
        // teamviewerOrWindows: TeamViewer session when it exists, otherwise Windows mic
        public string Mode { get; set; } = "teamviewer";
    }

    public sealed class FavoriteConfig
    {
        public string Name { get; set; } = "";
        public string TeamviewerId { get; set; } = "";
        public string Alias { get; set; } = "";
        // desktop | laptop | server | client | star
        public string Icon { get; set; } = "desktop";
    }

    // How TeamViewer windows are recognised (verified on TeamViewer 15.83.7, adjustable).
    public sealed class WindowRules
    {
        public List<string> MainWindowClasses { get; set; } = new() { "MainWindowOne" };
        public List<string> SessionWindowClasses { get; set; } = new() { "sciter-frame" };
        public string SessionTitleSuffix { get; set; } = " - TeamViewer";
        public List<string> MeetingWindowClasses { get; set; } = new() { "BlizzMeetingCommandBar", "ParticipantsTabContainer" };
    }

    public sealed class ElementSelector
    {
        public string AutomationId { get; set; } = "";
        public List<string> Names { get; set; } = new();
    }

    public sealed class UiaSelectors
    {
        public ElementSelector SearchField { get; set; } = new()
        {
            AutomationId = "search-input-field",
            Names = new() { "Rechercher et se connecter", "Search and connect" },
        };

        // Violet "Nouvelle session" button of the Sessions page (creates a session waiting for a participant).
        public ElementSelector NewSessionButton { get; set; } = new()
        {
            AutomationId = "",
            Names = new() { "Nouvelle session", "New session" },
        };

        // "Sessions" tab of the main window (the button above only exists on that tab).
        public ElementSelector SessionsTab { get; set; } = new()
        {
            AutomationId = "Pivot14900-Tab1",
            Names = new() { "Sessions" },
        };

        public ElementSelector Meeting { get; set; } = new()
        {
            AutomationId = "meeting",
            Names = new() { "Accéder à Réunion", "Go to Meeting", "Réunion", "Meeting" },
        };
    }

    // Keyboard shortcuts sent to TeamViewer. Format "Ctrl+Alt+F", "Ctrl+K", "F11"...
    // Empty = no documented TeamViewer shortcut => the action uses its fallback or shows N/D.
    public sealed class KeyBindings
    {
        // Shown by the TeamViewer 15.83 UI next to "Rechercher et se connecter".
        public string SearchAndConnect { get; set; } = "Ctrl+K";
        public string Fullscreen { get; set; } = "";
        public string FitToWindow { get; set; } = "";
        public string Zoom100 { get; set; } = "";
        public string ZoomIn { get; set; } = "";
        public string ZoomOut { get; set; } = "";
        public string RecordStart { get; set; } = "";
        public string RecordStop { get; set; } = "";
    }

    // Last-resort hooks: path to a .ps1, .ahk (AutoHotkey v2) or .exe run for an action.
    public sealed class ScriptHooks
    {
        public string RecordStart { get; set; } = "";
        public string RecordStop { get; set; } = "";
        public string Fullscreen { get; set; } = "";
        public string ZoomIn { get; set; } = "";
        public string ZoomOut { get; set; } = "";
        public string Zoom100 { get; set; } = "";
    }
}
