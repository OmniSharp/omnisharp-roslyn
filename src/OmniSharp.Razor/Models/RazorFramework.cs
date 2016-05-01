namespace OmniSharp.Razor.Models
{
    public class RazorFramework
    {
        public RazorFramework(/*NuGetFramework framework*/)
        {
            /*Name = framework.Framework;
            FriendlyName = framework.Framework;
            ShortName = framework.GetShortFolderName();*/
        }

        public string Name { get; }
        public string FriendlyName { get; }
        public string ShortName { get; }
    }
}
