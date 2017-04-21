using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.FileWatching;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.FilesChanged;

namespace OmniSharp.Roslyn.CSharp.Services.Files
{
    [OmniSharpHandler(OmniSharpEndpoints.FilesChanged, LanguageNames.CSharp)]
    public class OnFilesChangedService : IRequestHandler<IEnumerable<Request>, FilesChangedResponse>
    {
        private readonly IFileSystemWatcher _watcher;

        [ImportingConstructor]
        public OnFilesChangedService(IFileSystemWatcher watcher)
        {
            _watcher = watcher;
        }

        public Task<FilesChangedResponse> Handle(IEnumerable<Request> requests)
        {
            foreach (var request in requests)
            {
                _watcher.TriggerChange(request.FileName);
            }
            return Task.FromResult(new FilesChangedResponse());
        }
    }
}
