using NuGet.Frameworks;

namespace OmniSharp.MSBuild.Models
{
    public class TargetFramework
    {
        public string Name { get; }
        public string FriendlyName { get; }
        public string ShortName { get; }

        public TargetFramework(NuGetFramework framework)
        {
            Name = framework.Framework;
            FriendlyName = framework.Framework;
            ShortName = framework.GetShortFolderName();
        }

        public override string ToString() => ShortName;
    }
}
