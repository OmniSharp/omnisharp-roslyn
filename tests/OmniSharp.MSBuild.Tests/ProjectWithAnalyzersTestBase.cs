using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Eventing;
using OmniSharp.FileWatching;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Models.FilesChanged;
using OmniSharp.Options;
using OmniSharp.Services;
using TestUtility;
using Xunit;

namespace OmniSharp.MSBuild.Tests
{
    public abstract class ProjectWithAnalyzersTestBase : AbstractMSBuildTestFixture
    {
        protected ProjectWithAnalyzersTestBase(ITestOutputHelper output)
            : base(output)
        {
        }

        protected string ModifyXmlFileInPlace(string file, Action<XDocument> docUpdateAction)
        {
            var xmlFile = XDocument.Load(file);
            docUpdateAction(xmlFile);
            xmlFile.Save(file);
            return file;
        }

        protected static async Task NotifyFileChanged(OmniSharpTestHost host, string file)
        {
            await host.GetFilesChangedService().Handle(new[] {
                    new FilesChangedRequest() {
                    FileName = file,
                    ChangeType = FileChangeType.Change
                    }
                });
        }

        protected static async Task RestoreProject(ITestProject testProject)
        {
            DotNetCliOptions options = new DotNetCliOptions
            {
                LocationPaths = new[]
                {
                    Path.Combine(TestAssets.Instance.RootFolder, DotNetCliVersion.Current.GetFolderName())
                }
            };
            await new DotNetCliService(new LoggerFactory(), NullEventEmitter.Instance, Microsoft.Extensions.Options.Options.Create(options), new OmniSharpEnvironment(testProject.Directory)).RestoreAsync(testProject.Directory);
        }
    }
}
