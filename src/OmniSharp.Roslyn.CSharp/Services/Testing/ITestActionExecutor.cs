using System.Threading.Tasks;
using OmniSharp.Models.V2;

namespace OmniSharp.Roslyn.CSharp.Services.Testing
{
    public interface ITestActionRunner
    {
        Task<ITestActionResult> RunAsync();
    }
    
    public interface ITestActionResult
    {
        RunCodeActionResponse ToRespnse();
    }
}