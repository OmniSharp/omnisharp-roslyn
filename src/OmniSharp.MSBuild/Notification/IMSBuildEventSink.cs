namespace OmniSharp.MSBuild.Notification
{
    public interface IMSBuildEventSink
    {
        void ProjectLoaded(ProjectLoadedEventArgs e);
    }
}
