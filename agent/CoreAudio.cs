namespace MXTeamViewer.Agent
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.InteropServices;

    // Windows Core Audio (WASAPI session API) without any NuGet dependency.
    //
    // Three DIFFERENT things, never mixed:
    //   * TeamViewer "remote audio"  = audio sessions of TeamViewer.exe on RENDER endpoints
    //                                  (what the remote computer plays, heard locally).
    //   * TeamViewer "microphone"    = audio sessions of TeamViewer.exe on CAPTURE endpoints
    //                                  (what TeamViewer records from the local mic). Only exists
    //                                  while TeamViewer has a microphone stream open.
    //   * Windows microphone         = mute of the default capture endpoint (affects every app).
    // Windows speakers (default render endpoint volume) are never touched.
    internal static class CoreAudio
    {
        private static readonly Guid EventContext = new("6a3b7c2e-1f0d-4e55-9a3c-2b7f0c9d4e11");

        public static Dictionary<string, string> Snapshot(string[] processes)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            var render = Sessions(EDataFlow.eRender, processes);
            var capture = Sessions(EDataFlow.eCapture, processes);
            try
            {
                d["render.found"] = Bool(render.Count > 0);
                d["render.muted"] = Bool(render.Count > 0 && render.All(s => s.Muted));
                d["render.volume"] = render.Count > 0 ? render.Max(s => s.Volume).ToString("0.00", CultureInfo.InvariantCulture) : "";
                d["render.sessions"] = string.Join("; ", render.Select(s => s.Describe()));
                d["capture.found"] = Bool(capture.Count > 0);
                d["capture.muted"] = Bool(capture.Count > 0 && capture.All(s => s.Muted));
                d["capture.sessions"] = string.Join("; ", capture.Select(s => s.Describe()));
                var mic = DefaultEndpointVolume(EDataFlow.eCapture);
                d["winmic.found"] = Bool(mic != null);
                if (mic != null)
                {
                    Check(mic.GetMute(out var muted));
                    d["winmic.muted"] = Bool(muted);
                    Marshal.ReleaseComObject(mic);
                }
            }
            finally
            {
                Release(render);
                Release(capture);
            }
            return d;
        }

        public static int SetAppMute(string[] processes, bool capture, bool mute)
        {
            var sessions = Sessions(capture ? EDataFlow.eCapture : EDataFlow.eRender, processes);
            try
            {
                var ctx = EventContext;
                foreach (var s in sessions)
                {
                    Check(s.Volume2.SetMute(mute, ref ctx));
                }
                return sessions.Count;
            }
            finally
            {
                Release(sessions);
            }
        }

        // Returns the new level (0..1) or -1 when no session exists.
        public static float StepAppVolume(string[] processes, float delta)
        {
            var sessions = Sessions(EDataFlow.eRender, processes);
            try
            {
                if (sessions.Count == 0)
                {
                    return -1;
                }
                var ctx = EventContext;
                var level = Math.Clamp(sessions.Max(s => s.Volume) + delta, 0f, 1f);
                foreach (var s in sessions)
                {
                    Check(s.Volume2.SetMasterVolume(level, ref ctx));
                    if (level > 0 && s.Muted && delta > 0)
                    {
                        Check(s.Volume2.SetMute(false, ref ctx));
                    }
                }
                return level;
            }
            finally
            {
                Release(sessions);
            }
        }

        public static bool SetDefaultMicMute(bool mute)
        {
            var mic = DefaultEndpointVolume(EDataFlow.eCapture);
            if (mic == null)
            {
                return false;
            }
            try
            {
                var ctx = EventContext;
                Check(mic.SetMute(mute, ref ctx));
                return true;
            }
            finally
            {
                Marshal.ReleaseComObject(mic);
            }
        }

        // ---------------------------------------------------------------------------------------

        private sealed class AppSession
        {
            public ISimpleAudioVolume Volume2;
            public string Process;
            public uint Pid;
            public bool Muted;
            public float Volume;

            public string Describe() => $"{this.Process}({this.Pid}) vol={this.Volume:0.00} mute={this.Muted}";
        }

        private static string Bool(bool b) => b ? "true" : "false";

        private static void Check(int hr)
        {
            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        private static void Release(List<AppSession> sessions)
        {
            foreach (var s in sessions)
            {
                if (s.Volume2 != null)
                {
                    Marshal.ReleaseComObject(s.Volume2);
                }
            }
        }

        private static IMMDeviceEnumerator CreateEnumerator() =>
            (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"))!);

        private static IAudioEndpointVolume DefaultEndpointVolume(EDataFlow flow)
        {
            var enumerator = CreateEnumerator();
            try
            {
                if (enumerator.GetDefaultAudioEndpoint(flow, ERole.eCommunications, out var device) < 0 || device == null)
                {
                    if (enumerator.GetDefaultAudioEndpoint(flow, ERole.eConsole, out device) < 0 || device == null)
                    {
                        return null;
                    }
                }
                var iid = typeof(IAudioEndpointVolume).GUID;
                Check(device.Activate(ref iid, 23 /* CLSCTX_ALL */, IntPtr.Zero, out var obj));
                Marshal.ReleaseComObject(device);
                return (IAudioEndpointVolume)obj;
            }
            finally
            {
                Marshal.ReleaseComObject(enumerator);
            }
        }

        private static List<AppSession> Sessions(EDataFlow flow, string[] processes)
        {
            var wanted = new HashSet<string>(processes ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var result = new List<AppSession>();
            var names = new Dictionary<uint, string>();
            var enumerator = CreateEnumerator();
            try
            {
                Check(enumerator.EnumAudioEndpoints(flow, 0x1 /* DEVICE_STATE_ACTIVE */, out var devices));
                Check(devices.GetCount(out var count));
                for (uint i = 0; i < count; i++)
                {
                    Check(devices.Item(i, out var device));
                    try
                    {
                        var iid = typeof(IAudioSessionManager2).GUID;
                        if (device.Activate(ref iid, 23, IntPtr.Zero, out var mgrObj) < 0)
                        {
                            continue;
                        }
                        var manager = (IAudioSessionManager2)mgrObj;
                        Check(manager.GetSessionEnumerator(out var sessionEnum));
                        Check(sessionEnum.GetCount(out var n));
                        for (var k = 0; k < n; k++)
                        {
                            Check(sessionEnum.GetSession(k, out var ctlObj));
                            var control = (IAudioSessionControl2)ctlObj;
                            control.GetProcessId(out var pid);
                            if (pid == 0)
                            {
                                Marshal.ReleaseComObject(control);
                                continue;
                            }
                            if (!names.TryGetValue(pid, out var name))
                            {
                                try { name = Process.GetProcessById((int)pid).ProcessName; } catch { name = ""; }
                                names[pid] = name;
                            }
                            if (!wanted.Contains(name))
                            {
                                Marshal.ReleaseComObject(control);
                                continue;
                            }
                            // ISimpleAudioVolume is obtained by QueryInterface on the session control.
                            var volume = (ISimpleAudioVolume)control;
                            Check(volume.GetMute(out var muted));
                            Check(volume.GetMasterVolume(out var level));
                            result.Add(new AppSession { Volume2 = volume, Process = name, Pid = pid, Muted = muted, Volume = level });
                        }
                        Marshal.ReleaseComObject(sessionEnum);
                        Marshal.ReleaseComObject(manager);
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(device);
                    }
                }
                Marshal.ReleaseComObject(devices);
            }
            finally
            {
                Marshal.ReleaseComObject(enumerator);
            }
            return result;
        }

        // ---- COM declarations (vtable order matters) -----------------------------------------

        private enum EDataFlow { eRender = 0, eCapture = 1, eAll = 2 }

        private enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IMMDeviceCollection devices);
            [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);
            [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
            [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
            [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
        }

        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceCollection
        {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int Item(uint index, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
            [PreserveSig] int OpenPropertyStore(int access, out IntPtr properties);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
            [PreserveSig] int GetState(out int state);
        }

        [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionManager2
        {
            // IAudioSessionManager
            [PreserveSig] int GetAudioSessionControl(IntPtr sessionGuid, int flags, out IntPtr control);
            [PreserveSig] int GetSimpleAudioVolume(IntPtr sessionGuid, int flags, out IntPtr volume);
            // IAudioSessionManager2
            [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnum);
            [PreserveSig] int RegisterSessionNotification(IntPtr notification);
            [PreserveSig] int UnregisterSessionNotification(IntPtr notification);
            [PreserveSig] int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionId, IntPtr notification);
            [PreserveSig] int UnregisterDuckNotification(IntPtr notification);
        }

        [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionEnumerator
        {
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int GetSession(int index, [MarshalAs(UnmanagedType.IUnknown)] out object session);
        }

        [ComImport, Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionControl2
        {
            // IAudioSessionControl
            [PreserveSig] int GetState(out int state);
            [PreserveSig] int GetDisplayName(out IntPtr name);
            [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid context);
            [PreserveSig] int GetIconPath(out IntPtr path);
            [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid context);
            [PreserveSig] int GetGroupingParam(out Guid param);
            [PreserveSig] int SetGroupingParam(ref Guid param, ref Guid context);
            [PreserveSig] int RegisterAudioSessionNotification(IntPtr client);
            [PreserveSig] int UnregisterAudioSessionNotification(IntPtr client);
            // IAudioSessionControl2
            [PreserveSig] int GetSessionIdentifier(out IntPtr id);
            [PreserveSig] int GetSessionInstanceIdentifier(out IntPtr id);
            [PreserveSig] int GetProcessId(out uint pid);
            [PreserveSig] int IsSystemSoundsSession();
            [PreserveSig] int SetDuckingPreference(bool optOut);
        }

        [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ISimpleAudioVolume
        {
            [PreserveSig] int SetMasterVolume(float level, ref Guid context);
            [PreserveSig] int GetMasterVolume(out float level);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        }

        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int GetChannelCount(out uint count);
            [PreserveSig] int SetMasterVolumeLevel(float levelDb, ref Guid context);
            [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
            [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
            [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid context);
            [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
            [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDb);
            [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        }
    }
}
