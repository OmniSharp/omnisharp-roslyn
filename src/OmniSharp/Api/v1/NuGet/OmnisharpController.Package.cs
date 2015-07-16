using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Recommendations;
using Microsoft.CodeAnalysis.Text;
using NuGet.Protocol.Core.Types;
using OmniSharp.Dnx;
using OmniSharp.Documentation;
using OmniSharp.Extensions;
using OmniSharp.Intellisense;
using OmniSharp.Models;
using OmniSharp.NuGet;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("packagesearch")]
        public async Task< PackageSearchResponse> PackageSearch(PackageSearchRequest request)
        {
            var document = _workspace.GetDocument(request.FileName);
            var projectPath = document?.Project?.FilePath;
            if (!string.IsNullOrWhiteSpace(projectPath))
            {
                projectPath = Path.GetDirectoryName(projectPath);

#if DNX451
                var token = CancellationToken.None;
                var tasks = new List<Task<IEnumerable<SimpleSearchMetadata>>>();
                var repositoryProvider = new OmniSharpSourceRepositoryProvider(projectPath);
                foreach (var repo in repositoryProvider.GetRepositories()) {
                    var resource = await repo.GetResourceAsync<SimpleSearchResource>();
                    if (resource != null)
                    {
                        tasks.Add(resource.Search("jquery", new SearchFilter(), 0, 100, token));
                    }
                }

                var results = await Task.WhenAll(tasks);
                return MergeResults(results);
#endif
            }

            return new PackageSearchResponse();
        }

#if DNX451
        private PackageSearchResponse MergeResults(IEnumerable<SimpleSearchMetadata>[] results)
        {
            return new PackageSearchResponse();
        }
#endif
    }
}
