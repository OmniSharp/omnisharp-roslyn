using System.Linq;
using OmniSharp.Eventing;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Models.Events;
using ILanguageServer = OmniSharp.Extensions.LanguageServer.Server.ILanguageServer;

namespace OmniSharp.LanguageServerProtocol.Eventing
{
    public class LanguageServerEventEmitter : IEventEmitter
    {
        private ILanguageServer _server;

        public void SetLanguageServer(ILanguageServer server)
        {
            _server = server;
        }

        public void Emit(string kind, object args)
        {
            switch (kind)
            {
                case EventTypes.Diagnostic:
                    if (args is DiagnosticMessage message)
                    {
                        var groups = message.Results
                            .GroupBy(z => Helpers.ToUri(z.FileName), z => z.QuickFixes);

                        foreach (var group in groups)
                        {
                            _server.Document.PublishDiagnostics(new PublishDiagnosticsParams()
                            {
                                Uri = group.Key,
                                Diagnostics = group
                                    .SelectMany(z => z.Select(v => v.ToDiagnostic()))
                                    .ToArray()
                            });
                        }
                    }
                    break;
                case EventTypes.ProjectAdded:
                case EventTypes.ProjectChanged:
                case EventTypes.ProjectRemoved:
                case EventTypes.Error:
                case EventTypes.PackageRestoreStarted:
                case EventTypes.PackageRestoreFinished:
                case EventTypes.UnresolvedDependencies:
                    // TODO: As custom notifications
                    break;
            }
        }
    }
}
