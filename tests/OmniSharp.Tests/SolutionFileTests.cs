using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.MSBuild;
using OmniSharp.MSBuild;
using Xunit;

namespace OmniSharp.Tests
{
    public class SolutionFileTests
    {
        [Fact]
        public void Can_load_unity_solution_file()
        {
            var solutionFileText = 
@"Microsoft Visual Studio Solution File, Format Version 11.00
# Visual Studio 2008

Project(""{D02EBBA0-86EB-60B6-155B-94E12649FF84}"") = ""asteroids"", ""Assembly-CSharp-vs.csproj"", ""{3EA5169F-7459-63CD-8E5C-1E28FF4F7D84}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{3EA5169F-7459-63CD-8E5C-1E28FF4F7D84}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{3EA5169F-7459-63CD-8E5C-1E28FF4F7D84}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{3EA5169F-7459-63CD-8E5C-1E28FF4F7D84}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{3EA5169F-7459-63CD-8E5C-1E28FF4F7D84}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
									GlobalSection(MonoDevelopProperties) = preSolution
		StartupItem = Assembly-CSharp.csproj
		Policies = $0
		$0.TextStylePolicy = $1
		$1.FileWidth = 120
		$1.NoTabsAfterNonTabs = True
		$1.EolMarker = Unix
		$1.inheritsSet = VisualStudio
		$1.inheritsScope = text/plain
		$1.scope = text/x-csharp
		$0.CSharpFormattingPolicy = $2
		$2.inheritsSet = Mono
		$2.inheritsScope = text/x-csharp
		$2.scope = text/x-csharp
		$0.TextStylePolicy = $3
		$3.FileWidth = 120
		$3.TabWidth = 4
		$3.EolMarker = Unix
		$3.inheritsSet = Mono
		$3.inheritsScope = text/plain
		$3.scope = text/plain
	EndGlobalSection

EndGlobal
";
            SolutionFile solutionFile;
            using (TextReader sr = new StringReader(solutionFileText))
            {
                solutionFile = SolutionFile.Parse(sr);
            }

            Assert.Equal(1, solutionFile.ProjectBlocks.Count());
        }

        [Fact]
        public void Can_get_unity_project_type_guid()
        {
            var guid = MSBuildProjectSystem.UnityTypeGuid("asteroids");
            Assert.Equal(Guid.Parse("D02EBBA0-86EB-60B6-155B-94E12649FF84"), guid);
        }
    }
}