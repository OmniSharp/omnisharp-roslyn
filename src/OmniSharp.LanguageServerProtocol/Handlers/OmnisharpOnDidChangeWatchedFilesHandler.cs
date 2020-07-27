using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using OmniSharp.FileWatching;
using FileChangeType = OmniSharp.Extensions.LanguageServer.Protocol.Models.FileChangeType;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    internal sealed class OmnisharpOnDidChangeWatchedFilesHandler : DidChangeWatchedFilesHandler
    {
        private readonly IFileSystemNotifier _fileSystemNotifier;

        public OmnisharpOnDidChangeWatchedFilesHandler(IFileSystemNotifier fileSystemNotifier) : base(
            new DidChangeWatchedFilesRegistrationOptions()
            {
                Watchers = new Container<FileSystemWatcher>(
                    new FileSystemWatcher()
                    {
                        Kind = WatchKind.Create | WatchKind.Change | WatchKind.Delete,
                        GlobPattern = "**/*.*"
                    }
                )
            })
        {
            _fileSystemNotifier = fileSystemNotifier;
        }

        public override Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken cancellationToken)
        {
            foreach (var change in request.Changes)
            {
                var changeType = change switch
                {
                    { Type: FileChangeType.Changed } => FileWatching.FileChangeType.Change,
                    { Type: FileChangeType.Created } => FileWatching.FileChangeType.Create,
                    { Type: FileChangeType.Deleted } => FileWatching.FileChangeType.Delete,
                    _ => FileWatching.FileChangeType.Unspecified
                };
                _fileSystemNotifier.Notify(change.Uri.GetFileSystemPath(), changeType);
            }

            return Unit.Task;
        }
    }
}