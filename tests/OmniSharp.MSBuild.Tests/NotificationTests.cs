using System;
using System.Collections.Generic;
using System.Composition.Hosting.Core;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Mef;
using OmniSharp.MSBuild.Notification;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.MSBuild.Tests
{
    public class NotificationTests : AbstractMSBuildTestFixture
    {
        public NotificationTests(ITestOutputHelper output)
            : base(output)
        {
        }

        private class FakeMSBuildEventSink : IMSBuildEventSink
        {
            private readonly Action<ProjectLoadedEventArgs> _onLoaded;

            public FakeMSBuildEventSink(Action<ProjectLoadedEventArgs> onLoaded)
            {
                _onLoaded = onLoaded;
            }

            public Task ProjectLoadedAsync(ProjectLoadedEventArgs e, CancellationToken cancellationToken = default)
            {
                _onLoaded(e);
                return Task.CompletedTask;
            }

            public ValueTask ProjectLoadStartedAsync(string projectPath, CancellationToken cancellationToken = default) =>
                new();
        }

        [Fact]
        public async Task ProjectLoadedFires()
        {
            var allEventArgs = new List<ProjectLoadedEventArgs>();

            var eventSink = new FakeMSBuildEventSink(e =>
            {
                allEventArgs.Add(e);
            });

            var exports = new ExportDescriptorProvider[]
            {
                MefValueProvider.From<IMSBuildEventSink>(eventSink)
            };

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectAndSolution"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, exports))
            {
                var eventArgs = Assert.Single(allEventArgs);
                Assert.Equal(
                    $"{testProject.Directory}/{testProject.Name}.csproj".EnsureForwardSlashes(),
                    eventArgs.ProjectInstance.FullPath.EnsureForwardSlashes());
            }
        }
    }
}
