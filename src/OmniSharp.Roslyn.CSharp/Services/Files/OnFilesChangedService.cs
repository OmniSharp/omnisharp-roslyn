using System;
using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.FileWatching;
using OmniSharp.Mef;
using OmniSharp.Models.FilesChanged;

namespace OmniSharp.Roslyn.CSharp.Services.Files
{
    [OmniSharpHandler(OmniSharpEndpoints.FilesChanged, LanguageNames.CSharp)]
    public class OnFilesChangedService : IRequestHandler<IEnumerable<FilesChangedRequest>, FilesChangedResponse>
    {
        private readonly IFileSystemNotifier _notifier;

        [ImportingConstructor]
        public OnFilesChangedService(IFileSystemNotifier notifier)
        {
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        }

        public Task<FilesChangedResponse> Handle(IEnumerable<FilesChangedRequest> requests)
        {
            foreach (var request in requests)
            {
                _notifier.Notify(request.FileName, request.ChangeType);
            }

            return Task.FromResult(new FilesChangedResponse());
        }
    }
}
