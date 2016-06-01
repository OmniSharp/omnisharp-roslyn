using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OmniSharp.Models;
using OmniSharp.MSBuild.ProjectFile;
using OmniSharp.Options;
using TestCommon;

namespace MSBuildTestDriver
{
    public class MSBuildTestProgram
    {
        public static void Main(string[] args)
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddConsole();
            var logger = loggerFactory.CreateLogger("MSBuild Test Driver");

            var testProject = TestsContext.Default.GetTestSample("CsProjectSample01");
            var option = new MSBuildOptions();
            var diagnstics = new List<MSBuildDiagnosticsMessage>();
            var project = ProjectFileInfo.Create(
                option,
                logger,
                testProject,
                System.IO.Path.Combine(testProject, "ConsoleApplication1", "ConsoleApplication1.csproj"),
                diagnstics);
        }
    }
}