using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using OmniSharp.FileWatching;
using FileChangeType = OmniSharp.Extensions.LanguageServer.Protocol.Models.FileChangeType;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    internal sealed class OmnisharpOnDidChangeWatchedDirectoriesHandler : DidChangeWatchedFilesHandler
    {
        private readonly IFileSystemNotifier _fileSystemNotifier;

        public OmnisharpOnDidChangeWatchedDirectoriesHandler(IFileSystemNotifier fileSystemNotifier) : base(
            new DidChangeWatchedFilesRegistrationOptions()
            {
                Watchers = new Container<FileSystemWatcher>(
                    new FileSystemWatcher()
                    {
                        Kind = WatchKind.Delete,
                        GlobPattern = "**/"
                    }
                )
            })
        {
            _fileSystemNotifier = fileSystemNotifier;
        }

        public override Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken cancellationToken)
        {
            foreach (var change in request.Changes.Where(z => z.Type == FileChangeType.Deleted))
            {
                _fileSystemNotifier.Notify(change.Uri.GetFileSystemPath(), FileWatching.FileChangeType.DirectoryDelete);
            }

            return Unit.Task;
        }
    }
}