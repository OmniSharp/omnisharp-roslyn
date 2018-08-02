using System.Threading.Tasks;

namespace OmniSharp.Services
{
    public interface IUpdates
    {
        Task WaitForUpdatesAsync();
    }
} 
