namespace OmniSharp.Models.Events
{
    public static class EventTypes
    {
        public const string ProjectAdded = nameof(ProjectAdded);
        public const string ProjectChanged = nameof(ProjectChanged);
        public const string ProjectRemoved = nameof(ProjectRemoved);
        public const string Error = nameof(Error);
        public const string Diagnostic = nameof(Diagnostic);
        public const string PackageRestoreStarted = nameof(PackageRestoreStarted);
        public const string PackageRestoreFinished = nameof(PackageRestoreFinished);
        public const string UnresolvedDependencies = nameof(UnresolvedDependencies);
        public const string ProjectConfiguration = nameof(ProjectConfiguration);
        public const string ProjectDiagnosticStatus = nameof(ProjectDiagnosticStatus);

    }
}
