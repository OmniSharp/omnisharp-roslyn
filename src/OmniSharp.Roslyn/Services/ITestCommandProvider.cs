namespace OmniSharp.Services
{
    public interface ITestCommandProvider
    {
        string GetTestCommand(TestContext testContext);
    }
}