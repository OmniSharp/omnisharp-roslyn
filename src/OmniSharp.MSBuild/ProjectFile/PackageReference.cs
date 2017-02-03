using NuGet.Versioning;

namespace OmniSharp.MSBuild.ProjectFile
{
    public class PackageReference
    {
        public string Name { get; }
        public NuGetVersion Version { get; }
        public bool IsImplicitlyDefined { get; }

        public PackageReference(string name, NuGetVersion version, bool isImplicitlyDefined)
        {
            this.Name = name;
            this.Version = version;
            this.IsImplicitlyDefined = isImplicitlyDefined;
        }

        public override string ToString()
        {
            var suffix = IsImplicitlyDefined ? " (implicit)" : string.Empty;
            return $"{Name}, {Version}{suffix}";
        }
    }
}
