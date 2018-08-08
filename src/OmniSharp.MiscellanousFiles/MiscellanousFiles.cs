﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp.FileSystem;
using OmniSharp.FileWatching;
using OmniSharp.Mef;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.Services;

namespace OmniSharp.MiscellanousFiles
{
    [ExtensionOrder(After = ProjectSystemNames.MSBuildProjectSystem)]
    [ExtensionOrder(After = ProjectSystemNames.DotNetProjectSystem)]
    [ExportProjectSystem(ProjectSystemNames.MiscellanousFilesProjectSystem), Shared]
    public class MiscellanousFilesProjectSystem : IProjectSystem
    {
        private readonly string miscFileExtension = ".cs";
        public string Key { get; } = "MiscellanousFiles";
        public string Language { get; } = LanguageNames.CSharp;
        IEnumerable<string> IProjectSystem.Extensions => new[] { miscFileExtension };
        public bool EnabledByDefault { get; } = true;

        private readonly ConcurrentDictionary<string, DocumentId> _documents = new ConcurrentDictionary<string, DocumentId>();
        private readonly OmniSharpWorkspace _workspace;
        private readonly IFileSystemWatcher _fileSystemWatcher;
        private readonly FileSystemHelper _fileSystemHelper;
        private readonly List<IUpdates> _projectSystems;
        private readonly ILogger _logger;
        private ProjectId _projectId;

        [ImportingConstructor]
        public MiscellanousFilesProjectSystem(OmniSharpWorkspace workspace, IFileSystemWatcher fileSystemWatcher, FileSystemHelper fileSystemHelper,
            ILoggerFactory loggerFactory, [ImportMany] IEnumerable<Lazy<IProjectSystem, ProjectSystemMetadata>> projectSystems)
        {
            _workspace = workspace;
            _fileSystemWatcher = fileSystemWatcher ?? throw new ArgumentNullException(nameof(fileSystemWatcher));
            _fileSystemHelper = fileSystemHelper;
            _logger = loggerFactory.CreateLogger<MiscellanousFilesProjectSystem>();
            _projectSystems = projectSystems.
                Where
                (ps => ps.Metadata.Name == ProjectSystemNames.MSBuildProjectSystem || 
                ps.Metadata.Name == ProjectSystemNames.DotNetProjectSystem).
                Select(ps => ps.Value).
                Select(ps => ps as IUpdates)
                .ToList();
        }

        Task<object> IProjectSystem.GetProjectModelAsync(string filePath)
        {
            return Task.FromResult<object>(null);
        }

        Task<object> IProjectSystem.GetWorkspaceModelAsync(WorkspaceInformationRequest request)
        {
            return Task.FromResult<object>(null);
        }

        void IProjectSystem.Initalize(IConfiguration configuration)
        {
            var allFiles = _fileSystemHelper.GetFiles("**/*.cs");
            foreach (var filePath in allFiles)
                AddIfMiscellanousFile(filePath);

            _fileSystemWatcher.Watch(miscFileExtension, OnMiscellanousFileChanged);
        }

        private async void AddIfMiscellanousFile(string filePath)
        {
            //wait for the project systems to finish processing the updates
            foreach(var projectSystem in _projectSystems)
            {
                await projectSystem.WaitForUpdatesAsync();
            }

            var absoluteFilePath = new FileInfo(filePath).FullName;
            if (!File.Exists(absoluteFilePath))
                return;
            if (_workspace.GetDocument(filePath) == null)
            {
                if (this._projectId == null)
                {
                    string assemblyName = Guid.NewGuid().ToString("N");
                    //If not project exists for the Misc files, create one
                    var projectInfo = ProjectInfo.Create(
                   id: ProjectId.CreateNewId(),
                   version: VersionStamp.Create(),
                   name: "MiscellanousFiles",
                   metadataReferences: new MetadataReference[] { MetadataReference.CreateFromFile((typeof(object).Assembly).Location) },
                   assemblyName: assemblyName,
                   language: Language);

                    _workspace.AddProject(projectInfo);
                    _projectId = projectInfo.Id;
                }

                _documents[absoluteFilePath] = _workspace.AddMiscellanousFileDocument(_projectId, absoluteFilePath);
                _logger.LogInformation($"Successfully added file '{absoluteFilePath}' to workspace");
            }
        }

        private void OnMiscellanousFileChanged(string filePath, FileChangeType changeType)
        {
            if (changeType == FileChangeType.Unspecified && File.Exists(filePath) ||
                changeType == FileChangeType.Create)
            {
                AddIfMiscellanousFile(filePath);
            }

            else if (changeType == FileChangeType.Unspecified && !File.Exists(filePath) ||
                changeType == FileChangeType.Delete)
            {
                RemoveFromWorkspace(filePath);
            }
        }

        private void RemoveFromWorkspace(string filePath)
        {
            if (_documents.TryRemove(filePath, out var documentId))
            {
                _workspace.RemoveMiscellanousFileDocument(documentId);
                _logger.LogDebug($"Removed file '{filePath}' from the workspace.");
            }
        }
    }
}
