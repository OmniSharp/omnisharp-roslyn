using OmniSharp.MSBuild;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OmniSharp.Models
{
	public class MsBuildWorkspaceInformation
	{
		public MsBuildWorkspaceInformation(MSBuildContext msbuildContext)
		{
			SolutionPath = msbuildContext.SolutionPath;

			Projects = msbuildContext.Projects.Values.Select(p => new MSBuildProject(p));
		}

		public string SolutionPath { get; }
		public IEnumerable<MSBuildProject> Projects { get; }
	}
}