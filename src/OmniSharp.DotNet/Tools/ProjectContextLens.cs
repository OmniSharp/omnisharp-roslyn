﻿using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Graph;

namespace OmniSharp.DotNet.Tools
{
    public class ProjectContextLens
    {
        private readonly string _configuration;
        private readonly ProjectContext _context;

        private List<string> _sourceFiles = new List<string>();
        private List<string> _fileReferences = new List<string>();
        private List<ProjectDescription> _projectReferenes = new List<ProjectDescription>();

        public ProjectContextLens(ProjectContext context, string configuration)
        {
            _context = context;
            _configuration = configuration;
            Resolve();
        }

        public IEnumerable<string> SourceFiles
        {
            get { return _sourceFiles; }
        }

        public IEnumerable<string> FileReferences
        {
            get { return _fileReferences; }
        }

        public IEnumerable<ProjectDescription> ProjectReferences
        {
            get { return _projectReferenes; }
        }

        private void Resolve()
        {
            _sourceFiles.AddRange(_context.ProjectFile.Files.SourceFiles);
            var exporter = _context.CreateExporter(_configuration);

            foreach (var export in exporter.GetAllExports())
            {
                ResolveFileReferences(export);
                ResolveProjectReference(export);
            }
        }

        private void ResolveFileReferences(LibraryExport export)
        {
            if (export.Library.Identity.Type != LibraryType.Project)
            {
                _fileReferences.AddRange(export.CompilationAssemblies.Select(asset => asset.ResolvedPath));
            }
        }

        private void ResolveProjectReference(LibraryExport export)
        {
            var desc = export.Library as ProjectDescription;
            if (desc == null || export.Library.Identity.Type != LibraryType.Project)
            {
                return;
            }

            if (export.Library.Identity.Name == _context.ProjectFile.Name)
            {
                return;
            }

            if (!string.IsNullOrEmpty(desc?.TargetFrameworkInfo?.AssemblyPath))
            {
                return;
            }

            _sourceFiles.AddRange(export.SourceReferences);
            _projectReferenes.Add(desc);
        }
    }
}
