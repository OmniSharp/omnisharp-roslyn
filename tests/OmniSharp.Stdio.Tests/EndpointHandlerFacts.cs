using System;
using System.Collections.Generic;
using System.Composition.Hosting.Core;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.FindSymbols;
using OmniSharp.Models.GotoDefinition;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.Protocol;
using OmniSharp.Services;
using Xunit;

namespace OmniSharp.Stdio.Tests
{
    public class EndpointHandlerFacts
    {
        private abstract class FakeFindSymbolsServiceBase : IRequestHandler<FindSymbolsRequest, QuickFixResponse>
        {
            private readonly string _name;

            public FakeFindSymbolsServiceBase(string name)
            {
                _name = name;
            }

            public Task<QuickFixResponse> Handle(FindSymbolsRequest request = null)
            {
                return Task.FromResult(new QuickFixResponse
                {
                    QuickFixes = new[]
                    {
                            new QuickFix
                            {
                                FileName = $"{_name}.cs",
                                Line = 1,
                                Column = 1,
                                EndLine = 1,
                                EndColumn = 4,
                                Text = _name
                            }
                        }
                });
            }
        }

        private class AAAFakeFindSymbolsService : FakeFindSymbolsServiceBase
        {
            public AAAFakeFindSymbolsService() : base(nameof(AAAFakeFindSymbolsService)) { }
        }

        private class BBBFakeFindSymbolsService : FakeFindSymbolsServiceBase
        {
            public BBBFakeFindSymbolsService() : base(nameof(BBBFakeFindSymbolsService)) { }
        }

        private class CCCFakeFindSymbolsService : FakeFindSymbolsServiceBase
        {
            public CCCFakeFindSymbolsService() : base(nameof(CCCFakeFindSymbolsService)) { }
        }

        private class FakeGotoDefinitionService : IRequestHandler<GotoDefinitionRequest, GotoDefinitionResponse>
        {
            private readonly string _name;
            private readonly bool _returnEmptyResponse;

            public FakeGotoDefinitionService(string name, bool returnEmptyResponse)
            {
                _name = name;
                _returnEmptyResponse = returnEmptyResponse;
            }

            public Task<GotoDefinitionResponse> Handle(GotoDefinitionRequest request)
            {
                if (_returnEmptyResponse)
                {
                    Task.FromResult(new GotoDefinitionResponse());
                }

                return Task.FromResult(new GotoDefinitionResponse
                {
                    FileName = $"{_name}.cs",
                    Line = 1,
                    Column = 2
                });
            }
        }

        private class FakeProjectSystem : IProjectSystem
        {
            public string Key => "FakeProjectSystem";

            public string Language => LanguageNames.CSharp;

            public IEnumerable<string> Extensions => new[] { string.Empty };

            public bool EnabledByDefault => true;

            public bool Initialized => true;

            public Task<object> GetProjectModelAsync(string filePath)
            {
                throw new NotImplementedException();
            }

            public Task<object> GetWorkspaceModelAsync(WorkspaceInformationRequest request)
            {
                throw new NotImplementedException();
            }

            public void Initalize(IConfiguration configuration)
            {
            }
        }

        private class TestRequestPacket : RequestPacket
        {
            public string Arguments { get; set; }
        }

        [Fact]
        public void HandleAggregatableResponsesForSingleLanguage()
        {
            var request = new TestRequestPacket()
            {
                Seq = 99,
                Command = OmniSharpEndpoints.FindSymbols,
                Arguments = JsonConvert.SerializeObject(new FindSymbolsRequest { Language = LanguageNames.CSharp })
            };

            var writer = new TestTextWriter(
                value =>
                {
                    var packet = JsonConvert.DeserializeObject<EventPacket>(value);
                    Assert.Equal("started", packet.Event);
                },
                value =>
                {
                    var packet = JsonConvert.DeserializeObject<ResponsePacket>(value);
                    Assert.Equal(request.Seq, packet.Request_seq);
                    Assert.Equal(request.Command, packet.Command);
                    Assert.True(packet.Success);
                    Assert.True(packet.Running);
                    Assert.Null(packet.Message);
                    var quickFixResponse = ((JObject)packet.Body).ToObject<QuickFixResponse>();
                    Assert.Equal(3, quickFixResponse.QuickFixes.Count());
                    Assert.Equal("AAAFakeFindSymbolsService", quickFixResponse.QuickFixes.ElementAt(0).Text);
                    Assert.Equal("BBBFakeFindSymbolsService", quickFixResponse.QuickFixes.ElementAt(1).Text);
                    Assert.Equal("CCCFakeFindSymbolsService", quickFixResponse.QuickFixes.ElementAt(2).Text);
                }
            );

            var exports = new ExportDescriptorProvider[]
            {
                MefValueProvider.From<IRequestHandler>(
                    new BBBFakeFindSymbolsService(),
                    new Dictionary<string, object> { ["EndpointName"] = OmniSharpEndpoints.FindSymbols, ["Language"] = LanguageNames.CSharp }),
                MefValueProvider.From<IRequestHandler>(
                    new CCCFakeFindSymbolsService(),
                    new Dictionary<string, object> { ["EndpointName"] = OmniSharpEndpoints.FindSymbols, ["Language"] = LanguageNames.CSharp }),
                MefValueProvider.From<IRequestHandler>(
                    new AAAFakeFindSymbolsService(),
                    new Dictionary<string, object> { ["EndpointName"] = OmniSharpEndpoints.FindSymbols, ["Language"] = LanguageNames.CSharp }),
            };

            using (StdioServerFacts.BuildTestServerAndStart(new StringReader(JsonConvert.SerializeObject(request) + "\r\n"), writer, additionalExports: exports))
            {
                Assert.True(writer.Completion.WaitOne(TimeSpan.FromSeconds(60)), "Timeout");
                Assert.Null(writer.Exception);
            }
        }

        [Fact]
        public void HandleAggregatableResponsesForMultipleLanguages()
        {
            var request = new TestRequestPacket()
            {
                Seq = 99,
                Command = OmniSharpEndpoints.FindSymbols,
                Arguments = JsonConvert.SerializeObject(new FindSymbolsRequest())
            };

            var writer = new TestTextWriter(
                value =>
                {
                    var packet = JsonConvert.DeserializeObject<EventPacket>(value);
                    Assert.Equal("started", packet.Event);
                },
                value =>
                {
                    var packet = JsonConvert.DeserializeObject<ResponsePacket>(value);
                    Assert.Equal(request.Seq, packet.Request_seq);
                    Assert.Equal(request.Command, packet.Command);
                    Assert.True(packet.Success);
                    Assert.True(packet.Running);
                    Assert.Null(packet.Message);
                    var quickFixResponse = ((JObject)packet.Body).ToObject<QuickFixResponse>();
                    Assert.Equal(3, quickFixResponse.QuickFixes.Count());
                    Assert.Equal("AAAFakeFindSymbolsService", quickFixResponse.QuickFixes.ElementAt(0).Text);
                    Assert.Equal("BBBFakeFindSymbolsService", quickFixResponse.QuickFixes.ElementAt(1).Text);
                    Assert.Equal("CCCFakeFindSymbolsService", quickFixResponse.QuickFixes.ElementAt(2).Text);
                }
            );

            var exports = new ExportDescriptorProvider[]
            {
                MefValueProvider.From<IRequestHandler>(
                    new BBBFakeFindSymbolsService(),
                    new Dictionary<string, object> { ["EndpointName"] = OmniSharpEndpoints.FindSymbols, ["Language"] = LanguageNames.CSharp }),
                MefValueProvider.From<IRequestHandler>(
                    new CCCFakeFindSymbolsService(),
                    new Dictionary<string, object> { ["EndpointName"] = OmniSharpEndpoints.FindSymbols, ["Language"] = LanguageNames.VisualBasic }),
                MefValueProvider.From<IRequestHandler>(
                    new AAAFakeFindSymbolsService(),
                    new Dictionary<string, object> { ["EndpointName"] = OmniSharpEndpoints.FindSymbols, ["Language"] = LanguageNames.CSharp }),
            };

            using (StdioServerFacts.BuildTestServerAndStart(new StringReader(JsonConvert.SerializeObject(request) + "\r\n"), writer, additionalExports: exports))
            {
                Assert.True(writer.Completion.WaitOne(TimeSpan.FromSeconds(60)), "Timeout");
                Assert.Null(writer.Exception);
            }
        }

        [Fact]
        public void HandleNonAggregatableResponses()
        {
            var request = new TestRequestPacket()
            {
                Seq = 99,
                Command = OmniSharpEndpoints.GotoDefinition,
                Arguments = JsonConvert.SerializeObject(new GotoDefinitionRequest { FileName = "foo.cs" })
            };

            var writer = new TestTextWriter(
                value =>
                {
                    var packet = JsonConvert.DeserializeObject<EventPacket>(value);
                    Assert.Equal("started", packet.Event);
                },
                value =>
                {
                    var packet = JsonConvert.DeserializeObject<ResponsePacket>(value);
                    Assert.Equal(request.Seq, packet.Request_seq);
                    Assert.Equal(request.Command, packet.Command);
                    Assert.True(packet.Success);
                    Assert.True(packet.Running);
                    Assert.Null(packet.Message);
                    var gotoDefinitionResponse = ((JObject)packet.Body).ToObject<GotoDefinitionResponse>();
                    Assert.Equal("ZZZFake.cs", gotoDefinitionResponse.FileName);
                }
            );

            var exports = new ExportDescriptorProvider[]
            {
                MefValueProvider.From<IProjectSystem>(new FakeProjectSystem()),
                MefValueProvider.From<IRequestHandler>(
                    new FakeGotoDefinitionService("ZZZFake", false),
                    new Dictionary<string, object> { ["EndpointName"] = OmniSharpEndpoints.GotoDefinition, ["Language"] = LanguageNames.CSharp }),
                MefValueProvider.From<IRequestHandler>(
                    new FakeGotoDefinitionService("AAAFake", true),
                    new Dictionary<string, object> { ["EndpointName"] = OmniSharpEndpoints.GotoDefinition, ["Language"] = LanguageNames.CSharp }),
            };

            using (StdioServerFacts.BuildTestServerAndStart(new StringReader(JsonConvert.SerializeObject(request) + "\r\n"), writer, additionalExports: exports))
            {
                Assert.True(writer.Completion.WaitOne(TimeSpan.FromSeconds(60)), "Timeout");
                Assert.Null(writer.Exception);
            }
        }
    }
}