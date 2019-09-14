using System;
using Microsoft.CodeAnalysis;

namespace OmniSharp.MSBuild
{
    public class ProjectIdInfo
    {
        public ProjectIdInfo(ProjectId id, bool isDefinedInSolution)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            IsDefinedInSolution = isDefinedInSolution;
        }

        public ProjectId Id { get; set; }
        public bool IsDefinedInSolution { get; set; }
    }
}
