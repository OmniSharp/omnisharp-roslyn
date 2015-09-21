using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Models.v1;
using V2 = OmniSharp.Models.V2;

namespace OmniSharp
{
    public interface RequestHandler<TRequest, TResponse>
    {
        Task<TResponse> Handle(TRequest request);
    }

    public interface IMergeableResponse
    {
        IMergeableResponse Merge(IMergeableResponse response);
    }

    public interface IRequest {}

    /*public static class Endpoints
    {
        public static EndpointMapItem[] AvailableEndpoints = {
            EndpointMapItem.Create<GotoDefinitionRequest, GotoDefinitionResponse>("/gotodefinition"),
            EndpointMapItem.Create<FindSymbolsRequest, QuickFixResponse>("/findsymbols"),
            EndpointMapItem.Create<UpdateBufferRequest, object>("/updatebuffer"),
            EndpointMapItem.Create<ChangeBufferRequest, object>("/changebuffer"),
            EndpointMapItem.Create<CodeCheckRequest, QuickFixResponse>("/codecheck"),
            EndpointMapItem.Create<IEnumerable<Request>, object>( "/filesChanged"),
            EndpointMapItem.Create<FormatAfterKeystrokeRequest, FormatRangeResponse>("/formatAfterKeystroke"),
            EndpointMapItem.Create<FormatRangeRequest, FormatRangeResponse>("/formatRange"),
            EndpointMapItem.Create<CodeFormatRequest, CodeFormatResponse>("/codeformat"),
            EndpointMapItem.Create<HighlightRequest, HighlightResponse>("/highlight"),
            EndpointMapItem.Create<AutoCompleteRequest, IEnumerable<AutoCompleteResponse>>("/autocomplete"),
            EndpointMapItem.Create<FindImplementationsRequest, QuickFixResponse>("/findimplementations"),
            EndpointMapItem.Create<FindUsagesRequest, QuickFixResponse>("/findusages"),
            EndpointMapItem.Create<GotoFileRequest, QuickFixResponse>("/gotofile"),
            EndpointMapItem.Create<GotoRegionRequest, QuickFixResponse>("/gotoregion"),
            EndpointMapItem.Create<NavigateUpRequest, NavigateResponse>("/navigateup"),
            EndpointMapItem.Create<NavigateDownRequest, NavigateResponse>("/navigatedown"),
            EndpointMapItem.Create<TypeLookupRequest, TypeLookupResponse>("/typelookup"),
            EndpointMapItem.Create<GetCodeActionRequest , GetCodeActionsResponse>("/getcodeactions"),
            EndpointMapItem.Create<RunCodeActionRequest , RunCodeActionResponse>("/runcodeaction"),
            EndpointMapItem.Create<RenameRequest , RenameResponse>("/rename"),
            EndpointMapItem.Create<SignatureHelpRequest, SignatureHelp>("/signatureHelp"),
            EndpointMapItem.Create<MembersTreeRequest, FileMemberTree>("/currentfilemembersastree"),
            EndpointMapItem.Create<MembersFlatRequest, IEnumerable<QuickFix>>("/currentfilemembersasflat"),
            EndpointMapItem.Create<TestCommandRequest, GetTestCommandResponse>("/gettestcontext"),

            EndpointMapItem.Create<MetadataRequest, MetadataResponse>("/metadata", takeOne: true),
            EndpointMapItem.Create<PackageSourceRequest ,PackageSourceResponse >("/packagesource", takeOne: true),
            EndpointMapItem.Create<PackageSearchRequest , PackageSearchResponse>("/packagesearch", takeOne: true),
            EndpointMapItem.Create<PackageVersionRequest , PackageVersionResponse>("/packageversion", takeOne: true),

            EndpointMapItem.Create<WorkspaceInformationRequest, WorkspaceInformationResponse>("/projects", takeOne: true),
            EndpointMapItem.Create<ProjectInformationRequest, ProjectInformationResponse>("/project"),

            EndpointMapItem.Create<V2.GetCodeActionsRequest, V2.GetCodeActionsResponse>("/v2/getcodeactions"),
            EndpointMapItem.Create<V2.RunCodeActionRequest, V2.RunCodeActionResponse>("/v2/runcodeaction"),
        };

        public class EndpointMapItem
        {
            public static EndpointMapItem Create<TRequest, TResponse>(string endpoint, bool takeOne = false)
            {
                return new EndpointMapItem(endpoint, typeof(TRequest), typeof(TResponse), takeOne);
            }

            public EndpointMapItem(string endpointName, Type requestType, Type responseType, bool takeOne)
            {
                EndpointName = endpointName;
                RequestType = requestType;
                ResponseType = responseType;
                TakeOne = takeOne;
            }

            public string EndpointName { get; }
            public Type RequestType { get; }
            public Type ResponseType { get; }
            public bool TakeOne { get; }
        }
    }*/
}
