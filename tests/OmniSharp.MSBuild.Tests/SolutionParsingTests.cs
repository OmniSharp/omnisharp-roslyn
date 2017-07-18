using System;
using OmniSharp.MSBuild.SolutionParsing;
using Xunit;

namespace OmniSharp.MSBuild.Tests
{
    public class SolutionParsingTests
    {
        #region SimpleSolutionContent
        private const string SimpleSolutionContent = @"
            Microsoft Visual Studio Solution File, Format Version 9.00
            # Visual Studio 2005
            Project(""{F184B08F-C81C-45F6-A57F-5ABD9991F28F}"") = ""ConsoleApplication1"", ""ConsoleApplication1\ConsoleApplication1.vbproj"", ""{AB3413A6-D689-486D-B7F0-A095371B3F13}""
            EndProject
            Project(""{F184B08F-C81C-45F6-A57F-5ABD9991F28F}"") = ""vbClassLibrary"", ""vbClassLibrary\vbClassLibrary.vbproj"", ""{BA333A76-4511-47B8-8DF4-CA51C303AD0B}""
            EndProject
            Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""ClassLibrary1"", ""ClassLibrary1\ClassLibrary1.csproj"", ""{DEBCE986-61B9-435E-8018-44B9EF751655}""
            EndProject
            Global
                GlobalSection(SolutionConfigurationPlatforms) = preSolution
                    Debug|AnyCPU = Debug|AnyCPU
                    Release|AnyCPU = Release|AnyCPU
                EndGlobalSection
                GlobalSection(ProjectConfigurationPlatforms) = postSolution
                    {AB3413A6-D689-486D-B7F0-A095371B3F13}.Debug|AnyCPU.ActiveCfg = Debug|AnyCPU
                    {AB3413A6-D689-486D-B7F0-A095371B3F13}.Debug|AnyCPU.Build.0 = Debug|AnyCPU
                    {AB3413A6-D689-486D-B7F0-A095371B3F13}.Release|AnyCPU.ActiveCfg = Release|AnyCPU
                    {AB3413A6-D689-486D-B7F0-A095371B3F13}.Release|AnyCPU.Build.0 = Release|AnyCPU
                    {BA333A76-4511-47B8-8DF4-CA51C303AD0B}.Debug|AnyCPU.ActiveCfg = Debug|AnyCPU
                    {BA333A76-4511-47B8-8DF4-CA51C303AD0B}.Debug|AnyCPU.Build.0 = Debug|AnyCPU
                    {BA333A76-4511-47B8-8DF4-CA51C303AD0B}.Release|AnyCPU.ActiveCfg = Release|AnyCPU
                    {BA333A76-4511-47B8-8DF4-CA51C303AD0B}.Release|AnyCPU.Build.0 = Release|AnyCPU
                    {DEBCE986-61B9-435E-8018-44B9EF751655}.Debug|AnyCPU.ActiveCfg = Debug|AnyCPU
                    {DEBCE986-61B9-435E-8018-44B9EF751655}.Debug|AnyCPU.Build.0 = Debug|AnyCPU
                    {DEBCE986-61B9-435E-8018-44B9EF751655}.Release|AnyCPU.ActiveCfg = Release|AnyCPU
                    {DEBCE986-61B9-435E-8018-44B9EF751655}.Release|AnyCPU.Build.0 = Release|AnyCPU
                EndGlobalSection
                GlobalSection(SolutionProperties) = preSolution
                    HideSolutionNode = FALSE
                EndGlobalSection
            EndGlobal";
        #endregion
        #region SimpleSolutionWithDifferentSpacingContent
        private const string SimpleSolutionWithDifferentSpacingContent = @"
            Microsoft Visual Studio Solution File, Format Version 9.00
            # Visual Studio 2005
            Project("" { Project GUID} "")  = "" Project name "",  "" Relative path to project file ""    , "" {0ABED153-9451-483C-8140-9E8D7306B216} ""
            EndProject
            Global
                GlobalSection(SolutionConfigurationPlatforms) = preSolution
                    Debug|AnyCPU = Debug|AnyCPU
                    Release|AnyCPU = Release|AnyCPU
                EndGlobalSection
                GlobalSection(ProjectConfigurationPlatforms) = postSolution
                    {0ABED153-9451-483C-8140-9E8D7306B216}.Debug|AnyCPU.ActiveCfg = Debug|AnyCPU
                    {0ABED153-9451-483C-8140-9E8D7306B216}.Debug|AnyCPU.Build.0 = Debug|AnyCPU
                    {0ABED153-9451-483C-8140-9E8D7306B216}.Release|AnyCPU.ActiveCfg = Release|AnyCPU
                    {0ABED153-9451-483C-8140-9E8D7306B216}.Release|AnyCPU.Build.0 = Release|AnyCPU
                EndGlobalSection
                GlobalSection(SolutionProperties) = preSolution
                    HideSolutionNode = FALSE
                EndGlobalSection
            EndGlobal";
        #endregion
        #region UnitySolutionContent
        private const string UnitySolutionContent = @"
            Microsoft Visual Studio Solution File, Format Version 11.00
            # Visual Studio 2010
            Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""LeopotamGroupLibrary"", ""Assembly-CSharp.csproj"", ""{0279C7A5-B8B1-345F-ED42-A58232A100B3}""
            EndProject
            Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""LeopotamGroupLibrary"", ""Assembly-CSharp-firstpass.csproj"", ""{CD80764A-B5E2-C644-F0D0-A85E486306D8}""
            EndProject
            Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""LeopotamGroupLibrary"", ""Assembly-CSharp-Editor.csproj"", ""{BEDD06D2-DCFB-A6D5-CAC1-1320A679D62A}""
            EndProject
            Global
                GlobalSection(SolutionConfigurationPlatforms) = preSolution
                    Debug|Any CPU = Debug|Any CPU
                    Release|Any CPU = Release|Any CPU
                EndGlobalSection
                GlobalSection(ProjectConfigurationPlatforms) = postSolution
                    {0279C7A5-B8B1-345F-ED42-A58232A100B3}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                    {0279C7A5-B8B1-345F-ED42-A58232A100B3}.Debug|Any CPU.Build.0 = Debug|Any CPU
                    {0279C7A5-B8B1-345F-ED42-A58232A100B3}.Release|Any CPU.ActiveCfg = Release|Any CPU
                    {0279C7A5-B8B1-345F-ED42-A58232A100B3}.Release|Any CPU.Build.0 = Release|Any CPU
                    {CD80764A-B5E2-C644-F0D0-A85E486306D8}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                    {CD80764A-B5E2-C644-F0D0-A85E486306D8}.Debug|Any CPU.Build.0 = Debug|Any CPU
                    {CD80764A-B5E2-C644-F0D0-A85E486306D8}.Release|Any CPU.ActiveCfg = Release|Any CPU
                    {CD80764A-B5E2-C644-F0D0-A85E486306D8}.Release|Any CPU.Build.0 = Release|Any CPU
                    {BEDD06D2-DCFB-A6D5-CAC1-1320A679D62A}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                    {BEDD06D2-DCFB-A6D5-CAC1-1320A679D62A}.Debug|Any CPU.Build.0 = Debug|Any CPU
                    {BEDD06D2-DCFB-A6D5-CAC1-1320A679D62A}.Release|Any CPU.ActiveCfg = Release|Any CPU
                    {BEDD06D2-DCFB-A6D5-CAC1-1320A679D62A}.Release|Any CPU.Build.0 = Release|Any CPU
                EndGlobalSection
                GlobalSection(SolutionProperties) = preSolution
                    HideSolutionNode = FALSE
                EndGlobalSection
                GlobalSection(MonoDevelopProperties) = preSolution
                    StartupItem = Assembly-CSharp.csproj
                EndGlobalSection
            EndGlobal";
        #endregion
        #region SolutionWithProjectSectionContent
        private const string SolutionWithProjectSectionContent = @"
            Microsoft Visual Studio Solution File, Format Version 12.00
            # Visual Studio 15
            VisualStudioVersion = 15.0.26124.0
            MinimumVisualStudioVersion = 15.0.26124.0
            Project(""{2150E333-8FDC-42A3-9474-1A3956D46DE8}"") = ""src"", ""src"", ""{3C622F77-3C74-474E-AC38-7F30E9235F63}""
            EndProject
            Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""GG.Library.ConfigProvider.Vault"", ""src\GG.Library.ConfigProvider.Vault\GG.Library.ConfigProvider.Vault.csproj"", ""{1D50BF95-C9C0-4EF0-B869-0194684E8519}""
            EndProject
            Project(""{2150E333-8FDC-42A3-9474-1A3956D46DE8}"") = ""build"", ""build"", ""{4CFA9523-BC33-4C49-BF8E-554943CDC653}""
            ProjectSection(SolutionItems) = preProject
                build\Readme.txt = build\Readme.txt
            EndProjectSection
            EndProject
            Project(""{2150E333-8FDC-42A3-9474-1A3956D46DE8}"") = ""test"", ""test"", ""{65E7B2FA-C1D0-411C-82D7-0DF418A16555}""
            EndProject
            Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""GG.Library.ConfigProvider.Vault.Test"", ""test\GG.Library.ConfigProvider.Vault.Test\GG.Library.ConfigProvider.Vault.Test.csproj"", ""{0C768495-EA52-4703-AEAF-316A4C0A01CB}""
            EndProject
            Global
                GlobalSection(SolutionConfigurationPlatforms) = preSolution
                    Debug|Any CPU = Debug|Any CPU
                    Debug|x64 = Debug|x64
                    Debug|x86 = Debug|x86
                    Release|Any CPU = Release|Any CPU
                    Release|x64 = Release|x64
                    Release|x86 = Release|x86
                EndGlobalSection
                GlobalSection(SolutionProperties) = preSolution
                    HideSolutionNode = FALSE
                EndGlobalSection
                GlobalSection(ProjectConfigurationPlatforms) = postSolution
                    {1D50BF95-C9C0-4EF0-B869-0194684E8519}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                    {1D50BF95-C9C0-4EF0-B869-0194684E8519}.Debug|Any CPU.Build.0 = Debug|Any CPU
                    {1D50BF95-C9C0-4EF0-B869-0194684E8519}.Debug|x64.ActiveCfg = Debug|x64
                    {1D50BF95-C9C0-4EF0-B869-0194684E8519}.Debug|x64.Build.0 = Debug|x64
                    {1D50BF95-C9C0-4EF0-B869-0194684E8519}.Debug|x86.ActiveCfg = Debug|x86
                    {1D50BF95-C9C0-4EF0-B869-0194684E8519}.Debug|x86.Build.0 = Debug|x86
                    {1D50BF95-C9C0-4EF0-B869-0194684E8519}.Release|Any CPU.ActiveCfg = Release|Any CPU
                    {1D50BF95-C9C0-4EF0-B869-0194684E8519}.Release|Any CPU.Build.0 = Release|Any CPU
                    {1D50BF95-C9C0-4EF0-B869-0194684E8519}.Release|x64.ActiveCfg = Release|x64
                    {1D50BF95-C9C0-4EF0-B869-0194684E8519}.Release|x64.Build.0 = Release|x64
                    {1D50BF95-C9C0-4EF0-B869-0194684E8519}.Release|x86.ActiveCfg = Release|x86
                    {1D50BF95-C9C0-4EF0-B869-0194684E8519}.Release|x86.Build.0 = Release|x86
                    {0C768495-EA52-4703-AEAF-316A4C0A01CB}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                    {0C768495-EA52-4703-AEAF-316A4C0A01CB}.Debug|Any CPU.Build.0 = Debug|Any CPU
                    {0C768495-EA52-4703-AEAF-316A4C0A01CB}.Debug|x64.ActiveCfg = Debug|x64
                    {0C768495-EA52-4703-AEAF-316A4C0A01CB}.Debug|x64.Build.0 = Debug|x64
                    {0C768495-EA52-4703-AEAF-316A4C0A01CB}.Debug|x86.ActiveCfg = Debug|x86
                    {0C768495-EA52-4703-AEAF-316A4C0A01CB}.Debug|x86.Build.0 = Debug|x86
                    {0C768495-EA52-4703-AEAF-316A4C0A01CB}.Release|Any CPU.ActiveCfg = Release|Any CPU
                    {0C768495-EA52-4703-AEAF-316A4C0A01CB}.Release|Any CPU.Build.0 = Release|Any CPU
                    {0C768495-EA52-4703-AEAF-316A4C0A01CB}.Release|x64.ActiveCfg = Release|x64
                    {0C768495-EA52-4703-AEAF-316A4C0A01CB}.Release|x64.Build.0 = Release|x64
                    {0C768495-EA52-4703-AEAF-316A4C0A01CB}.Release|x86.ActiveCfg = Release|x86
                    {0C768495-EA52-4703-AEAF-316A4C0A01CB}.Release|x86.Build.0 = Release|x86
                EndGlobalSection
                GlobalSection(NestedProjects) = preSolution
                    {1D50BF95-C9C0-4EF0-B869-0194684E8519} = {3C622F77-3C74-474E-AC38-7F30E9235F63}
                    {0C768495-EA52-4703-AEAF-316A4C0A01CB} = {65E7B2FA-C1D0-411C-82D7-0DF418A16555}
                EndGlobalSection
            EndGlobal";
        #endregion

        [Fact]
        public void SolutionFile_Parse_throws_with_null_text()
        {
            Assert.Throws<ArgumentNullException>(() => SolutionFile.Parse(null));
        }

        [Fact]
        public void SolutionFile_Parse_simple_solution()
        {
            var solution = SolutionFile.Parse(SimpleSolutionContent);

            Assert.NotNull(solution.FormatVersion);
            Assert.Equal(new Version("9.00"), solution.FormatVersion);

            Assert.Null(solution.VisualStudioVersion);

            Assert.Equal(3, solution.Projects.Length);

            Assert.Equal("{F184B08F-C81C-45F6-A57F-5ABD9991F28F}", solution.Projects[0].ProjectTypeGuid);
            Assert.Equal("ConsoleApplication1", solution.Projects[0].ProjectName);
            Assert.Equal(@"ConsoleApplication1\ConsoleApplication1.vbproj", solution.Projects[0].RelativePath);
            Assert.Equal("{AB3413A6-D689-486D-B7F0-A095371B3F13}", solution.Projects[0].ProjectGuid);

            Assert.Equal("{F184B08F-C81C-45F6-A57F-5ABD9991F28F}", solution.Projects[1].ProjectTypeGuid);
            Assert.Equal("vbClassLibrary", solution.Projects[1].ProjectName);
            Assert.Equal(@"vbClassLibrary\vbClassLibrary.vbproj", solution.Projects[1].RelativePath);
            Assert.Equal("{BA333A76-4511-47B8-8DF4-CA51C303AD0B}", solution.Projects[1].ProjectGuid);

            Assert.Equal("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", solution.Projects[2].ProjectTypeGuid);
            Assert.Equal("ClassLibrary1", solution.Projects[2].ProjectName);
            Assert.Equal(@"ClassLibrary1\ClassLibrary1.csproj", solution.Projects[2].RelativePath);
            Assert.Equal("{DEBCE986-61B9-435E-8018-44B9EF751655}", solution.Projects[2].ProjectGuid);

            Assert.Equal(3, solution.GlobalSections.Length);
            Assert.Equal("SolutionConfigurationPlatforms", solution.GlobalSections[0].Name);
            Assert.Equal("ProjectConfigurationPlatforms", solution.GlobalSections[1].Name);
            Assert.Equal("SolutionProperties", solution.GlobalSections[2].Name);
        }

        [Fact]
        public void SolutionFile_Parse_simple_solution_with_different_spacing()
        {
            var solution = SolutionFile.Parse(SimpleSolutionWithDifferentSpacingContent);

            Assert.NotNull(solution.FormatVersion);
            Assert.Equal(new Version("9.00"), solution.FormatVersion);

            Assert.Null(solution.VisualStudioVersion);

            Assert.Equal(1, solution.Projects.Length);

            Assert.Equal("{ Project GUID}", solution.Projects[0].ProjectTypeGuid);
            Assert.Equal("Project name", solution.Projects[0].ProjectName);
            Assert.Equal("Relative path to project file", solution.Projects[0].RelativePath);
            Assert.Equal("{0ABED153-9451-483C-8140-9E8D7306B216}", solution.Projects[0].ProjectGuid);
        }

        [Fact]
        public void SolutionFile_Parse_unity_solution()
        {
            var solution = SolutionFile.Parse(UnitySolutionContent);

            Assert.NotNull(solution.FormatVersion);
            Assert.Equal(new Version("11.00"), solution.FormatVersion);

            Assert.Null(solution.VisualStudioVersion);

            Assert.Equal(3, solution.Projects.Length);

            Assert.Equal("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", solution.Projects[0].ProjectTypeGuid);
            Assert.Equal("LeopotamGroupLibrary", solution.Projects[0].ProjectName);
            Assert.Equal("Assembly-CSharp.csproj", solution.Projects[0].RelativePath);
            Assert.Equal("{0279C7A5-B8B1-345F-ED42-A58232A100B3}", solution.Projects[0].ProjectGuid);

            Assert.Equal("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", solution.Projects[1].ProjectTypeGuid);
            Assert.Equal("LeopotamGroupLibrary", solution.Projects[1].ProjectName);
            Assert.Equal("Assembly-CSharp-firstpass.csproj", solution.Projects[1].RelativePath);
            Assert.Equal("{CD80764A-B5E2-C644-F0D0-A85E486306D8}", solution.Projects[1].ProjectGuid);

            Assert.Equal("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", solution.Projects[2].ProjectTypeGuid);
            Assert.Equal("LeopotamGroupLibrary", solution.Projects[2].ProjectName);
            Assert.Equal("Assembly-CSharp-Editor.csproj", solution.Projects[2].RelativePath);
            Assert.Equal("{BEDD06D2-DCFB-A6D5-CAC1-1320A679D62A}", solution.Projects[2].ProjectGuid);

            Assert.Equal(4, solution.GlobalSections.Length);
            Assert.Equal("SolutionConfigurationPlatforms", solution.GlobalSections[0].Name);
            Assert.Equal("ProjectConfigurationPlatforms", solution.GlobalSections[1].Name);
            Assert.Equal("SolutionProperties", solution.GlobalSections[2].Name);
            Assert.Equal("MonoDevelopProperties", solution.GlobalSections[3].Name);
        }

        [Fact]
        public void SolutionFile_Parse_solution_with_project_section()
        {
            var solution = SolutionFile.Parse(SolutionWithProjectSectionContent);

            Assert.NotNull(solution.FormatVersion);
            Assert.Equal(new Version("12.00"), solution.FormatVersion);

            Assert.NotNull(solution.VisualStudioVersion);
            Assert.Equal(new Version("15.0.26124.0"), solution.VisualStudioVersion);

            Assert.Equal(5, solution.Projects.Length);

            Assert.Equal("{2150E333-8FDC-42A3-9474-1A3956D46DE8}", solution.Projects[0].ProjectTypeGuid);
            Assert.Equal("src", solution.Projects[0].ProjectName);
            Assert.Equal("src", solution.Projects[0].RelativePath);
            Assert.Equal("{3C622F77-3C74-474E-AC38-7F30E9235F63}", solution.Projects[0].ProjectGuid);

            Assert.Equal("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", solution.Projects[1].ProjectTypeGuid);
            Assert.Equal("GG.Library.ConfigProvider.Vault", solution.Projects[1].ProjectName);
            Assert.Equal(@"src\GG.Library.ConfigProvider.Vault\GG.Library.ConfigProvider.Vault.csproj", solution.Projects[1].RelativePath);
            Assert.Equal("{1D50BF95-C9C0-4EF0-B869-0194684E8519}", solution.Projects[1].ProjectGuid);

            Assert.Equal("{2150E333-8FDC-42A3-9474-1A3956D46DE8}", solution.Projects[2].ProjectTypeGuid);
            Assert.Equal("build", solution.Projects[2].ProjectName);
            Assert.Equal("build", solution.Projects[2].RelativePath);
            Assert.Equal("{4CFA9523-BC33-4C49-BF8E-554943CDC653}", solution.Projects[2].ProjectGuid);

            Assert.Equal("{2150E333-8FDC-42A3-9474-1A3956D46DE8}", solution.Projects[3].ProjectTypeGuid);
            Assert.Equal("test", solution.Projects[3].ProjectName);
            Assert.Equal("test", solution.Projects[3].RelativePath);
            Assert.Equal("{65E7B2FA-C1D0-411C-82D7-0DF418A16555}", solution.Projects[3].ProjectGuid);

            Assert.Equal("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", solution.Projects[4].ProjectTypeGuid);
            Assert.Equal("GG.Library.ConfigProvider.Vault.Test", solution.Projects[4].ProjectName);
            Assert.Equal(@"test\GG.Library.ConfigProvider.Vault.Test\GG.Library.ConfigProvider.Vault.Test.csproj", solution.Projects[4].RelativePath);
            Assert.Equal("{0C768495-EA52-4703-AEAF-316A4C0A01CB}", solution.Projects[4].ProjectGuid);

            Assert.Equal(4, solution.GlobalSections.Length);
            Assert.Equal("SolutionConfigurationPlatforms", solution.GlobalSections[0].Name);
            Assert.Equal("SolutionProperties", solution.GlobalSections[1].Name);
            Assert.Equal("ProjectConfigurationPlatforms", solution.GlobalSections[2].Name);
            Assert.Equal("NestedProjects", solution.GlobalSections[3].Name);
        }
    }
}
