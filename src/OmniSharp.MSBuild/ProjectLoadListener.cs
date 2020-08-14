using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Eventing;
using OmniSharp.Models;
using OmniSharp.MSBuild.Notification;
using OmniSharp.MSBuild.ProjectFile;

namespace OmniSharp.MSBuild
{
    [Export(typeof(IMSBuildEventSink))]
    public class ProjectLoadListener : IMSBuildEventSink
    {
        internal const string TargetFramework = nameof(TargetFramework);
        internal const string TargetFrameworkVersion = nameof(TargetFrameworkVersion);
        internal const string TargetFrameworks = nameof(TargetFrameworks);
        private const string MSBuildProjectFullPath = nameof(MSBuildProjectFullPath);
        private readonly ILogger _logger;
        private readonly IEventEmitter _eventEmitter;
        private static readonly VsTfmAndFileExtHashingAlgorithm _tfmAndFileHashingAlgorithm = new VsTfmAndFileExtHashingAlgorithm();
        private static readonly VsReferenceHashingAlgorithm _referenceHashingAlgorithm = new VsReferenceHashingAlgorithm();

        [ImportingConstructor]
        public ProjectLoadListener(ILoggerFactory loggerFactory, IEventEmitter eventEmitter)
        {
            _logger = loggerFactory.CreateLogger<ProjectLoadListener>();
            _eventEmitter = eventEmitter;
        }

        public void ProjectLoaded(ProjectLoadedEventArgs args)
        {
            try
            {
                var projectId = GetProjectId(args);
                var sessionId = GetSessionId(args);
                var targetFrameworks = GetTargetFrameworks(args.ProjectInstance);
                var sdkVersion = GetSdkVersion(args);
                var outputKind = GetOutputKind(args);
                var projectCapabilities = GetProjectCapabilities(args.ProjectInstance);

                if (args.References == null)
                {
                    return;
                }

                var hashedReferences = GetHashedReferences(args);
                var (hashedFileExtensions, fileCounts) = GetUniqueHashedFileExtensionsAndCounts(args);

                _eventEmitter.ProjectInformation(projectId, sessionId, (int)outputKind, projectCapabilities, targetFrameworks, sdkVersion, hashedReferences, hashedFileExtensions, fileCounts);
            }
            catch (Exception ex)
            {
                _logger.LogError("Unexpected exception got thrown from project load listener: " + ex);
            }
        }

        private static (IEnumerable<HashedString> Extensions, IEnumerable<int> Counts) GetUniqueHashedFileExtensionsAndCounts(ProjectLoadedEventArgs args)
        {
            var contentFiles = args.ProjectInstance
                .GetItems(ItemNames.Content)
                .Select(item => item.GetMetadataValue(MetadataNames.FullPath));
            var allFiles = args.SourceFiles.Concat(contentFiles);
            var filesCounts = allFiles.GroupBy(file => Path.GetExtension(file)).ToDictionary(kvp => kvp.Key, kvp => kvp.Count());
            IEnumerable<string> fileExtensions = filesCounts.Select(kvp => kvp.Key);
            IEnumerable<int> fileCounts = filesCounts.Select(kvp => kvp.Value);
            return (fileExtensions.Select(_tfmAndFileHashingAlgorithm.HashInput), fileCounts);
        }

        private static HashedString GetProjectId(ProjectLoadedEventArgs args)
        {
            if (args.ProjectIdIsDefinedInSolution)
            {
                //If we are getting a raw guid we should not hash it
                return new HashedString(args.Id.Id.ToString());
            }

            var projectFilePath = args.ProjectInstance.GetPropertyValue(MSBuildProjectFullPath);
            var content = File.ReadAllText(projectFilePath);
            //create a hash from the filename and the content
            return _referenceHashingAlgorithm.HashInput($"Filename: {Path.GetFileName(projectFilePath)}\n{content}");
        }

        private static HashedString GetSdkVersion(ProjectLoadedEventArgs args)
        {
            return _tfmAndFileHashingAlgorithm.HashInput(args.SdkVersion.ToString());
        }

        private static HashedString GetSessionId(ProjectLoadedEventArgs args)
        {
            return _tfmAndFileHashingAlgorithm.HashInput(args.SessionId.ToString());
        }

        private static OutputKind GetOutputKind(ProjectLoadedEventArgs args)
        {
            return PropertyConverter.ToOutputKind(args.ProjectInstance.GetPropertyValue(PropertyNames.OutputType));
        }

        private static IEnumerable<HashedString> GetHashedReferences(ProjectLoadedEventArgs args)
        {
            var referenceNames = args.References.Select(reference => Path.GetFileNameWithoutExtension(reference).ToLower());
            return referenceNames.Select(_referenceHashingAlgorithm.HashInput);
        }

        private IEnumerable<string> GetProjectCapabilities(ProjectInstance projectInstance)
        {
            return projectInstance.GetItems(ItemNames.ProjectCapability).Select(item => item.ToString());
        }

        // Internal for testing
        internal static IEnumerable<string> GetTargetFrameworks(ProjectInstance projectInstance)
        {
            var targetFrameworks = projectInstance.GetPropertyValue(TargetFrameworks);
            if (string.IsNullOrEmpty(targetFrameworks))
            {
                targetFrameworks = projectInstance.GetPropertyValue(TargetFramework);
            }
            if (string.IsNullOrEmpty(targetFrameworks))
            {
                targetFrameworks = projectInstance.GetPropertyValue(TargetFrameworkVersion);
            }

            if (!string.IsNullOrEmpty(targetFrameworks))
            {
                return targetFrameworks.Split(';')
                    .Where(tfm => !string.IsNullOrWhiteSpace(tfm))
                    .Select(tfm => tfm.ToLower());
            }

            return new List<string>();
        }
    }
}
