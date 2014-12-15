using System;
using System.Collections.Generic;

namespace OmniSharp.MSBuild.ProjectFile
{
    public class ProjectFileInfo
    {

        public Guid ProjectId { get; private set; }

        public string Name { get; private set; }

        public string Path { get; private set; }

        public string AssemblyName { get; private set; }

        public IEnumerable<string> SourceFiles { get; private set; }

        public IEnumerable<string> References { get; private set; }


        internal ProjectFileInfo(Guid projectId, string name, string path, string assemblyName, IEnumerable<string> sourceFiles, IEnumerable<string> references)
        {
            ProjectId = projectId;
            Name = name;
            Path = path;
            AssemblyName = assemblyName;
            SourceFiles = sourceFiles;
            References = references;
        }
    }
}