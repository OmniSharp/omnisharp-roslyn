using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using OmniSharp.DotNetTest.Models;
using OmniSharp.DotNetTest.Models.Events;
using OmniSharp.Eventing;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using OmniSharp.Models;
using OmniSharp.Models.V2.CodeActions;
using LSP = OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Models.V2.CodeStructure;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    class OmniSharpExecuteCommandHandler : ExecuteCommandHandler
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(
            RequestHandlers handlers,
            IEventEmitter eventEmitter)
        {
            foreach (var (selector, runTestsInClassHandler, codeStructureHandler) in handlers
                     .OfType<Mef.IRequestHandler<RunTestsInClassRequest, RunTestResponse>,
                             Mef.IRequestHandler<CodeStructureRequest, CodeStructureResponse>>())
            {
                if (runTestsInClassHandler != null && codeStructureHandler != null)
                {
                    yield return new OmniSharpExecuteCommandHandler(
                        runTestsInClassHandler,
                        codeStructureHandler,
                        eventEmitter);
                }
            }
        }

        private readonly Mef.IRequestHandler<RunTestsInClassRequest, RunTestResponse> _runTestsInClassHandler;
        private readonly Mef.IRequestHandler<CodeStructureRequest, CodeStructureResponse> _codeStructureHandler;
        private readonly IEventEmitter _eventEmitter;

        public OmniSharpExecuteCommandHandler(
            Mef.IRequestHandler<RunTestsInClassRequest, RunTestResponse> runTestsInClassHandler,
            Mef.IRequestHandler<CodeStructureRequest, CodeStructureResponse> codeStructureHandler,
            IEventEmitter eventEmitter)
            : base (new ExecuteCommandRegistrationOptions() {
                Commands = new Container<string>("omnisharp/runTestMethod"),
            })
        {
            _runTestsInClassHandler = runTestsInClassHandler;
            _codeStructureHandler = codeStructureHandler;
            _eventEmitter = eventEmitter;
        }

        public override async Task<Unit>
        Handle(ExecuteCommandParams request, CancellationToken cancellationToken)
        {
            if (request.Command == "omnisharp/runTestMethod")
            {
                if ((request.Arguments.First() is JObject testRunArgs)
                    && testRunArgs.TryGetValue("textDocumentUri", out JToken textDocumentUri))
                {
                    var codeStructure = await _codeStructureHandler.Handle(
                        new CodeStructureRequest {
                            FileName = Helpers.FromUri(textDocumentUri.ToObject<Uri>()),
                        });

                    IEnumerable<(string testFramework, string testMethodName, Range body)>
                        collectTestsMethodOnCodeElement(CodeElement element)
                    {
                        if (element.Properties != null
                            && element.Properties.TryGetValue("testFramework", out object testFramework)
                            && element.Properties.TryGetValue("testMethodName", out object testMethodName)
                            && !string.IsNullOrWhiteSpace(testFramework as string)
                            && !string.IsNullOrWhiteSpace(testMethodName as string))
                        {
                            yield return ((string)testFramework,
                                        (string)testMethodName,
                                        Helpers.ToRange(element.Ranges[OmniSharp.Models.V2.SymbolRangeNames.Full]));
                        }
                        else if (element.Children?.Any() ?? false)
                        {
                            var testMethodsOnChildren = element.Children.SelectMany(collectTestsMethodOnCodeElement);

                            foreach (var t in testMethodsOnChildren)
                            {
                                yield return t;
                            }
                        }
                    }

                    // walk document code elements recursively to find out test methods on this document
                    var unitTestsToRun =
                        (codeStructure.Elements ?? Array.Empty<CodeElement>())
                        .SelectMany(collectTestsMethodOnCodeElement);

                    if (testRunArgs.TryGetValue("position", out JToken positionToken))
                    {
                        var position = positionToken.ToObject<Position>(
                            new JsonSerializer { ContractResolver = new CamelCasePropertyNamesContractResolver() });

                        bool rangeMatchesPosition(Range r) =>
                            ((position.Line == r.Start.Line && position.Character >= r.Start.Character)
                            || position.Line > r.Start.Line)
                            && ((position.Line == r.End.Line && position.Character <= r.End.Character)
                                || position.Line < r.End.Line);

                        unitTestsToRun = unitTestsToRun.Where(x => rangeMatchesPosition(x.body)).ToArray();
                    }

                    if (unitTestsToRun.Any())
                    {
                        var uri = textDocumentUri.ToObject<Uri>();
                        var testFrameworkName = unitTestsToRun.Select(x => x.testFramework).Distinct().Single();
                        string[] testMethodNames = unitTestsToRun.Select(x => x.testMethodName).ToArray();

                        var omnisharpRequest = new RunTestsInClassRequest
                        {
                            FileName = Helpers.FromUri(uri),
                            TestFrameworkName = testFrameworkName,
                            MethodNames = testMethodNames,
                        };

                        // _runTestsInClassHandler.Handle() will block for some time before it yields
                        // but we want to return from the command handler as fast as possible, thus the wrapping in Task.Run()
                        var _ignored = Task.Run(() => _runTestsInClassHandler.Handle(omnisharpRequest));
                    }
                    else
                    {
                        _eventEmitter.Emit(
                            TestMessageEvent.Id,
                            new TestMessageEvent
                            {
                                MessageLevel = "error",
                                Message = "No tests found"
                            });
                    }
                }
            }

            return Unit.Value;
        }
    }
}
