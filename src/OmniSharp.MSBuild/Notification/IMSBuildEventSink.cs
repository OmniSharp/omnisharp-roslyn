namespace OmniSharp.MSBuild.Notification
{
    public interface IMSBuildEventSink
    {
        void ProjectLoadingStarted(string projectPath);
        void ProjectLoaded(ProjectLoadedEventArgs e);
    }
}
