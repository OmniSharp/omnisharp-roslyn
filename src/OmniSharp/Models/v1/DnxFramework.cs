using OmniSharp.Dnx;

namespace OmniSharp.Models
{
    public class DnxFramework
    {
        public string Name { get; set; }
        public string FriendlyName { get; set; }
        public string ShortName { get; set; }

        public DnxFramework(FrameworkProject project)
        {
            Name = project.Framework;
            FriendlyName = project.FriendlyName;
            ShortName = project.ShortName;
        }
    }
}
