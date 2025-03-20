using System.Threading;
using System.Threading.Tasks;

namespace OmniSharp.MSBuild.Notification
{
    public interface IMSBuildEventSink
    {
        ValueTask ProjectLoadingStartedAsync(string projectPath, CancellationToken cancellationToken = default);
        Task ProjectLoadedAsync(ProjectLoadedEventArgs e, CancellationToken cancellationToken = default);
    }
}
