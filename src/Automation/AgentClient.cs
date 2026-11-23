namespace Loupedeck.MXTeamViewerPlugin
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using MXTeamViewer.Shared;

    // Client for bin\agent\MXTeamViewerAgent.exe (UI Automation, Core Audio, window capture).
    // Started lazily, restarted automatically if it exits, killed on plugin unload.
    internal sealed class AgentClient : IDisposable
    {
        private readonly object _sync = new();
        private readonly ConcurrentDictionary<long, TaskCompletionSource<AgentResponse>> _pending = new();
        private readonly string _exePath;
        private Process _process;
        private long _nextId;
        private DateTime _lastStartFailure = DateTime.MinValue;

        public AgentClient(string pluginDirectory)
        {
            this._exePath = Path.Combine(pluginDirectory, "agent", "MXTeamViewerAgent.exe");
        }

        public string ExePath => this._exePath;

        public bool IsAvailable => File.Exists(this._exePath);

        public bool IsRunning
        {
            get
            {
                lock (this._sync)
                {
                    return this._process is { HasExited: false };
                }
            }
        }

        public AgentResponse Call(string cmd, Dictionary<string, string> args = null, int timeoutMs = 3000)
        {
            if (!this.EnsureStarted())
            {
                return new AgentResponse { Ok = false, Message = "agent unavailable: " + this._exePath };
            }
            var request = new AgentRequest { Id = Interlocked.Increment(ref this._nextId), Cmd = cmd, Args = args ?? new() };
            var tcs = new TaskCompletionSource<AgentResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            this._pending[request.Id] = tcs;
            try
            {
                lock (this._sync)
                {
                    this._process.StandardInput.WriteLine(JsonSerializer.Serialize(request, AgentJson.Options));
                    this._process.StandardInput.Flush();
                }
                if (tcs.Task.Wait(timeoutMs))
                {
                    return tcs.Task.Result;
                }
                TvLog.Warning($"Agent timeout on {cmd}");
                return new AgentResponse { Ok = false, Message = "agent timeout" };
            }
            catch (Exception ex)
            {
                TvLog.Warning($"Agent call {cmd} failed", ex);
                this.Kill();
                return new AgentResponse { Ok = false, Message = ex.Message };
            }
            finally
            {
                this._pending.TryRemove(request.Id, out _);
            }
        }

        private bool EnsureStarted()
        {
            lock (this._sync)
            {
                if (this._process is { HasExited: false })
                {
                    return true;
                }
                if (!this.IsAvailable || (DateTime.UtcNow - this._lastStartFailure).TotalSeconds < 10)
                {
                    return false;
                }
                try
                {
                    var psi = new ProcessStartInfo(this._exePath)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                        WorkingDirectory = Path.GetDirectoryName(this._exePath)!,
                    };
                    var p = Process.Start(psi);
                    if (p == null)
                    {
                        throw new InvalidOperationException("Process.Start returned null");
                    }
                    p.OutputDataReceived += this.OnOutput;
                    p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) { TvLog.Warning("agent stderr: " + e.Data); } };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    this._process = p;
                    TvLog.Info($"Agent started (pid {p.Id})");
                    return true;
                }
                catch (Exception ex)
                {
                    this._lastStartFailure = DateTime.UtcNow;
                    TvLog.Error("Cannot start the automation agent", ex);
                    return false;
                }
            }
        }

        private void OnOutput(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data))
            {
                return;
            }
            try
            {
                var response = JsonSerializer.Deserialize<AgentResponse>(e.Data, AgentJson.Options);
                if (response != null && this._pending.TryGetValue(response.Id, out var tcs))
                {
                    tcs.TrySetResult(response);
                }
            }
            catch (Exception ex)
            {
                TvLog.Debug("agent output ignored: " + ex.Message);
            }
        }

        private void Kill()
        {
            lock (this._sync)
            {
                try
                {
                    if (this._process is { HasExited: false })
                    {
                        this._process.Kill();
                    }
                }
                catch
                {
                    // already gone
                }
                this._process?.Dispose();
                this._process = null;
            }
        }

        public void Dispose()
        {
            try
            {
                lock (this._sync)
                {
                    this._process?.StandardInput.Close(); // EOF => agent exits cleanly
                }
                this._process?.WaitForExit(500);
            }
            catch
            {
                // ignore
            }
            this.Kill();
        }
    }
}
