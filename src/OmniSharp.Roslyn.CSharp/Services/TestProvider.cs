using OmniSharp.ConfigurationManager;
using OmniSharp.Models.TestCommand;
using OmniSharp.Services;
using System.Composition;

namespace OmniSharp.Roslyn.CSharp.Services
{
    [Export(typeof(ITestCommandProvider))]
    class TestProvider : ITestCommandProvider
    {
        public OmniSharpConfiguration config { get => ConfigurationLoader.Config; set => throw new System.NotImplementedException(); }
        public TestCommandType testCommands { get; set; }

        public string GetTestCommand(TestContext testContext)
        {
            string test = string.Empty;

            switch(testContext.TestCommandType)
            {
                case OmniSharp.Models.TestCommand.TestCommandType.All:
                    test = testContext.Symbol.ContainingAssembly.MetadataName;
                    break;
                case OmniSharp.Models.TestCommand.TestCommandType.Fixture:
                    test = testContext.Symbol.GetDocumentationCommentId().Substring(2, testContext.Symbol.GetDocumentationCommentId().Substring(2).LastIndexOf('.'));
                    break;
                case OmniSharp.Models.TestCommand.TestCommandType.Single:
                    test = testContext.Symbol.GetDocumentationCommentId().Substring(2);
                    break;
            }
            return test;
        }
    }
}
