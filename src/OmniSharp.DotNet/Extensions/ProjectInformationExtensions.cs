using System.Linq;
using Microsoft.Extensions.ProjectModel.ProjectSystem;

namespace OmniSharp.DotNet.Extensions
{
    public static class ProjectInformationExtensions
    {
        public static string ChooseCompilationConfig(this ProjectInformation info, string preferred)
        {
            if (info.Configurations.Contains(preferred))
            {
                return preferred;
            }
            else
            {
                return info.Configurations.FirstOrDefault() ?? string.Empty;
            }
        }
    }
}
