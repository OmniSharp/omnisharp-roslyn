using System.ComponentModel.Design;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using OmniSharp.FileWatching;
using FileChangeType = OmniSharp.Extensions.LanguageServer.Protocol.Models.FileChangeType;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    internal sealed class OmnisharpOnDidChangeWatchedFilesHandler : DidChangeWatchedFilesHandlerBase
    {
        private readonly IFileSystemNotifier _fileSystemNotifier;

        public OmnisharpOnDidChangeWatchedFilesHandler(IFileSystemNotifier fileSystemNotifier)
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

                // We can only register one IDidChangeWatchedFilesHandler as there is no way to really identify the caller
                // when the request comes through
                // However we still need to deal with folder deletions
                // There is no easy way to determine if the uri is a folder or a file (what if the file has no
                // extension, we can't make this assumption with files that not yet exist on disc, vscode has Untitled-x)
                if (change.Type == FileChangeType.Deleted)
                {
                    _fileSystemNotifier.Notify(change.Uri.GetFileSystemPath(), FileWatching.FileChangeType.DirectoryDelete);
                }
            }

            return Unit.Task;
        }

        protected override DidChangeWatchedFilesRegistrationOptions CreateRegistrationOptions(DidChangeWatchedFilesCapability capability, ClientCapabilities clientCapabilities)
        {
            return new DidChangeWatchedFilesRegistrationOptions()
                {
                    Watchers = new Container<FileSystemWatcher>(
                        new FileSystemWatcher()
                        {
                            Kind = WatchKind.Create | WatchKind.Change | WatchKind.Delete,
                            GlobPattern = "**/*.*"
                        }
                    )
                };
        }
    }
}
