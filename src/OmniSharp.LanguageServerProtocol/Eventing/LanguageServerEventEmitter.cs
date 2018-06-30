using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OmniSharp.Eventing;
using OmniSharp.Extensions.LanguageServer;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Models.Events;
using OmniSharp.Stdio.Protocol;
using OmniSharp.Stdio.Services;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace OmniSharp.LanguageServerProtocol.Eventing
{
    public class LanguageServerEventEmitter : IEventEmitter
    {
        private ILanguageServer _server;

        public void SetLanguageServer(ILanguageServer server) {
            _server = server;
        }

        public void Emit(string kind, object args)
        {
            if (_server == null) return;

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
