using System;
using System.Collections.Generic;
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
        public Dictionary<string, string> ConfigurationsInSolution { get; set; }
    }
}
