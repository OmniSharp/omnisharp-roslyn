using System;
using OmniSharp.MSBuild.Discovery;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.MSBuild.Tests
{
    public class MSBuildSelectionTests : AbstractTestFixture
    {
        public MSBuildSelectionTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public void RegisterDefaultInstanceFindsTheBestInstanceAvailable()
        {
            var msBuildInstances = new[]
            {
                GetInvalidMsBuildInstance(),
                // Valid
                new MSBuildInstance(
                    "Test Instance",
                    TestIO.GetRandomTempFolderPath(),
                    Version.Parse("16.3.2.3"),
                    DiscoveryType.VisualStudioSetup
                ).AddDotNetCoreToFakeInstance(),
            };

            var msbuildLocator = new MSFakeLocator(msBuildInstances);
            var logger = LoggerFactory.CreateLogger(nameof(RegisterDefaultInstanceFindsTheBestInstanceAvailable));

            // test
            msbuildLocator.RegisterDefaultInstance(logger, dotNetInfo: null);

            Assert.NotNull(msbuildLocator.RegisteredInstance);
            Assert.Same(msBuildInstances[1], msbuildLocator.RegisteredInstance);

            // clean up
            msbuildLocator.DeleteFakeInstancesFolders();
        }

        [Fact]
        public void RegisterDefaultInstanceFindsTheBestInstanceAvailableEvenWithOtherValidInstances()
        {
            var msBuildInstances = new[]
            {
                new MSBuildInstance(
                    "Valid Test Instance",
                    TestIO.GetRandomTempFolderPath(),
                    Version.Parse("16.3.2.3"),
                    DiscoveryType.VisualStudioSetup
                ),
                GetInvalidMsBuildInstance(),

                // Valid + Dotnet Core
                new MSBuildInstance(
                    "Another Valid Test Instance",
                    TestIO.GetRandomTempFolderPath(),
                    Version.Parse("16.3.2.1"),
                    DiscoveryType.VisualStudioSetup
                ).AddDotNetCoreToFakeInstance(),
            };

            var msbuildLocator = new MSFakeLocator(msBuildInstances);

            var logger = LoggerFactory.CreateLogger(
                nameof(RegisterDefaultInstanceFindsTheBestInstanceAvailableEvenWithOtherValidInstances)
            );

            // test
            msbuildLocator.RegisterDefaultInstance(logger, dotNetInfo: null);

            Assert.NotNull(msbuildLocator.RegisteredInstance);
            Assert.Same(msBuildInstances[2], msbuildLocator.RegisteredInstance);

            // clean up
            msbuildLocator.DeleteFakeInstancesFolders();
        }


        [Fact]
        public void RegisterDefaultInstanceFindsTheNewestInstanceAvailableEvenWithOtherValidInstances()
        {
            var msBuildInstances = new[]
            {
                new MSBuildInstance(
                    "Valid Test Instance",
                    TestIO.GetRandomTempFolderPath(),
                    Version.Parse("16.5.1.0"),
                    DiscoveryType.VisualStudioSetup
                ).AddDotNetCoreToFakeInstance(),
                GetInvalidMsBuildInstance(),

                // same but newer minor version
                new MSBuildInstance(
                    "Another Valid Test Instance",
                    TestIO.GetRandomTempFolderPath(),
                    Version.Parse("16.6.1.0"),
                    DiscoveryType.VisualStudioSetup
                ).AddDotNetCoreToFakeInstance(),
            };

            var msbuildLocator = new MSFakeLocator(msBuildInstances);

            var logger = LoggerFactory.CreateLogger(
                nameof(RegisterDefaultInstanceFindsTheBestInstanceAvailableEvenWithOtherValidInstances)
            );

            // test
            msbuildLocator.RegisterDefaultInstance(logger, dotNetInfo: null);

            Assert.NotNull(msbuildLocator.RegisteredInstance);
            Assert.Same(msBuildInstances[2], msbuildLocator.RegisteredInstance);

            // clean up
            msbuildLocator.DeleteFakeInstancesFolders();
        }

        [Fact]
        public void RegisterDefaultInstanceFindsUserOverrideAvailableEvenWithOtherValidInstances()
        {
            var msBuildInstances = new[]
            {
                new MSBuildInstance(
                    "Valid Test Instance",
                    TestIO.GetRandomTempFolderPath(),
                    Version.Parse("16.3.2.1"),
                    DiscoveryType.VisualStudioSetup
                ),
                GetInvalidMsBuildInstance(),

                // Valid + Dotnet Core
                new MSBuildInstance(
                    "Another Valid Test Instance",
                    TestIO.GetRandomTempFolderPath(),
                    Version.Parse("16.1.2.3"),
                    DiscoveryType.VisualStudioSetup
                ).AddDotNetCoreToFakeInstance(),

                // user override
                new MSBuildInstance(
                    "Manually Overridden",
                    TestIO.GetRandomTempFolderPath(),
                    Version.Parse("1.0.0"),
                    DiscoveryType.UserOverride
                ).AddDotNetCoreToFakeInstance(),
            };

            var msbuildLocator = new MSFakeLocator(msBuildInstances);

            var logger = LoggerFactory.CreateLogger(
                nameof(RegisterDefaultInstanceFindsUserOverrideAvailableEvenWithOtherValidInstances)
            );

            // test
            msbuildLocator.RegisterDefaultInstance(logger, dotNetInfo: null);

            Assert.NotNull(msbuildLocator.RegisteredInstance);
            Assert.Same(msBuildInstances[3], msbuildLocator.RegisteredInstance);

            // clean up
            msbuildLocator.DeleteFakeInstancesFolders();
        }

        private static MSBuildInstance GetInvalidMsBuildInstance()
        {
            return new MSBuildInstance(
                "Invalid Instance",
                TestIO.GetRandomTempFolderPath(),
                Version.Parse("15.0.4.2"),
                DiscoveryType.VisualStudioSetup
            );
        }
    }
}
