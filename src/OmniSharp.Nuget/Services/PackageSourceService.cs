using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Recommendations;
using Microsoft.CodeAnalysis.Text;
#if DNX451
using NuGet.Logging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
#endif
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.NuGet;

namespace OmniSharp
{
#if DNX451
    [OmniSharpHandler(typeof(RequestHandler<PackageSourceRequest, PackageSourceResponse>), "NuGet")]
    public class PackageSourceService : RequestHandler<PackageSourceRequest, PackageSourceResponse>
    {
        [ImportingConstructor]
        public PackageSourceService() { }

        public Task<PackageSourceResponse> Handle(PackageSourceRequest request)
        {
            var projectPath = request.ProjectPath;
            if (request.ProjectPath.EndsWith(".json"))
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
#endif
}
