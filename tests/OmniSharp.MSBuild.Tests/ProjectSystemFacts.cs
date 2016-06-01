using Xunit;
using TestCommon;
using OmniSharp.MSBuild.ProjectFile;
using OmniSharp.Options;
using System.IO;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using OmniSharp.Models;

namespace OmniSharp.MSBuild.Tests
{
    public class ProjectSystemFacts
    {
        // [Fact]
        public void OpenSimpleConsoleApp()
        {
            var testProject = TestsContext.Default.GetTestSample("CsProjectSample01");
            var option = new MSBuildOptions();
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddConsole();
            var logger = loggerFactory.CreateLogger("OpenSimpleConsoleApp.Test");
            var diagnstics = new List<MSBuildDiagnosticsMessage>();
            var project = ProjectFileInfo.Create(
                option,
                logger,
                testProject,
                Path.Combine(testProject, "ConsoleApplication1", "ConsoleApplication1.csproj"),
                diagnstics);

            // var projectSystem = new MSBuildProjectSystem();
            Assert.NotNull(project);
        }
    }
}