namespace MXTeamViewer.Shared
{
    using System.Collections.Generic;
    using System.Text.Json;

    // Line-delimited JSON protocol between the plugin (client) and MXTeamViewerAgent.exe (server).
    // One request per line on the agent's stdin, one response per line on its stdout.
    // Values are strings on purpose: no polymorphic JSON, trivially forward compatible.
    // NEVER put a password or token in a request: the agent does not need any.
    public sealed class AgentRequest
    {
        public long Id { get; set; }
        public string Cmd { get; set; }
        public Dictionary<string, string> Args { get; set; } = new();
    }

    public sealed class AgentResponse
    {
        public long Id { get; set; }
        public bool Ok { get; set; }
        public string Message { get; set; }
        public Dictionary<string, string> Data { get; set; } = new();
    }

    public static class AgentCommands
    {
        public const string Ping = "ping";

        // UI Automation (TeamViewer main window only: session windows expose nothing).
        public const string UiaExists = "uia.exists";     // args: hwnd, automationId, names (|-separated)
        public const string UiaInvoke = "uia.invoke";     // same args; Invoke / Select / Focus
        public const string UiaFocus = "uia.focus";       // same args; SetFocus
        public const string UiaDump = "uia.dump";         // args: hwnd -> data.text

        // Windows Core Audio. args: processes (|-separated names without .exe)
        public const string AudioGet = "audio.get";
        public const string AudioSetAppCapture = "audio.app.capture.mute";   // args: processes, mute
        public const string AudioSetAppRender = "audio.app.render.mute";     // args: processes, mute
        public const string AudioStepAppRender = "audio.app.render.step";    // args: processes, delta (0..1 signed)
        public const string AudioSetWindowsMic = "audio.windows.mic.mute";   // args: mute

        // Screen capture of one window to PNG. args: hwnd, path
        public const string CaptureWindow = "capture.window";
    }

    public static class AgentJson
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };
    }
}
