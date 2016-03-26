using NuGet.Frameworks;

namespace OmniSharp.DotNet.Models
{
    public class DotNetFramework
    {
        public DotNetFramework(NuGetFramework framework)
        {
            Name = framework.Framework;
            FriendlyName = framework.Framework;
            ShortName = framework.GetShortFolderName();
        }

        public string Name { get; }
        public string FriendlyName { get; }
        public string ShortName { get; }
    }
}
