namespace MXTeamViewer.Agent
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using MXTeamViewer.Shared;

    // MXTeamViewerAgent.exe
    //   (no args)          server mode: JSON requests on stdin, responses on stdout (used by the plugin)
    //   dump               prints the UI Automation tree of every TeamViewer window (calibration)
    //   audio              prints the TeamViewer audio sessions and the Windows mic state (read-only)
    //   help
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            var mode = args.Length > 0 ? args[0].ToLowerInvariant() : "server";
            try
            {
                switch (mode)
                {
                    case "server":
                        return Serve();
                    case "dump":
                        foreach (var hwnd in TeamViewerWindowHandles())
                        {
                            Console.WriteLine(Uia.Dump(hwnd, 400));
                        }
                        return 0;
                    case "audio":
                        foreach (var kv in CoreAudio.Snapshot(new[] { "TeamViewer" }))
                        {
                            Console.WriteLine($"{kv.Key} = {kv.Value}");
                        }
                        return 0;
                    default:
                        Console.WriteLine("MXTeamViewerAgent [server|dump|audio]");
                        return 0;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        // Every visible top-level window owned by a TeamViewer process.
        private static IEnumerable<IntPtr> TeamViewerWindowHandles()
        {
            var pids = Process.GetProcessesByName("TeamViewer").Select(p => (uint)p.Id).ToHashSet();
            return Native.TopLevelWindows().Where(h => pids.Contains(Native.ProcessIdOf(h)) && Native.IsWindowVisible(h)).ToList();
        }

        private static int Serve()
        {
            string line;
            while ((line = Console.In.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                AgentResponse response;
                AgentRequest request = null;
                try
                {
                    request = JsonSerializer.Deserialize<AgentRequest>(line, AgentJson.Options);
                    response = Handle(request);
                }
                catch (Exception ex)
                {
                    response = new AgentResponse { Ok = false, Message = ex.GetType().Name + ": " + ex.Message };
                }
                response.Id = request?.Id ?? 0;
                Console.Out.WriteLine(JsonSerializer.Serialize(response, AgentJson.Options));
                Console.Out.Flush();
            }
            return 0;
        }

        private static string Arg(AgentRequest r, string name, string fallback = "") =>
            r.Args != null && r.Args.TryGetValue(name, out var v) && v != null ? v : fallback;

        private static IntPtr Hwnd(AgentRequest r) => new(long.Parse(Arg(r, "hwnd", "0"), CultureInfo.InvariantCulture));

        private static string[] List(AgentRequest r, string name) =>
            Arg(r, name).Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        private static bool Bool(AgentRequest r, string name) => string.Equals(Arg(r, name), "true", StringComparison.OrdinalIgnoreCase);

        private static AgentResponse Handle(AgentRequest r)
        {
            switch (r.Cmd)
            {
                case AgentCommands.Ping:
                    return Ok("pong", ("version", typeof(Program).Assembly.GetName().Version?.ToString() ?? "?"));

                case AgentCommands.UiaExists:
                    return Result(Uia.Find(Hwnd(r), Arg(r, "automationId"), List(r, "names")) != null, "found", "not found");

                case AgentCommands.UiaInvoke:
                    return Result(Uia.Invoke(Hwnd(r), Arg(r, "automationId"), List(r, "names"), out var how), how, how);

                case AgentCommands.UiaFocus:
                    return Result(Uia.Focus(Hwnd(r), Arg(r, "automationId"), List(r, "names")), "focused", "not found");

                case AgentCommands.UiaDump:
                    return Ok("ok", ("text", Uia.Dump(Hwnd(r), 600)));

                case AgentCommands.AudioGet:
                    return Ok("ok", CoreAudio.Snapshot(List(r, "processes")).Select(kv => (kv.Key, kv.Value)).ToArray());

                case AgentCommands.AudioSetAppCapture:
                {
                    var n = CoreAudio.SetAppMute(List(r, "processes"), capture: true, Bool(r, "mute"));
                    return Result(n > 0, $"{n} session(s)", "no TeamViewer capture session (no active microphone stream)");
                }

                case AgentCommands.AudioSetAppRender:
                {
                    var n = CoreAudio.SetAppMute(List(r, "processes"), capture: false, Bool(r, "mute"));
                    return Result(n > 0, $"{n} session(s)", "no TeamViewer playback session (no remote audio stream)");
                }

                case AgentCommands.AudioStepAppRender:
                {
                    var delta = float.Parse(Arg(r, "delta", "0"), CultureInfo.InvariantCulture);
                    var level = CoreAudio.StepAppVolume(List(r, "processes"), delta);
                    return level < 0
                        ? Fail("no TeamViewer playback session")
                        : Ok("ok", ("volume", level.ToString("0.00", CultureInfo.InvariantCulture)));
                }

                case AgentCommands.AudioSetWindowsMic:
                    return Result(CoreAudio.SetDefaultMicMute(Bool(r, "mute")), "ok", "no default capture device");

                case AgentCommands.CaptureWindow:
                    return Result(WindowCapture.Save(Hwnd(r), Arg(r, "path"), out var info), info, info);

                default:
                    return Fail("unknown command " + r.Cmd);
            }
        }

        private static AgentResponse Ok(string message, params (string Key, string Value)[] data)
        {
            var response = new AgentResponse { Ok = true, Message = message };
            foreach (var (k, v) in data)
            {
                response.Data[k] = v;
            }
            return response;
        }

        private static AgentResponse Fail(string message) => new() { Ok = false, Message = message };

        private static AgentResponse Result(bool ok, string okMessage, string failMessage) => ok ? Ok(okMessage) : Fail(failMessage);
    }
}
