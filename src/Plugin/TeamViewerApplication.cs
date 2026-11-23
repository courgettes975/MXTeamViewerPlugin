namespace Loupedeck.MXTeamViewerPlugin
{
    // Required by the Logi Plugin Service loader even for universal plugins (it refuses a plugin
    // without a ClientApplication subclass). The plugin is universal (HasNoApplication = true) so
    // its keys also work when TeamViewer is not in the foreground ("lancer TeamViewer").
    public class TeamViewerApplication : ClientApplication
    {
        public TeamViewerApplication()
        {
        }

        protected override string GetProcessName() => "";

        protected override string GetBundleName() => "";

        public override ClientApplicationStatus GetApplicationStatus() => ClientApplicationStatus.Unknown;
    }
}
