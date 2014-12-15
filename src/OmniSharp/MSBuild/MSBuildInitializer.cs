using Microsoft.Framework.Logging;
using OmniSharp.Services;
using Microsoft.CodeAnalysis;
using System.IO;
using Microsoft.CodeAnalysis.MSBuild;
using System.Linq;

namespace OmniSharp.MSBuild
{

    public class MSBuildInitializer : IWorkspaceInitializer
    {
        private readonly OmnisharpWorkspace _workspace;
        private readonly IReferenceResolver _resolver;
        private readonly IOmnisharpEnvironment _env;
        private readonly ILogger _logger;

        public MSBuildInitializer(OmnisharpWorkspace workspace,
                                  IOmnisharpEnvironment env,
                                  ILoggerFactory loggerFactory)
        {
            _workspace = workspace;
            _resolver = new StupidReferenceResolver();
            _env = env;
            _logger = loggerFactory.Create<MSBuildInitializer>();
        }

        public void Initalize()
        {

            if(string.IsNullOrWhiteSpace(_env.SolutionFilePath))
            {
                throw new System.Exception("Missing sln-file");
            }

            var sln = SolutionFile.Parse(new StringReader(File.ReadAllText(_env.SolutionFilePath)));
            var slnDirectory = Path.GetDirectoryName(_env.SolutionFilePath);

            foreach (var block in sln.ProjectBlocks)
            {
                _logger.WriteInformation(string.Format("loading project from {0}", Path.Combine(slnDirectory, block.ProjectPath)));

                var proj = new ProjectFile.ProjectFile(block.ProjectGuid, block.ProjectName, Path.Combine(slnDirectory, block.ProjectPath)).Info;
                var projectInfo = ProjectInfo.Create(ProjectId.CreateNewId(proj.Name), VersionStamp.Create(),
                    proj.Name, proj.AssemblyName, LanguageNames.CSharp, proj.Path);

                // documents
                var docInfos = proj.SourceFiles.Select(file => DocumentInfo.Create(DocumentId.CreateNewId(projectInfo.Id), 
                    Path.GetFileName(file), 
                    null, 
                    SourceCodeKind.Regular, 
                    null, 
                    Path.Combine(Path.GetDirectoryName(proj.Path), file)));
                projectInfo = projectInfo.WithDocuments(docInfos);

                // metadata references
                var references = proj.References.Select(reference => _resolver.Resolve(reference));
                projectInfo = projectInfo.WithMetadataReferences(references);

                _workspace.AddProject(projectInfo);

                _logger.WriteVerbose(string.Format("Added project [{0}]", projectInfo.Name));
            }
        }
    }
}