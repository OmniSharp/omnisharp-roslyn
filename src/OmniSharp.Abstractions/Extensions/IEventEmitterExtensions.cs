using OmniSharp.Models;

namespace OmniSharp.Services
{
    public static class IEventEmitterExtensions
    {
        public static void RestoreStarted(this IEventEmitter emitter, string projectPath)
        {
            emitter.Emit(
                EventTypes.PackageRestoreStarted,
                new PackageRestoreMessage { FileName = projectPath });
        }

        public static void RestoreFinished(this IEventEmitter emitter, string projectPath, bool succeeded)
        {
            emitter.Emit(
                EventTypes.PackageRestoreFinished,
                new PackageRestoreMessage
                {
                    FileName = projectPath,
                    Succeeded = succeeded
                });
        }
    }
}
