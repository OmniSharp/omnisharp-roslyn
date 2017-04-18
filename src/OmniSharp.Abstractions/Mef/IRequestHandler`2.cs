using System.Threading.Tasks;

namespace OmniSharp.Mef
{
    public interface IRequestHandler<TRequest, TResponse> : IRequestHandler
    {
        Task<TResponse> Handle(TRequest request);
    }
}
