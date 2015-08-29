using System.Threading.Tasks;
using OmniSharp.Models;

namespace OmniSharp.Endpoints
{
    public interface IGotoDefintion
    {
        Task<bool> IsApplicableTo(GotoDefinitionRequest request);
        Task<GotoDefinitionResponse> GotoDefinition(GotoDefinitionRequest request);
    }
}
