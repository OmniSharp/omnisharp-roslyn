using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Framework.Logging;
using OmniSharp.MSBuild.ProjectFile;
using OmniSharp.Services;

namespace OmniSharp.MSBuild
{
    public class MSBuildProjectSystem : IProjectSystem
    {
        private readonly OmnisharpWorkspace _workspace;
        private readonly IMetadataFileReferenceCache _metadataReferenceCache;
        private readonly IOmnisharpEnvironment _env;
        private readonly ILogger _logger;

        private static readonly Guid[] _supportsProjectTypes = new[] {
            new Guid("fae04ec0-301f-11d3-bf4b-00c04f79efbc") // CSharp
        };

        private readonly MSBuildContext _context;
        private readonly IFileSystemWatcher _watcher;

        public MSBuildProjectSystem(OmnisharpWorkspace workspace,
                                    IOmnisharpEnvironment env,
                                    ILoggerFactory loggerFactory,
                                    IMetadataFileReferenceCache metadataReferenceCache,
                                    IFileSystemWatcher watcher,
                                    MSBuildContext context)
        {
            _workspace = workspace;
            _metadataReferenceCache = metadataReferenceCache;
            _watcher = watcher;
            _env = env;
            _logger = loggerFactory.Create<MSBuildProjectSystem>();
            _context = context;
        }

        public void Initalize()
        {
            var solutionFilePath = _env.SolutionFilePath;

            if (string.IsNullOrEmpty(solutionFilePath))
            {
                var solutions = Directory.GetFiles(_env.Path, "*.sln");

                switch (solutions.Length)
                {
                    case 0:
                        _logger.WriteInformation(string.Format("No solution files found in '{0}'", _env.Path));
                        return;
                    case 1:
                        solutionFilePath = solutions[0];
                        break;
                    default:
                        _logger.WriteError("Could not determine solution file");
                        return;
                }
            }

            SolutionFile solutionFile = null;

            _context.SolutionPath = solutionFilePath;

            using (var stream = File.OpenRead(solutionFilePath))
            using (var reader = new StreamReader(stream))
            {
                solutionFile = SolutionFile.Parse(reader);
            }

            _logger.WriteInformation(string.Format("Detecting projects in '{0}'.", solutionFilePath));

            foreach (var block in solutionFile.ProjectBlocks)
            {
                if (!_supportsProjectTypes.Contains(block.ProjectTypeGuid))
                {
                    _logger.WriteWarning("Skipped unsupported project type '{0}'", block.ProjectPath);
                    continue;
                }

                if (_context.ProjectGuidToWorkspaceMapping.ContainsKey(block.ProjectGuid))
                {
                    continue;
                }

                var projectFilePath = Path.GetFullPath(Path.GetFullPath(Path.Combine(_env.Path, block.ProjectPath.Replace('\\', Path.DirectorySeparatorChar))));

                _logger.WriteInformation(string.Format("Loading project from '{0}'.", projectFilePath));

                var projectFileInfo = CreateProject(projectFilePath);

                if (projectFileInfo == null)
                {
                    continue;
                }

                var projectInfo = ProjectInfo.Create(ProjectId.CreateNewId(projectFileInfo.Name),
                                                     VersionStamp.Create(),
                                                     projectFileInfo.Name,
                                                     projectFileInfo.AssemblyName,
                                                     LanguageNames.CSharp,
                                                     projectFileInfo.ProjectFilePath);

                _workspace.AddProject(projectInfo);

                projectFileInfo.WorkspaceId = projectInfo.Id;

                _context.Projects[projectFileInfo.ProjectFilePath] = projectFileInfo;
                _context.ProjectGuidToWorkspaceMapping[block.ProjectGuid] = projectInfo.Id;

                _watcher.Watch(projectFilePath, OnProjectChanged);
            }

            foreach (var projectFileInfo in _context.Projects.Values)
            {
                UpdateProject(projectFileInfo);
            }

        }

        private ProjectFileInfo CreateProject(string projectFilePath)
        {
            ProjectFileInfo projectFileInfo = null;

            try
            {
                projectFileInfo = ProjectFileInfo.Create(_logger, _env.Path, projectFilePath);

                if (projectFileInfo == null)
                {
                    _logger.WriteWarning(string.Format("Failed to process project file '{0}'.", projectFilePath));
                }
            }
            catch (Exception ex)
            {
                _logger.WriteWarning(string.Format("Failed to process project file '{0}'.", projectFilePath), ex);
            }

            return projectFileInfo;
        }

        private void OnProjectChanged(string projectFilePath)
        {
            var newProjectInfo = CreateProject(projectFilePath);

            // Should we remove the entry if the project is malformed?
            if (newProjectInfo != null)
            {
                lock (_context)
                {
                    ProjectFileInfo oldProjectFileInfo;
                    if (_context.Projects.TryGetValue(projectFilePath, out oldProjectFileInfo))
                    {
                        _context.Projects[projectFilePath] = newProjectInfo;
                        newProjectInfo.WorkspaceId = oldProjectFileInfo.WorkspaceId;
                        UpdateProject(newProjectInfo);
                    }
                }
            }
        }

        private void UpdateProject(ProjectFileInfo projectFileInfo)
        {
            var project = _workspace.CurrentSolution.GetProject(projectFileInfo.WorkspaceId);

            var unusedDocuments = project.Documents.ToDictionary(d => d.FilePath, d => d.Id);

            foreach (var file in projectFileInfo.SourceFiles)
            {
                if (unusedDocuments.Remove(file))
                {
                    continue;
                }

                using (var stream = File.OpenRead(file))
                {
                    var sourceText = SourceText.From(stream, encoding: Encoding.UTF8);
                    var id = DocumentId.CreateNewId(projectFileInfo.WorkspaceId);
                    var version = VersionStamp.Create();

                    var loader = TextLoader.From(TextAndVersion.Create(sourceText, version));

                    _workspace.AddDocument(DocumentInfo.Create(id, file, filePath: file, loader: loader));
                }
            }

            foreach (var unused in unusedDocuments)
            {
                _workspace.RemoveDocument(unused.Value);
            }

            var unusedProjectReferences = new HashSet<ProjectReference>(project.ProjectReferences);

            foreach (var projectReferencePath in projectFileInfo.ProjectReferences)
            {
                ProjectFileInfo projectReferenceInfo;
                if (_context.Projects.TryGetValue(projectReferencePath, out projectReferenceInfo))
                {
                    var reference = new ProjectReference(projectReferenceInfo.WorkspaceId);

                    if (unusedProjectReferences.Remove(reference))
                    {
                        // This reference already exists
                        continue;
                    }

                    _workspace.AddProjectReference(project.Id, reference);
                }
                else
                {
                    _logger.WriteWarning(string.Format("Unable to resolve project reference '{0}' for '{1}'.", projectReferencePath, projectFileInfo.ProjectFilePath));
                }
            }

            foreach (var unused in unusedProjectReferences)
            {
                _workspace.RemoveProjectReference(project.Id, unused);
            }

            var unusedAnalyzers = new Dictionary<string, AnalyzerReference>(project.AnalyzerReferences.ToDictionary(a => a.FullPath));

            foreach (var analyzerPath in projectFileInfo.Analyzers)
            {
                if (!File.Exists(analyzerPath))
                {
                    _logger.WriteWarning(string.Format("Unable to resolve assembly '{0}'", analyzerPath));
                }
                else
                {
                    if (unusedAnalyzers.Remove(analyzerPath))
                    {
                        continue;
                    }
#if ASPNET50
                    var analyzerReference = new AnalyzerFileReference(analyzerPath);
                    project.AddAnalyzerReference(analyzerReference);
#endif
                }
            }

            foreach (var analyzerReference in unusedAnalyzers.Values)
            {
                project.RemoveAnalyzerReference(analyzerReference);
            }

            var unusedReferences = new HashSet<MetadataReference>(project.MetadataReferences);

            foreach (var referencePath in projectFileInfo.References)
            {
                if (!File.Exists(referencePath))
                {
                    _logger.WriteWarning(string.Format("Unable to resolve assembly '{0}'", referencePath));
                }
                else
                {
                    var metadataReference = _metadataReferenceCache.GetMetadataReference(referencePath);

                    if (unusedReferences.Remove(metadataReference))
                    {
                        continue;
                    }

                    _logger.WriteVerbose(string.Format("Adding reference '{0}' to '{1}'.", referencePath, projectFileInfo.ProjectFilePath));
                    _workspace.AddMetadataReference(project.Id, metadataReference);
                }
            }

            foreach (var reference in unusedReferences)
            {
                _workspace.RemoveMetadataReference(project.Id, reference);
            }
        }
    }
}