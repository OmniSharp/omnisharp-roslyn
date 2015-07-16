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
using NuGet.Protocol.Core.Types;
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
            var document = _workspace.GetDocument(request.FileName);
            var projectPath = document?.Project?.FilePath;
            if (!string.IsNullOrWhiteSpace(projectPath))
            {
                projectPath = Path.GetDirectoryName(projectPath);

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
                Items = results
                .SelectMany(z => z)
                .GroupBy(x => x.Identity.Id)
                .Select(x => x.OrderByDescending(z => z.Identity, comparer).First())
                .Select(x => new PackageSearchItem()
                {
                    Id = x.Identity.Id,
                    Version = x.Identity.Version.ToNormalizedString(),
                    HasVersion = x.Identity.HasVersion,
                    Description = x.Description
                })
            };
        }
    }
#endif
}
