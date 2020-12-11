using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp.MSBuild.Discovery;
using OmniSharp.Services;
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
                GetStandAloneMSBuildInstance()
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
                GetStandAloneMSBuildInstance()
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
                GetStandAloneMSBuildInstance()
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
                GetStandAloneMSBuildInstance(),

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
            Assert.Same(msBuildInstances[4], msbuildLocator.RegisteredInstance);

            // clean up
            msbuildLocator.DeleteFakeInstancesFolders();
        }

        [Fact]
        public void RegisterDefaultInstancePrefersSupportedVSLowerVersionInstanceOverStandAlone()
        {
            var msBuildInstances = new[]
            {
                new MSBuildInstance(
                    "Test Instance",
                    TestIO.GetRandomTempFolderPath(),
                    Version.Parse("16.3.2.3"),
                    DiscoveryType.VisualStudioSetup
                ).AddDotNetCoreToFakeInstance(),
                GetStandAloneMSBuildInstance()
            };

            var msbuildLocator = new MSFakeLocator(msBuildInstances);
            var logger = LoggerFactory.CreateLogger(nameof(RegisterDefaultInstancePrefersSupportedVSLowerVersionInstanceOverStandAlone));

            // test
            msbuildLocator.RegisterDefaultInstance(logger, dotNetInfo: null);

            Assert.NotNull(msbuildLocator.RegisteredInstance);
            Assert.Same(msBuildInstances[0], msbuildLocator.RegisteredInstance);

            // clean up
            msbuildLocator.DeleteFakeInstancesFolders();
        }

        [Theory]
        [InlineData("16.2.2.3")] // lower than standalone
        [InlineData("16.6.2.3")] // higher than standalone
        public void RegisterDefaultInstancePrefersStandAloneOverSupportedVSInstanceWithoutDotnetCore(string vsVersion)
        {
            var msBuildInstances = new[]
            {
                new MSBuildInstance(
                    "Test Instance",
                    TestIO.GetRandomTempFolderPath(),
                    Version.Parse(vsVersion),
                    DiscoveryType.VisualStudioSetup
                ),
                GetStandAloneMSBuildInstance()
            };

            var msbuildLocator = new MSFakeLocator(msBuildInstances);
            var logger = LoggerFactory.CreateLogger(nameof(RegisterDefaultInstancePrefersStandAloneOverSupportedVSInstanceWithoutDotnetCore));

            // test
            msbuildLocator.RegisterDefaultInstance(logger, dotNetInfo: null);

            Assert.NotNull(msbuildLocator.RegisteredInstance);
            Assert.Same(msBuildInstances[1], msbuildLocator.RegisteredInstance);

            // clean up
            msbuildLocator.DeleteFakeInstancesFolders();
        }

        [Theory]
        [InlineData("15.1.2.3")]
        [InlineData("15.9.2.3")]
        [InlineData("16.1.2.3")]
        [InlineData("16.2.2.3")]
        public void StandAloneIsPreferredOverUnsupportedVS(string vsVersion)
        {
            var msBuildInstances = new[]
            {
                new MSBuildInstance(
                    "Test Instance",
                    TestIO.GetRandomTempFolderPath(),
                    Version.Parse(vsVersion),
                    DiscoveryType.VisualStudioSetup
                ).AddDotNetCoreToFakeInstance(),
                GetStandAloneMSBuildInstance()
            };

            var msbuildLocator = new MSFakeLocator(msBuildInstances);
            var logger = LoggerFactory.CreateLogger(nameof(StandAloneIsPreferredOverUnsupportedVS));

            // test
            msbuildLocator.RegisterDefaultInstance(logger, dotNetInfo: null);

            Assert.NotNull(msbuildLocator.RegisteredInstance);
            Assert.Same(msBuildInstances[1], msbuildLocator.RegisteredInstance);

            // clean up
            msbuildLocator.DeleteFakeInstancesFolders();
        }

        [Theory]
        [InlineData(true, 1)]
        [InlineData(false, 4)]
        public void CreateDefault_Respects_UseBundledOnlySetting(bool useBundledOnly, int expectedLocatorCount)
        {
            var configBuilder = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>()
            {
                ["useBundledOnly"] = useBundledOnly.ToString()
            });
            var loggerFactory = new LoggerFactory();
            var locator = MSBuildLocator.CreateDefault(loggerFactory, new AssemblyLoader(loggerFactory), configBuilder.Build());
            var instances = locator.GetInstances();
            Assert.Equal(expectedLocatorCount, instances.Length);

            // if only a single one was found, it must be stand alone
            if (expectedLocatorCount == 1)
            {
                Assert.Equal(DiscoveryType.StandAlone, instances[0].DiscoveryType);
            }
        }

        private static MSBuildInstance GetStandAloneMSBuildInstance()
        {
            return new MSBuildInstance(
                "Stand Alone :(",
                TestIO.GetRandomTempFolderPath(),
                Version.Parse("16.4.0.0"),
                DiscoveryType.StandAlone
            ).AddDotNetCoreToFakeInstance();
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
