using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Recommendations;
using Microsoft.CodeAnalysis.Text;
#if DNX451
using NuGet.Logging;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
#endif
using OmniSharp.Dnx;
using OmniSharp.Documentation;
using OmniSharp.Extensions;
using OmniSharp.Intellisense;
using OmniSharp.Models;
using OmniSharp.NuGet;

namespace OmniSharp
{
#if DNX451
    public partial class OmnisharpController
    {
        [HttpPost("packagesearch")]
        public async Task<PackageSearchResponse> PackageSearch(PackageSearchRequest request)
        {
            var projectPath = request.ProjectPath;
            if (request.ProjectPath.EndsWith(".json"))
            {
                projectPath = Path.GetDirectoryName(projectPath);
            }

            if (!string.IsNullOrWhiteSpace(projectPath))
            {
                if (request.SupportedFrameworks == null)
                    request.SupportedFrameworks = Enumerable.Empty<string>();

                if (request.PackageTypes == null)
                    request.PackageTypes = Enumerable.Empty<string>();

                var token = CancellationToken.None;
                var filter = new SearchFilter()
                {
                    SupportedFrameworks = request.SupportedFrameworks,
                    IncludePrerelease = request.IncludePrerelease,
                    PackageTypes = request.PackageTypes
                };
                var tasks = new List<Task<IEnumerable<SimpleSearchMetadata>>>();
                var repositoryProvider = new OmniSharpSourceRepositoryProvider(projectPath);
                var repos = repositoryProvider.GetRepositories().ToArray();
                foreach (var repo in repos)
                {
                    var resource = await repo.GetResourceAsync<SimpleSearchResource>();
                    if (resource != null)
                    {
                        tasks.Add(resource.Search(request.Search, filter, 0, 50, token));
                    }
                }

                var results = await Task.WhenAll(tasks);
                return MergeResults(results, repos);
            }

            return new PackageSearchResponse();
        }

        private PackageSearchResponse MergeResults(IEnumerable<SimpleSearchMetadata>[] results, IEnumerable<SourceRepository> repos)
        {
            var comparer = new global::NuGet.Packaging.Core.PackageIdentityComparer();
            return new PackageSearchResponse()
            {
                Sources = repos.Select(x => x.PackageSource.Source),
                Packages = results
                    .SelectMany(z => z)
                    .GroupBy(x => x.Identity.Id)
                    .Select(x => x.OrderByDescending(z => z.Identity, comparer).First())
                    .OrderBy(x => x.Identity.Id)
                    .Select(x => new PackageSearchItem()
                    {
                        Id = x.Identity.Id,
                        Version = x.Identity.Version.ToNormalizedString(),
                        HasVersion = x.Identity.HasVersion,
                        Description = x.Description
                    })
            };
        }

        [HttpPost("packageversion")]
        public async Task<PackageVersionResponse> PackageVersion(PackageVersionRequest request)
        {

            var projectPath = request.ProjectPath;
            if (request.ProjectPath.EndsWith(".json"))
            {
                projectPath = Path.GetDirectoryName(projectPath);
            }

            if (!string.IsNullOrWhiteSpace(projectPath))
            {
                var token = CancellationToken.None;
                // Temp?
                var filter = new SearchFilter()
                {
                    IncludePrerelease = true
                };
                var foundVersions = new List<NuGetVersion>();
                var repositoryProvider = new OmniSharpSourceRepositoryProvider(projectPath);
                var repos = repositoryProvider.GetRepositories().ToArray();
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
                        var package = result.FirstOrDefault(z => z.Identity.Id == request.Id);
                        if (package != null)
                            foundVersions.AddRange(package.AllVersions);
                    }
                }

                var comparer = new VersionComparer();
                var versions = Enumerable.Distinct<NuGetVersion>(foundVersions, comparer)
                    .OrderByDescending(z => z, comparer)
                    .Select(z => z.ToNormalizedString());

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
