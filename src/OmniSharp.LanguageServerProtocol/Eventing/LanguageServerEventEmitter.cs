using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Eventing;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;
using OmniSharp.LanguageServerProtocol.Handlers;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Models.Events;

namespace OmniSharp.LanguageServerProtocol.Eventing
{
    public class LanguageServerEventEmitter : IEventEmitter
    {
        private readonly ILanguageServer _server;
        private readonly DocumentVersions _documentVersions;
        private readonly ConcurrentDictionary<string, IWorkDoneObserver> _projectObservers = new();
        private IWorkDoneObserver _restoreObserver;

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
                case EventTypes.ProjectLoadingStarted:
                    string projectPath = (string)args;
                    IWorkDoneObserver projectObserver = _server.WorkDoneManager
                        .Create(new WorkDoneProgressBegin { Title = $"Loading {projectPath}" })
                        .GetAwaiter()
                        .GetResult();
                    _projectObservers.TryAdd(projectPath, projectObserver);
                    break;
                case EventTypes.ProjectAdded:
                case EventTypes.ProjectChanged:
                case EventTypes.ProjectRemoved:
                    _server.SendNotification($"o#/{kind}".ToLowerInvariant(), JToken.FromObject(args)); // ProjectInformationResponse
                    break;

                // work done??
                case EventTypes.PackageRestoreStarted:
                    _server.SendNotification($"o#/{kind}".ToLowerInvariant(), JToken.FromObject(args));
                    _restoreObserver = _server.WorkDoneManager
                        .Create(new WorkDoneProgressBegin { Title = "Restoring" })
                        .GetAwaiter()
                        .GetResult();
                    break;
                case EventTypes.PackageRestoreFinished:
                    _server.SendNotification($"o#/{kind}".ToLowerInvariant(), JToken.FromObject(args));
                    _restoreObserver.OnNext(new WorkDoneProgressReport { Message = "Restored" });
                    _restoreObserver.OnCompleted();
                    break;
                case EventTypes.UnresolvedDependencies:
                    _server.SendNotification($"o#/{kind}".ToLowerInvariant(), JToken.FromObject(args));
                    break;

                case EventTypes.Error:
                case EventTypes.ProjectConfiguration:
                    _server.SendNotification($"o#/{kind}".ToLowerInvariant(), JToken.FromObject(args));
                    ProjectConfigurationMessage projectMessage = (ProjectConfigurationMessage)args;
                    if (_projectObservers.TryGetValue(projectMessage.ProjectFilePath, out IWorkDoneObserver obs))
                    {
                        obs.OnNext(new WorkDoneProgressReport { Message = $"Loaded {projectMessage.ProjectFilePath}" });
                        obs.OnCompleted();
                    }
                    break;
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
