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

        protected abstract string EndpointName { get; }

        protected TRequestHandler GetRequestHandler(TestOmniSharpHost host)
        {
            return host.GetRequestHandler<TRequestHandler>(EndpointName);
        }
    }
}
