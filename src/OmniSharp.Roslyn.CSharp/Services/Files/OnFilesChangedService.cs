using System.Collections.Generic;
using System.Composition;
using System.IO;
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
        private OmniSharpWorkspace _workspace;
        private object _lock = new object();

        [ImportingConstructor]
        public OnFilesChangedService(IFileSystemWatcher watcher, OmniSharpWorkspace workspace)
        {
            _watcher = watcher;
            _workspace = workspace;
        }

        public Task<FilesChangedResponse> Handle(IEnumerable<Request> requests)
        {
            foreach (var request in requests)
            {
                RemoveRenamedFiles(request.FileName);
                _watcher.TriggerChange(request.FileName);
            }
            return Task.FromResult(new FilesChangedResponse());
        }

        private void RemoveRenamedFiles(string fileName)
        {
            lock (_lock)
            {
                var path = Path.GetDirectoryName(fileName);
                foreach (var project in _workspace.CurrentSolution.Projects)
                {
                    if (!path.StartsWith(Path.GetDirectoryName(project.FilePath)))
                    {
                        continue;
                    }

                    foreach (var document in project.Documents)
                    {
                        if (!document.FilePath.Contains(path))
                        {
                            continue;
                        }

                        if(!File.Exists(document.FilePath) && _workspace.CurrentSolution.ContainsDocument(document.Id))
                        {
                            _workspace.TryApplyChanges(_workspace.CurrentSolution.RemoveDocument(document.Id));
                        }
                    }
                }
            }
        }
    }
}
