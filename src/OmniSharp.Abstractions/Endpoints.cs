using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmniSharp.Models;

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
            EndpointMapItem.Create<IEnumerable<Request>, object>( "filesChanged"),
            EndpointMapItem.Create<FormatAfterKeystrokeRequest, FormatRangeResponse>("formatAfterKeystroke"),
            EndpointMapItem.Create<FormatRangeRequest, FormatRangeResponse>("formatRange"),
            EndpointMapItem.Create<Request, CodeFormatResponse>("codeformat"),
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
