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
        internal const string TargetFrameworkPropertyName = "TargetFramework";
        internal const string TargetFrameworkVersionPropertyName = "TargetFrameworkVersion";
        internal const string TargetFrameworksPropertyName = "TargetFrameworks";
        private const string MSBuildProjectFullPathPropertyName = "MSBuildProjectFullPath";
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
                var projectGuid = GetProjectId(args);
                var hashedTargetFrameworks = GetHashedTargetFrameworks(args.ProjectInstance);

                if (args.References == null)
                {
                    return;
                }

                var hashedReferences = GetHashedReferences(args);
                var hashedFileExtensions = GetUniqueHashedFileExtensions(args);

                _eventEmitter.ProjectInformation(projectGuid, hashedTargetFrameworks, hashedReferences, hashedFileExtensions);
            }
            catch (Exception ex)
            {
                _logger.LogError("Unexpected exception got thrown from project load listener: " + ex);
            }
        }

        private static IEnumerable<HashedString> GetUniqueHashedFileExtensions(ProjectLoadedEventArgs args)
        {
            IEnumerable<string> sourceFileExtensions = args.SourceFiles.Select(file => Path.GetExtension(file)).Distinct();
            return sourceFileExtensions.Select(ext => _tfmAndFileHashingAlgorithm.HashInput(ext));
        }

        private static HashedString GetProjectId(ProjectLoadedEventArgs args)
        {
            if (args.HasProjectIdGeneratedFromSolution)
            {
                //If we are getting a raw guid we should not hash it
                return new HashedString(args.Id.Id.ToString());
            }

            var projectFilePath = args.ProjectInstance.GetPropertyValue(MSBuildProjectFullPathPropertyName);
            var content = File.ReadAllText(projectFilePath);
            //create a hash from the filename and the content
            return _referenceHashingAlgorithm.HashInput($"Filename: {Path.GetFileName(projectFilePath)}\n{content}");
        }

        private static IEnumerable<HashedString> GetHashedReferences(ProjectLoadedEventArgs args)
        {
            var referenceNames = args.References.Select(reference => Path.GetFileNameWithoutExtension(reference).ToLower());
            return referenceNames.Select(reference => _referenceHashingAlgorithm.HashInput(reference));
        }

        // Internal for testing
        internal static IEnumerable<HashedString> GetHashedTargetFrameworks(ProjectInstance projectInstance)
        {
            var targetFrameworks = projectInstance.GetPropertyValue(TargetFrameworksPropertyName);
            if (string.IsNullOrEmpty(targetFrameworks))
            {
                targetFrameworks = projectInstance.GetPropertyValue(TargetFrameworkPropertyName);
            }
            if (string.IsNullOrEmpty(targetFrameworks))
            {
                targetFrameworks = projectInstance.GetPropertyValue(TargetFrameworkVersionPropertyName);
            }

            if (!string.IsNullOrEmpty(targetFrameworks))
            {
                return targetFrameworks.Split(';')
                    .Where(tfm => !string.IsNullOrWhiteSpace(tfm))
                    .Select(tfm => tfm.ToLower())
                    .Select(tfm => _tfmAndFileHashingAlgorithm.HashInput(tfm));
            }

            return new List<HashedString>();
        }
    }
}
