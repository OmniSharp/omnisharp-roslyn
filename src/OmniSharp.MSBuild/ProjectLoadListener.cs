using System;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
                var targetFrameworks = args.TargetFrameworks;
                var sdkVersion = GetSdkVersion(args);
                var outputKind = args.OutputKind;
                var projectCapabilities = args.ProjectCapabilities;

                if (args.References == null)
                {
                    return;
                }

                var hashedReferences = GetHashedReferences(args);
                var (hashedFileExtensions, fileCounts) = GetUniqueHashedFileExtensionsAndCounts(args);

                var sdkStyleProject = args.IsSdkStyleProject;
                _eventEmitter.ProjectInformation(projectId, sessionId, (int)outputKind, projectCapabilities, targetFrameworks, sdkVersion, hashedReferences, hashedFileExtensions, fileCounts, sdkStyleProject);
            }
            catch (Exception ex)
            {
                _logger.LogError("Unexpected exception got thrown from project load listener: " + ex);
            }
        }


        private static (IEnumerable<HashedString> Extensions, IEnumerable<int> Counts) GetUniqueHashedFileExtensionsAndCounts(ProjectLoadedEventArgs args)
        {
            var allFiles = args.SourceFiles.Concat(args.ContentFiles);
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

            var projectFilePath = args.ProjectFilePath;
            var content = File.ReadAllText(projectFilePath);
            //create a hash from the filename and the content
            return _referenceHashingAlgorithm.HashInput($"Filename: {Path.GetFileName(projectFilePath)}\n{content}");
        }

        private static HashedString GetSdkVersion(ProjectLoadedEventArgs args)
        {
            return _tfmAndFileHashingAlgorithm.HashInput(args.SdkVersion?.ToString());
        }

        private static HashedString GetSessionId(ProjectLoadedEventArgs args)
        {
            return _tfmAndFileHashingAlgorithm.HashInput(args.SessionId.ToString());
        }

        private static IEnumerable<HashedString> GetHashedReferences(ProjectLoadedEventArgs args)
        {
            var referenceNames = args.References.Select(reference => Path.GetFileNameWithoutExtension(reference).ToLower());
            var hashed = referenceNames.Select(_referenceHashingAlgorithm.HashInput);
            return hashed;
        }
    }
}
