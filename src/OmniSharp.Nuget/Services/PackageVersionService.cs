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
    [OmniSharpHandler(typeof(RequestHandler<PackageVersionRequest, PackageVersionResponse>), LanguageNames.CSharp)]
    public class PackageVersionService : RequestHandler<PackageVersionRequest, PackageVersionResponse>
    {
        [ImportingConstructor]
        public PackageVersionService() { }

        public async Task<PackageVersionResponse> Handle(PackageVersionRequest request)
        {
            var projectPath = request.ProjectPath;
            if (request.ProjectPath.EndsWith(".json"))
            {
                projectPath = Path.GetDirectoryName(projectPath);
            }

            if (!string.IsNullOrWhiteSpace(projectPath))
            {
                if (request.Sources == null)
                    request.Sources = Enumerable.Empty<string>();

                var token = CancellationToken.None;

                var filter = new SearchFilter
                {
                    IncludePrerelease = request.IncludePrerelease
                };
                var foundVersions = new List<NuGetVersion>();
                var repositoryProvider = new OmniSharpSourceRepositoryProvider(projectPath);
                var repos = repositoryProvider.GetRepositories().ToArray();
                if (request.Sources.Any())
                {
                    // Reduce to just the sources we requested
                    repos = repos.Join(request.Sources, x => x.PackageSource.Source, x => x, (x, y) => x).ToArray();
                }
                foreach (var repo in repos)
                {
                    // TODO: Swap when bug is fixed
                    // https://github.com/NuGet/NuGet3/pull/90
                    /*
                    var resource = await repo.GetResourceAsync<FindPackageByIdResource>();
                    if (resource != null)
                    {
                        resource.Logger = NullLogger.Instance;
                        resource.NoCache = true;
                        foundVersions.AddRange(await resource.GetAllVersionsAsync(request.Id, token));
                    }*/
                    var resource = await repo.GetResourceAsync<SimpleSearchResource>();
                    if (resource != null)
                    {
                        var result = await resource.Search(request.Id, filter, 0, 50, token);
                        var package = result.FirstOrDefault(metadata => metadata.Identity.Id == request.Id);
                        if (package != null)
                            foundVersions.AddRange(package.AllVersions);
                    }
                }

                var comparer = new VersionComparer();
                var versions = Enumerable.Distinct<NuGetVersion>(foundVersions, comparer)
                    .OrderByDescending(version => version, comparer)
                    .Select(version => version.ToNormalizedString());

                return new PackageVersionResponse()
                {
                    Versions = versions
                };
            }

            return new PackageVersionResponse();
        }
    }
#endif
}
