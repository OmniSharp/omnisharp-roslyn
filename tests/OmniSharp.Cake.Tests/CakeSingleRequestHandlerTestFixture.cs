using OmniSharp.Cake.Services.RequestHandlers.Buffer;
using OmniSharp.Mef;
using TestUtility;
using Xunit.Abstractions;

namespace OmniSharp.Cake.Tests
{
    public abstract class CakeSingleRequestHandlerTestFixture<TRequestHandler> : AbstractTestFixture
        where TRequestHandler : IRequestHandler
    {
        protected CakeSingleRequestHandlerTestFixture(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        protected abstract string EndpointName { get; }

        protected TRequestHandler GetRequestHandler(OmniSharpTestHost host)
        {
            return host.GetRequestHandler<TRequestHandler>(EndpointName, Constants.LanguageNames.Cake);
        }

        protected UpdateBufferHandler GetUpdateBufferHandler(OmniSharpTestHost host)
        {
            return host.GetRequestHandler<UpdateBufferHandler>(OmniSharpEndpoints.UpdateBuffer, Constants.LanguageNames.Cake);
        }
    }
}
