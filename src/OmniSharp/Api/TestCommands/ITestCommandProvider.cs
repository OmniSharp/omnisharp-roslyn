namespace OmniSharp
{
    public interface ITestCommandProvider
    {
        string GetTestCommand(TestContext testContext);
    }
}