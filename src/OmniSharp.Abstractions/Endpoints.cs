using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmniSharp.Models;
using V2 = OmniSharp.Models.V2;

namespace OmniSharp
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public class EndpointNameAttribute : Attribute
    {
        public string Name { get; }
        public EndpointNameAttribute(string name)
        {
            Name = name;
        }
    }

    public interface RequestHandler<TRequest, TResponse>
    {
        Task<TResponse> Handle(TRequest request);
    }

    public interface IMergeableResponse
    {
        IMergeableResponse Merge(IMergeableResponse response);
    }

    public static class Endpoints
    {
        public static EndpointMapItem[] AvailableEndpoints = {
            EndpointMapItem.Create<GotoDefinitionRequest, GotoDefinitionResponse>("/gotodefinition"),
            EndpointMapItem.Create<FindSymbolsRequest, QuickFixResponse>("/findsymbols"),
            EndpointMapItem.Create<UpdateBufferRequest, object>("/updatebuffer"),
            EndpointMapItem.Create<ChangeBufferRequest, object>("/changebuffer"),
            EndpointMapItem.Create<Request, QuickFixResponse>("/codecheck"),
            EndpointMapItem.Create<IEnumerable<Request>, object>( "/filesChanged"),
            EndpointMapItem.Create<FormatAfterKeystrokeRequest, FormatRangeResponse>("/formatAfterKeystroke"),
            EndpointMapItem.Create<FormatRangeRequest, FormatRangeResponse>("/formatRange"),
            EndpointMapItem.Create<Request, CodeFormatResponse>("/codeformat"),
            EndpointMapItem.Create<HighlightRequest, HighlightResponse>("/highlight"),
            EndpointMapItem.Create<AutoCompleteRequest, IEnumerable<AutoCompleteResponse>>("/autocomplete"),
            EndpointMapItem.Create<Request, QuickFixResponse>("/findimplementations"),
            EndpointMapItem.Create<FindUsagesRequest, QuickFixResponse>("/findusages"),
            EndpointMapItem.Create<Request, QuickFixResponse>("/gotofile"),
            EndpointMapItem.Create<Request, QuickFixResponse>("/gotoregion"),
            EndpointMapItem.Create<MetadataRequest, MetadataResponse>("/metadata"),
            EndpointMapItem.Create<Request, NavigateResponse>("/navigateup"),
            EndpointMapItem.Create<Request, NavigateResponse>("/navigatedown"),
            EndpointMapItem.Create<TypeLookupRequest, TypeLookupResponse>("/typelookup"),
            EndpointMapItem.Create<PackageSourceRequest ,PackageSourceResponse >("/packagesource"),
            EndpointMapItem.Create<PackageSearchRequest , PackageSearchResponse>("/packagesearch"),
            EndpointMapItem.Create<PackageVersionRequest , PackageVersionResponse>("/packageversion"),
            EndpointMapItem.Create<CodeActionRequest , GetCodeActionsResponse>("/getcodeactions"),
            EndpointMapItem.Create<CodeActionRequest , RunCodeActionResponse>("/runcodeaction"),
            EndpointMapItem.Create<RenameRequest , RenameResponse>("/rename"),
            EndpointMapItem.Create<Request, SignatureHelp>("/signatureHelp"),
            EndpointMapItem.Create<Request, FileMemberTree>("/currentfilemembersastree"),
            EndpointMapItem.Create<Request, IEnumerable<QuickFix>>("/currentfilemembersasflat"),
            EndpointMapItem.Create<TestCommandRequest, GetTestCommandResponse>("/gettestcontext"),

            EndpointMapItem.Create<V2.GetCodeActionsRequest, V2.GetCodeActionsResponse>("/v2/getcodeactions"),
            EndpointMapItem.Create<V2.RunCodeActionRequest, V2.RunCodeActionResponse>("/v2/runcodeaction"),
    };

        public class EndpointMapItem
        {
            public static EndpointMapItem Create<TRequest, TResponse>(string endpoint)
            {
                return new EndpointMapItem(endpoint, typeof(TRequest), typeof(TResponse));
            }

            public EndpointMapItem(string endpointName, Type requestType, Type responseType)
            {
                EndpointName = endpointName;
                RequestType = requestType;
                ResponseType = responseType;
            }

            public string EndpointName { get; }
            public Type RequestType { get; }
            public Type ResponseType { get; }
        }
    }
}
