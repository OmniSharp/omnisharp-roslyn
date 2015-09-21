using System;
using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Files
{
    [OmniSharpHandler(typeof(RequestHandler<IEnumerable<Request> ,object>), LanguageNames.CSharp)]
    public class OnFilesChangedService : RequestHandler<IEnumerable<Request> ,object>
    {
        private readonly IFileSystemWatcher _watcher;

        [ImportingConstructor]
        public OnFilesChangedService(IFileSystemWatcher watcher)
        {
            _watcher = watcher;
        }

        public Task<object> Handle(IEnumerable<Request> requests)
        {
            foreach (var request in requests)
            {
                _watcher.TriggerChange(request.FileName);
            }
            return Task.FromResult<object>(true);
        }
    }
}
