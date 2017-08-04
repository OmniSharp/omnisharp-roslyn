using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using OmniSharp.Mef;
using OmniSharp.Models.PackageSource;
using OmniSharp.NuGet;

namespace OmniSharp
{
    [OmniSharpHandler(OmniSharpEndpoints.PackageSource, "NuGet")]
    public class PackageSourceService : IRequestHandler<PackageSourceRequest, PackageSourceResponse>
    {
        [ImportingConstructor]
        public PackageSourceService() { }

        public Task<PackageSourceResponse> Handle(PackageSourceRequest request)
        {
            var projectPath = request.ProjectPath;
            if (!string.IsNullOrWhiteSpace(projectPath) && projectPath.EndsWith(".json"))
            {
                projectPath = Path.GetDirectoryName(projectPath);
            }

            if (!string.IsNullOrWhiteSpace(projectPath))
            {
                var tasks = new List<Task<IEnumerable<SimpleSearchMetadata>>>();
                var repositoryProvider = new OmniSharpSourceRepositoryProvider(projectPath);
                var repos = repositoryProvider.GetRepositories().ToArray();
                return Task.FromResult(new PackageSourceResponse()
                {
                    Sources = repos.Select(x => x.PackageSource.Source)
                });
            }

            return Task.FromResult(new PackageSourceResponse());
        }
    }
}
