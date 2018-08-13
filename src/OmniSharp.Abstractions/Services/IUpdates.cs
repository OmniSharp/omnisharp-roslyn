using System.Threading.Tasks;

namespace OmniSharp.Services
{
    public interface IWaitableProjectSystem: IProjectSystem
    {
        Task WaitForUpdatesAsync();
    }
} 
