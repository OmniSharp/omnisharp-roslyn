using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;

namespace OmniSharp.Roslyn.CSharp.Services
{
    [OmniSharpHandler(OmniSharpEndpoints.GetFixAll, LanguageNames.CSharp)]
    public class GetFixAllCodeActionService : IRequestHandler<GetFixAllRequest, GetFixAllResponse>
    {
        public Task<GetFixAllResponse> Handle(GetFixAllRequest request)
        {
            return Task.FromResult(new GetFixAllResponse());
        }
    }

    public class GetFixAllResponse
    {
    }

    public class GetFixAllRequest
    {
    }
}