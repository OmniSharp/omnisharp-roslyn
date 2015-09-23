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
    [OmniSharpHandler(typeof(RequestHandler<PackageSearchRequest, PackageSearchResponse>), "NuGet")]
    public class PackageSearchService : RequestHandler<PackageSearchRequest, PackageSearchResponse>
    {
        [ImportingConstructor]
        public PackageSearchService() { }

        public async Task<PackageSearchResponse> Handle(PackageSearchRequest request)
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

                if (request.Sources == null)
                    request.Sources = Enumerable.Empty<string>();

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
                if (request.Sources.Any())
                {
                    // Reduce to just the sources we requested
                    repos = repos.Join(request.Sources, x => x.PackageSource.Source, x => x, (x, y) => x).ToArray();
                }

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
            var comparer = new PackageIdentityComparer();
            return new PackageSearchResponse()
            {
                Packages = results
                    .SelectMany(metadata => metadata)
                    .GroupBy(metadata => metadata.Identity.Id)
                    .Select(metadataGroup => metadataGroup.OrderByDescending(metadata => metadata.Identity, comparer).First())
                    .OrderBy(metadata => metadata.Identity.Id)
                    .Select(metadata => new PackageSearchItem()
                    {
                        Id = metadata.Identity.Id,
                        Version = metadata.Identity.Version.ToNormalizedString(),
                        HasVersion = metadata.Identity.HasVersion,
                        Description = metadata.Description
                    })
            };
        }
    }
#endif
}
