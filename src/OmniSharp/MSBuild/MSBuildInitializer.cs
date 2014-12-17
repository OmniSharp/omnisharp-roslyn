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
                        break;
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

                var docInfos = new List<DocumentInfo>();
                foreach (var file in projectFileInfo.SourceFiles)
                {
                    using (var stream = File.OpenRead(file))
                    {
                        var sourceText = SourceText.From(stream, encoding: Encoding.UTF8);
                        var id = DocumentId.CreateNewId(projectInfo.Id);
                        var version = VersionStamp.Create();

                        var loader = TextLoader.From(TextAndVersion.Create(sourceText, version));

                        docInfos.Add(DocumentInfo.Create(id, file, filePath: file, loader: loader));
                    }
                }

                projectInfo = projectInfo.WithDocuments(docInfos);

                _workspace.AddProject(projectInfo);

                projectFileInfo.WorkspaceId = projectInfo.Id;
                projectMap[projectFileInfo.ProjectFilePath] = projectFileInfo;
            }

            foreach (var project in projectMap.Values)
            {
                var references = project.References.ToList();

                foreach (var projectReferencePath in project.ProjectReferences)
                {
                    ProjectFileInfo projectReferenceInfo;
                    if (projectMap.TryGetValue(projectReferencePath, out projectReferenceInfo))
                    {
                        _workspace.AddProjectReference(project.WorkspaceId, new ProjectReference(projectReferenceInfo.WorkspaceId));
                        references.Remove(projectReferenceInfo.TargetPath);
                    }
                    else
                    {
                        _logger.WriteWarning(string.Format("Unable to resolve project reference '{0}' for '{1}'.", projectReferencePath, project.ProjectFilePath));
                    }
                }

                foreach (var reference in references)
                {
                    _workspace.AddMetadataReference(project.WorkspaceId, _metadataReferenceCache.GetMetadataReference(reference));
                }
            }

        }
    }
}