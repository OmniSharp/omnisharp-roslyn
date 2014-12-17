using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Framework.Logging;
using OmniSharp.MSBuild.ProjectFile;
using OmniSharp.Services;

namespace OmniSharp.MSBuild
{
    public class MSBuildInitializer : IWorkspaceInitializer
    {
        private readonly OmnisharpWorkspace _workspace;
        private readonly IMetadataFileReferenceCache _metadataReferenceCache;
        private readonly IOmnisharpEnvironment _env;
        private readonly ILogger _logger;

        private static readonly Guid[] _supportsProjectTypes = new[] {
            new Guid("fae04ec0-301f-11d3-bf4b-00c04f79efbc") // CSharp
        };

        private readonly Dictionary<Guid, ProjectId> _projectMap = new Dictionary<Guid, ProjectId>();

        public MSBuildInitializer(OmnisharpWorkspace workspace,
                                  IOmnisharpEnvironment env,
                                  ILoggerFactory loggerFactory,
                                  IMetadataFileReferenceCache metadataReferenceCache)
        {
            _workspace = workspace;
            _metadataReferenceCache = metadataReferenceCache;
            _env = env;
            _logger = loggerFactory.Create<MSBuildInitializer>();
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

            using (var stream = File.OpenRead(solutionFilePath))
            using (var reader = new StreamReader(stream))
            {
                solutionFile = SolutionFile.Parse(reader);
            }

            _logger.WriteInformation(string.Format("Detecting projects in '{0}'.", solutionFilePath));

            var projectMap = new Dictionary<string, ProjectFileInfo>();

            foreach (var block in solutionFile.ProjectBlocks)
            {
                if (!_supportsProjectTypes.Contains(block.ProjectTypeGuid))
                {
                    _logger.WriteWarning("Skipped unsupported project type '{0}'", block.ProjectPath);
                    continue;
                }

                if (_projectMap.ContainsKey(block.ProjectGuid))
                {
                    continue;
                }

                var projectFilePath = Path.GetFullPath(Path.Combine(_env.Path, block.ProjectPath));

                _logger.WriteInformation(string.Format("Loading project from '{0}'.", projectFilePath));

                ProjectFileInfo projectFileInfo = null;

                try
                {
                    projectFileInfo = ProjectFileInfo.Create(_env.Path, projectFilePath);

                    if (projectFileInfo == null)
                    {
                        _logger.WriteWarning(string.Format("Failed to process project file '{0}'.", projectFilePath));
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    _logger.WriteWarning(string.Format("Failed to process project file '{0}'.", projectFilePath), ex);
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
                projectMap[projectFileInfo.ProjectFilePath] = projectFileInfo;

                _projectMap[block.ProjectGuid] = projectInfo.Id;
            }

            foreach (var projectFileInfo in projectMap.Values)
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
                    if (projectMap.TryGetValue(projectReferencePath, out projectReferenceInfo))
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
}