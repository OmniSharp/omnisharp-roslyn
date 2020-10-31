using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Eventing;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.LanguageServerProtocol.Handlers;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Models.Events;

namespace OmniSharp.LanguageServerProtocol.Eventing
{
    public class LanguageServerEventEmitter : IEventEmitter
    {
        private readonly ILanguageServer _server;
        private readonly DocumentVersions _documentVersions;

        public LanguageServerEventEmitter(ILanguageServer server)
        {
            _server = server;
            _documentVersions = server.Services.GetRequiredService<DocumentVersions>();
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
                            _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams()
                            {
                                Uri = group.Key,
                                Version = _documentVersions.GetVersion(group.Key),
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
                    _server.SendNotification($"o#/{kind}".ToLowerInvariant(), JToken.FromObject(args)); // ProjectInformationResponse
                    break;

                // work done??
                case EventTypes.PackageRestoreStarted:
                case EventTypes.PackageRestoreFinished:
                case EventTypes.UnresolvedDependencies:
                    _server.SendNotification($"o#/{kind}".ToLowerInvariant(), JToken.FromObject(args));
                    break;

                case EventTypes.Error:
                case EventTypes.ProjectConfiguration:
                case EventTypes.ProjectDiagnosticStatus:
                    _server.SendNotification($"o#/{kind}".ToLowerInvariant(), JToken.FromObject(args));
                    break;

                default:
                    _server.SendNotification($"o#/{kind}".ToLowerInvariant(), JToken.FromObject(args));
                    break;
            }
        }
    }
}
