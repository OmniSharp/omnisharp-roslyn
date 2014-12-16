using Microsoft.Framework.Logging;
using OmniSharp.Services;
using Microsoft.CodeAnalysis;
using System.IO;
using Microsoft.CodeAnalysis.MSBuild;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System;

namespace OmniSharp.MSBuild
{
    public class MSBuildInitializer : IWorkspaceInitializer
    {
        private readonly OmnisharpWorkspace _workspace;
        private readonly IMetadataFileReferenceCache _referenceCache;
        private readonly IOmnisharpEnvironment _env;
        private readonly ILogger _logger;

        private readonly IDictionary<Guid, ProjectFile.ProjectFile> _projects = new Dictionary<Guid, ProjectFile.ProjectFile>();

        public MSBuildInitializer(OmnisharpWorkspace workspace,
                                  IMetadataFileReferenceCache referenceCache,
                                  IOmnisharpEnvironment env,
                                  ILoggerFactory loggerFactory)
        {
            _workspace = workspace;
            _referenceCache = referenceCache;
            _env = env;
            _logger = loggerFactory.Create<MSBuildInitializer>();
        }

        public void Initalize()
        {
            if (string.IsNullOrWhiteSpace(_env.SolutionFilePath))
            {
                _logger.WriteVerbose("NO solution file found");
                return;
            }

            var sln = SolutionFile.Parse(new StringReader(File.ReadAllText(_env.SolutionFilePath)));
            var unusedProjects = new HashSet<Guid>(_projects.Keys);
            var slnDirectory = Path.GetDirectoryName(_env.SolutionFilePath);

            foreach (var block in sln.ProjectBlocks)
            {
                if(_projects.ContainsKey(block.ProjectGuid))
                {
                    continue;
                }

                var proj = new ProjectFile.ProjectFile(block.ProjectGuid, block.ProjectName, Path.Combine(slnDirectory, block.ProjectPath));
                _workspace.AddProject(ProjectInfo.Create(proj.ProjectId, VersionStamp.Create(), proj.Name, proj.AssemblyName, LanguageNames.CSharp, proj.Filepath));
                _projects.Add(proj.Id, proj);
            }

            foreach (var block in sln.ProjectBlocks)
            {
                unusedProjects.Remove(block.ProjectGuid);
                var proj = _projects[block.ProjectGuid];
                var project = _workspace.CurrentSolution.GetProject(proj.ProjectId);

                // documents
                var unusedDocuments = project.Documents.ToDictionary(d => d.FilePath, d => d.Id);
                foreach(var file in proj.SourceFiles)
                {
                    if(unusedDocuments.Remove(file))
                    {
                        continue;
                    }
                    _workspace.AddDocument(DocumentInfo.Create(
                        DocumentId.CreateNewId(project.Id),
                        Path.GetFileName(file),
                        null,
                        SourceCodeKind.Regular,
                        null,
                        file));
                }
                foreach(var unused in unusedDocuments)
                {
                    _workspace.RemoveDocument(unused.Value);
                }


                // project references
                var unusedProjectReferences = new HashSet<ProjectReference>(project.ProjectReferences);
                foreach(var reference in proj.ProjectReferences)
                {
                    var projectReference = new ProjectReference(_projects[reference.Item1].ProjectId);
                    unusedProjectReferences.Remove(projectReference);
                    _workspace.AddProjectReference(project.Id, projectReference);
                }
                foreach(var unused in unusedProjectReferences)
                {
                    _workspace.RemoveProjectReference(project.Id, unused);
                }


                // references 
                var unusedReferences = new HashSet<MetadataReference>(project.MetadataReferences);
                var resolver = new StupidReferenceResolver(Path.GetDirectoryName(proj.Filepath));
                foreach (var reference in proj.References)
                {
                    var assemblyPath = resolver.Resolve(reference.Item1, reference.Item2);
                    if (assemblyPath == null)
                    {
                        _logger.WriteWarning("FAILED to resolve assembly: " + reference.Item1);
                        continue;
                    }
                    var metadatReference = _referenceCache.GetMetadataReference(assemblyPath);
                    _workspace.AddMetadataReference(project.Id, metadatReference);
                    unusedReferences.Remove(metadatReference);
                }

                _logger.WriteVerbose(string.Format("Added project [{0}]", project.Name));
            }

            // removed projects
            foreach(var id in unusedProjects)
            {
                _workspace.RemoveProject(_projects[id].ProjectId);
                _projects.Remove(id);
            }
        }
    }
}