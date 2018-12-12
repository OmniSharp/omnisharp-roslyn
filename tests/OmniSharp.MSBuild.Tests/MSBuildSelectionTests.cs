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
                    Version.Parse("15.1.2.3"),
                    DiscoveryType.VisualStudioSetup
                ).AddDotNetCoreToFakeInstance(),
                GetStandAloneMSBuildInstance()
            };

            var msbuildLocator = new MSFakeLocator(msBuildInstances);
            var logger = LoggerFactory.CreateLogger(nameof(RegisterDefaultInstanceFindsTheBestInstanceAvailable));

            // test
            msbuildLocator.RegisterDefaultInstance(logger);

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
                    Version.Parse("15.3.2.1"),
                    DiscoveryType.VisualStudioSetup
                ),
                GetInvalidMsBuildInstance(),

                // Valid + Dotnet Core
                new MSBuildInstance(
                    "Another Valid Test Instance",
                    TestIO.GetRandomTempFolderPath(),
                    Version.Parse("15.1.2.3"),
                    DiscoveryType.VisualStudioSetup
                ).AddDotNetCoreToFakeInstance(),
                GetStandAloneMSBuildInstance()
            };

            var msbuildLocator = new MSFakeLocator(msBuildInstances);

            var logger = LoggerFactory.CreateLogger(
                nameof(RegisterDefaultInstanceFindsTheBestInstanceAvailableEvenWithOtherValidInstances)
            );

            // test
            msbuildLocator.RegisterDefaultInstance(logger);

            Assert.NotNull(msbuildLocator.RegisteredInstance);
            Assert.Same(msBuildInstances[2], msbuildLocator.RegisteredInstance);

            // clean up
            msbuildLocator.DeleteFakeInstancesFolders();
        }

        [Fact]
        public void RegisterDefaultInstanceStillPrefersTheFirstInstance()
        {
            var msBuildInstances = new[]
            {
                new MSBuildInstance(
                    "Test Instance",
                    TestIO.GetRandomTempFolderPath(),
                    Version.Parse("15.1.2.3"),
                    DiscoveryType.VisualStudioSetup
                ),
                GetStandAloneMSBuildInstance()
            };

            var msbuildLocator = new MSFakeLocator(msBuildInstances);
            var logger = LoggerFactory.CreateLogger(nameof(RegisterDefaultInstanceStillPrefersTheFirstInstance));

            // test
            msbuildLocator.RegisterDefaultInstance(logger);

            Assert.NotNull(msbuildLocator.RegisteredInstance);
            Assert.Same(msBuildInstances[0], msbuildLocator.RegisteredInstance);

            // clean up
            msbuildLocator.DeleteFakeInstancesFolders();
        }

        private static MSBuildInstance GetStandAloneMSBuildInstance()
        {
            return new MSBuildInstance(
                "Stand Alone :(",
                TestIO.GetRandomTempFolderPath(),
                Version.Parse("99.0.0.0"),
                DiscoveryType.StandAlone
            );
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
