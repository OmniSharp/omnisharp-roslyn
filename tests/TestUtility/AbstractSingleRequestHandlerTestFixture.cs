using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using Xunit.Abstractions;

namespace TestUtility
{
    public abstract class AbstractSingleRequestHandlerTestFixture<TRequestHandler> : AbstractTestFixture
        where TRequestHandler : IRequestHandler
    {
        protected AbstractSingleRequestHandlerTestFixture(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        protected AbstractSingleRequestHandlerTestFixture(ITestOutputHelper testOutput, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(testOutput, sharedOmniSharpHostFixture)
        {
        }

        protected abstract string EndpointName { get; }

        protected TRequestHandler GetRequestHandler(OmniSharpTestHost host, string languageName = LanguageNames.CSharp)
        {
            return host.GetRequestHandler<TRequestHandler>(EndpointName, languageName);
        }
    }
}
