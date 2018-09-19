using OmniSharp.ConfigurationManager;
using OmniSharp.Models.TestCommand;

namespace OmniSharp.Services
{
    public interface ITestCommandProvider
    {
        string GetTestCommand(TestContext testContext);
        OmniSharpConfiguration config { get; set; }
        TestCommandType testCommands { get; set; }
    }
}
