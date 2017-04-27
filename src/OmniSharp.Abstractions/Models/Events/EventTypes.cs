namespace OmniSharp.Models.Events
{
    public static class EventTypes
    {
        public static readonly string ProjectAdded = nameof(ProjectAdded);
        public static readonly string ProjectChanged = nameof(ProjectChanged);
        public static readonly string ProjectRemoved = nameof(ProjectRemoved);
        public static readonly string Error = nameof(Error);
        public static readonly string Diagnostic = nameof(Diagnostic);
        public static readonly string PackageRestoreStarted = nameof(PackageRestoreStarted);
        public static readonly string PackageRestoreFinished = nameof(PackageRestoreFinished);
        public static readonly string UnresolvedDependencies = nameof(UnresolvedDependencies);
    }
}