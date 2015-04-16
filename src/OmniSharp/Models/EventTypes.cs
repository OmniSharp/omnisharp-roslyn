namespace OmniSharp.Models
{
    public static class EventTypes
    {
        public static readonly string ProjectAdded = "ProjectAdded";
        public static readonly string ProjectChanged = "ProjectChanged";
        public static readonly string ProjectRemoved = "ProjectRemoved";
        public static readonly string ProjectFailed = "ProjectFailed";
        public static readonly string PackageRestoreStarted = "PackageRestoreStarted";
        public static readonly string PackageRestoreFinished = "PackageRestoreFinished";
        public static readonly string Diagnostics = "Diagnostics";
    }
}