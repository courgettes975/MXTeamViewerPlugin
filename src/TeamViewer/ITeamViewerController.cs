namespace Loupedeck.MXTeamViewerPlugin
{
    using System.Collections.Generic;

    // The only surface the key/dial actions talk to. Each member is routed by TeamViewerController
    // to the best implementation actually available (API > CLI/URI > shortcut > UI Automation >
    // Core Audio/Win32 > script), so one method can be swapped without touching the actions.
    public interface ITeamViewerController
    {
        TeamViewerState State { get; }

        ActionResult Launch();
        ActionResult BringToFront();
        ActionResult OpenNewSession();
        ActionResult FocusConnectField();
        ActionResult Connect(string deviceId);
        ActionResult CreateMeeting();
        ActionResult Disconnect();

        ActionResult MuteMicrophone();
        ActionResult UnmuteMicrophone();
        Tri GetMicrophoneState();
        ActionResult SetWindowsMicrophone(bool mute);

        ActionResult MuteRemoteAudio();
        ActionResult UnmuteRemoteAudio();
        Tri GetRemoteAudioState();
        ActionResult StepRemoteVolume(int detents);

        ActionResult StartRecording();
        ActionResult StopRecording();
        Tri GetRecordingState();
        ActionResult OpenRecordings();

        ActionResult TakeScreenshot();

        ActionResult SetZoom(int percent);
        ActionResult ZoomIn();
        ActionResult ZoomOut();
        ActionResult ToggleFullscreen();
        ActionResult FitToWindow();
        ActionResult Scroll(int detents);

        IReadOnlyList<Capability> GetCapabilities();
    }
}
