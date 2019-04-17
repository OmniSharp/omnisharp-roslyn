using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.Extensions.Logging;
using OmniSharp.Eventing;
using OmniSharp.Models;
using OmniSharp.MSBuild.Notification;

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
                var targetFrameworks = GetTargetFrameworks(args.ProjectInstance);

                if (args.References == null)
                {
                    return;
                }

                var hashedReferences = GetHashedReferences(args);
                var hashedFileExtensions = GetUniqueHashedFileExtensions(args);

                _eventEmitter.ProjectInformation(projectId, targetFrameworks, hashedReferences, hashedFileExtensions);
            }
            catch (Exception ex)
            {
                _logger.LogError("Unexpected exception got thrown from project load listener: " + ex);
            }
        }

        private static IEnumerable<HashedString> GetUniqueHashedFileExtensions(ProjectLoadedEventArgs args)
        {
            IEnumerable<string> sourceFileExtensions = args.SourceFiles.Select(file => Path.GetExtension(file)).Distinct();
            return sourceFileExtensions.Select(_tfmAndFileHashingAlgorithm.HashInput);
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

        private static IEnumerable<HashedString> GetHashedReferences(ProjectLoadedEventArgs args)
        {
            var referenceNames = args.References.Select(reference => Path.GetFileNameWithoutExtension(reference).ToLower());
            return referenceNames.Select(_referenceHashingAlgorithm.HashInput);
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
