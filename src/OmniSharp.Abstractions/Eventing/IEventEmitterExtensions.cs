using System;
using System.Collections.Generic;
using OmniSharp.Models.Events;
using OmniSharp;
using OmniSharp.Models;
using System.Linq;

namespace OmniSharp.Eventing
{
    public static class IEventEmitterExtensions
    {
        public static void Error(this IEventEmitter emitter, Exception ex, string fileName = null)
        {
            emitter.Emit(
                EventTypes.Error,
                new ErrorMessage { FileName = fileName, Text = ex.ToString() });
        }

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

        public static void UnresolvedDepdendencies(this IEventEmitter emitter, string projectFilePath, IEnumerable<PackageDependency> unresolvedDependencies)
        {
            emitter.Emit(
                EventTypes.UnresolvedDependencies,
                new UnresolvedDependenciesMessage
                {
                    FileName = projectFilePath,
                    UnresolvedDependencies = unresolvedDependencies
                });
        }

        public static void ProjectInformation(this IEventEmitter emitter,
                                              HashedString projectId,
                                              IEnumerable<string> targetFrameworks,
                                              IEnumerable<HashedString> references,
                                              IEnumerable<HashedString> fileExtensions)
        {
            var projectConfiguration = new ProjectConfigurationMessage()
            {
                TargetFrameworks = targetFrameworks,
                ProjectId = projectId.Value,
                References = references.Select(hashed => hashed.Value),
                FileExtensions = fileExtensions.Select(hashed => hashed.Value)
            };

            emitter.Emit(
                EventTypes.ProjectConfiguration,
                projectConfiguration);
        }
    }
}
