using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OmniSharp.MSBuild.Discovery;
using OmniSharp.MSBuild.ProjectFile;
using OmniSharp.Options;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.MSBuild.Tests
{
    public class ProjectFileInfoTests : AbstractTestFixture
    {
        private readonly TestAssets _testAssets;

        public ProjectFileInfoTests(ITestOutputHelper output)
            : base(output)
        {
            this._testAssets = TestAssets.Instance;
        }

        private ProjectFileInfo CreateProjectFileInfo(OmniSharpTestHost host, ITestProject testProject, string projectFilePath)
        {
            var msbuildLocator = host.GetExport<IMSBuildLocator>();
            var sdksPathResolver = host.GetExport<SdksPathResolver>();

            var loader = new ProjectLoader(
                options: new MSBuildOptions(),
                solutionDirectory: testProject.Directory,
                propertyOverrides: msbuildLocator.RegisteredInstance.PropertyOverrides,
                loggerFactory: LoggerFactory,
                sdksPathResolver: sdksPathResolver);

            var projectIdInfo = new ProjectIdInfo(ProjectId.CreateNewId(), false);
            var (projectFileInfo, _, _) = ProjectFileInfo.Load(projectFilePath, projectIdInfo, loader);

            return projectFileInfo;
        }

        [Fact]
        public async Task HelloWorld_has_correct_property_values()
        {
            using (var host = CreateOmniSharpHost())
            using (var testProject = await _testAssets.GetTestProjectAsync("HelloWorld"))
            {
                var projectFilePath = Path.Combine(testProject.Directory, "HelloWorld.csproj");

                var projectFileInfo = CreateProjectFileInfo(host, testProject, projectFilePath);

                Assert.NotNull(projectFileInfo);
                Assert.Equal(projectFilePath, projectFileInfo.FilePath);
                var targetFramework = Assert.Single(projectFileInfo.TargetFrameworks);
                Assert.Equal("netcoreapp2.1", targetFramework);
                Assert.Equal("bin/Debug/netcoreapp2.1/", projectFileInfo.OutputPath.EnsureForwardSlashes());
                Assert.Equal("obj/Debug/netcoreapp2.1/", projectFileInfo.IntermediateOutputPath.EnsureForwardSlashes());
                Assert.Equal(3, projectFileInfo.SourceFiles.Length); // Program.cs, AssemblyInfo.cs, AssemblyAttributes.cs
                Assert.Equal(LanguageVersion.CSharp7_1, projectFileInfo.LanguageVersion);
                Assert.True(projectFileInfo.TreatWarningsAsErrors);
                Assert.Equal("Debug", projectFileInfo.Configuration);
                Assert.Equal("AnyCPU", projectFileInfo.Platform);

                var compilationOptions = projectFileInfo.CreateCompilationOptions();
                Assert.Equal(ReportDiagnostic.Error, compilationOptions.GeneralDiagnosticOption);
                Assert.True(compilationOptions.CheckOverflow);
            }
        }

        [Fact]
        public async Task HelloWorldSlim_has_correct_property_values()
        {
            using (var host = CreateOmniSharpHost())
            using (var testProject = await _testAssets.GetTestProjectAsync("HelloWorldSlim"))
            {
                var projectFilePath = Path.Combine(testProject.Directory, "HelloWorldSlim.csproj");

                var projectFileInfo = CreateProjectFileInfo(host, testProject, projectFilePath);

                Assert.NotNull(projectFileInfo);
                Assert.Equal(projectFilePath, projectFileInfo.FilePath);
                var targetFramework = Assert.Single(projectFileInfo.TargetFrameworks);
                Assert.Equal("netcoreapp2.1", targetFramework);
                Assert.Equal("bin/Debug/netcoreapp2.1/", projectFileInfo.OutputPath.EnsureForwardSlashes());
                Assert.Equal("obj/Debug/netcoreapp2.1/", projectFileInfo.IntermediateOutputPath.EnsureForwardSlashes());
                Assert.Equal(3, projectFileInfo.SourceFiles.Length); // Program.cs, AssemblyInfo.cs, AssemblyAttributes.cs
                Assert.Equal("Debug", projectFileInfo.Configuration);
                Assert.Equal("AnyCPU", projectFileInfo.Platform);

                var compilationOptions = projectFileInfo.CreateCompilationOptions();
                Assert.Equal(ReportDiagnostic.Default, compilationOptions.GeneralDiagnosticOption);
                Assert.False(compilationOptions.CheckOverflow);
            }
        }

        [Fact]
        public async Task NetStandardAndNetCoreApp_has_correct_property_values()
        {
            using (var host = CreateOmniSharpHost())
            using (var testProject = await _testAssets.GetTestProjectAsync("NetStandardAndNetCoreApp"))
            {
                var projectFilePath = Path.Combine(testProject.Directory, "NetStandardAndNetCoreApp.csproj");

                var projectFileInfo = CreateProjectFileInfo(host, testProject, projectFilePath);

                Assert.NotNull(projectFileInfo);
                Assert.Equal(projectFilePath, projectFileInfo.FilePath);
                Assert.Equal(2, projectFileInfo.TargetFrameworks.Length);
                Assert.Equal("netcoreapp2.1", projectFileInfo.TargetFrameworks[0]);
                Assert.Equal("netstandard1.5", projectFileInfo.TargetFrameworks[1]);
                Assert.Equal("bin/Debug/netcoreapp2.1/", projectFileInfo.OutputPath.EnsureForwardSlashes());
                Assert.Equal("obj/Debug/netcoreapp2.1/", projectFileInfo.IntermediateOutputPath.EnsureForwardSlashes());
                Assert.Equal(3, projectFileInfo.SourceFiles.Length); // Program.cs, AssemblyInfo.cs, AssemblyAttributes.cs
                Assert.Equal("Debug", projectFileInfo.Configuration);
                Assert.Equal("AnyCPU", projectFileInfo.Platform);
            }
        }

        [Fact]
        public async Task CSharp8AndNullableContext_has_correct_property_values()
        {
            using (var host = CreateOmniSharpHost())
            using (var testProject = await _testAssets.GetTestProjectAsync("CSharp8AndNullableContext"))
            {
                var projectFilePath = Path.Combine(testProject.Directory, "CSharp8AndNullableContext.csproj");

                var projectFileInfo = CreateProjectFileInfo(host, testProject, projectFilePath);

                Assert.NotNull(projectFileInfo);
                Assert.Equal(projectFilePath, projectFileInfo.FilePath);
                var targetFramework = Assert.Single(projectFileInfo.TargetFrameworks);
                Assert.Equal("netcoreapp3.1", targetFramework);
                Assert.Equal(LanguageVersion.CSharp8, projectFileInfo.LanguageVersion);
                Assert.Equal(NullableContextOptions.Enable, projectFileInfo.NullableContextOptions);
                Assert.Equal("Debug", projectFileInfo.Configuration);
                Assert.Equal("AnyCPU", projectFileInfo.Platform);

                var compilationOptions = projectFileInfo.CreateCompilationOptions();
                Assert.Equal(NullableContextOptions.Enable, compilationOptions.NullableContextOptions);
            }
        }

        [Fact]
        public async Task ExternAliasWithReference()
        {
            using (var host = CreateOmniSharpHost())
            using (var testProject = await _testAssets.GetTestProjectAsync("ExternAlias"))
            {
                var projectFilePath = Path.Combine(testProject.Directory, "ExternAlias.App", "ExternAlias.App.csproj");
                var projectFileInfo = CreateProjectFileInfo(host, testProject, projectFilePath);
                Assert.Single(projectFileInfo.ReferenceAliases);
                foreach(var kv in projectFileInfo.ReferenceAliases)
                {
                    TestOutput.WriteLine($"{kv.Key} = {kv.Value}");
                }

                var libpath = Path.Combine(testProject.Directory, "ExternAlias.Lib", "bin", "Debug", "netstandard2.0", "ExternAlias.Lib.dll");
                Assert.True(projectFileInfo.ReferenceAliases.ContainsKey(libpath));
                Assert.Equal("abc", projectFileInfo.ReferenceAliases[libpath]);
            }
        }

        [Fact]
        public async Task ExternAliasWithProjectReference()
        {
            using (var host = CreateOmniSharpHost())
            using (var testProject = await _testAssets.GetTestProjectAsync("ExternAlias"))
            {
                var projectFilePath = Path.Combine(testProject.Directory, "ExternAlias.App2", "ExternAlias.App2.csproj");
                var projectFileInfo = CreateProjectFileInfo(host, testProject, projectFilePath);
                Assert.Single(projectFileInfo.ProjectReferenceAliases);
                foreach(var kv in projectFileInfo.ProjectReferenceAliases)
                {
                    TestOutput.WriteLine($"{kv.Key} = {kv.Value}");
                }

                var projectReferencePath = Path.Combine(testProject.Directory, "ExternAlias.Lib", "ExternAlias.Lib.csproj");
                Assert.True(projectFileInfo.ProjectReferenceAliases.ContainsKey(projectReferencePath));
                Assert.Equal("abc", projectFileInfo.ProjectReferenceAliases[projectReferencePath]);
            }
        }

        [Fact]
        public async Task AllowUnsafe()
        {
            using (var host = CreateOmniSharpHost())
            using (var testProject = await _testAssets.GetTestProjectAsync("AllowUnsafe"))
            {
                var projectFilePath = Path.Combine(testProject.Directory, "AllowUnsafe.csproj");
                var projectFileInfo = CreateProjectFileInfo(host, testProject, projectFilePath);
                Assert.True(projectFileInfo.AllowUnsafeCode);

                var compilationOptions = projectFileInfo.CreateCompilationOptions();
                Assert.True(compilationOptions.AllowUnsafe);
                Assert.Equal(ReportDiagnostic.Default, compilationOptions.GeneralDiagnosticOption);
            }
        }

        [Fact]
        public async Task WarningsAsErrors()
        {
            using (var host = CreateOmniSharpHost())
            using (var testProject = await _testAssets.GetTestProjectAsync("WarningsAsErrors"))
            {
                var projectFilePath = Path.Combine(testProject.Directory, "WarningsAsErrors.csproj");
                var projectFileInfo = CreateProjectFileInfo(host, testProject, projectFilePath);
                Assert.NotEmpty(projectFileInfo.WarningsAsErrors);
                Assert.Contains("CS1998", projectFileInfo.WarningsAsErrors);
                Assert.Contains("CS7080", projectFileInfo.WarningsAsErrors);
                Assert.Contains("CS7081", projectFileInfo.WarningsAsErrors);

                Assert.NotEmpty(projectFileInfo.WarningsNotAsErrors);
                Assert.Contains("CS7080", projectFileInfo.WarningsNotAsErrors);
                Assert.Contains("CS7082", projectFileInfo.WarningsNotAsErrors);

                var compilationOptions = projectFileInfo.CreateCompilationOptions();
                Assert.True(compilationOptions.SpecificDiagnosticOptions.ContainsKey("CS1998"), "Specific diagnostic option for CS1998 not found");
                Assert.True(compilationOptions.SpecificDiagnosticOptions.ContainsKey("CS7080"), "Specific diagnostic option for CS7080 not found");
                Assert.True(compilationOptions.SpecificDiagnosticOptions.ContainsKey("CS7081"), "Specific diagnostic option for CS7081 not found");
                Assert.True(compilationOptions.SpecificDiagnosticOptions.ContainsKey("CS7082"), "Specific diagnostic option for CS7082 not found");
                Assert.Equal(ReportDiagnostic.Error, compilationOptions.SpecificDiagnosticOptions["CS1998"]);
                // CS7080 is both in WarningsAsErrors and WarningsNotAsErrors, but WarningsNotAsErrors are higher priority
                Assert.Equal(ReportDiagnostic.Warn, compilationOptions.SpecificDiagnosticOptions["CS7080"]); 
                Assert.Equal(ReportDiagnostic.Warn, compilationOptions.SpecificDiagnosticOptions["CS7082"]);
                // CS7081 is both WarningsAsErrors and NoWarn, but NoWarn are higher priority
                Assert.Equal(ReportDiagnostic.Suppress, compilationOptions.SpecificDiagnosticOptions["CS7081"]); 
            }
        }
    }
}
