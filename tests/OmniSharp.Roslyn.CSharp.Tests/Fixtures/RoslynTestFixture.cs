using Microsoft.Extensions.Logging;
using OmniSharp.Services;
using TestUtility.Annotate;
using TestUtility.Fake;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class RoslynTestFixture
    {
        private readonly ILoggerFactory _loggerFactory;
        
        public RoslynTestFixture()
        {
            _loggerFactory = new LoggerFactory();
            _loggerFactory.AddConsole();
        }
        
        public ILogger CreateLogger<T>()
        {
            return _loggerFactory.CreateLogger<T>();
        }
        
        public IOmnisharpAssemblyLoader CreateAssemblyLoader(ILogger logger)
        {
            return new AnnotateAssemblyLoader(logger);
        }
        
        public ILoggerFactory FakeLoggerFactory = new FakeLoggerFactory();
        
        public ILogger FakeLogger = new FakeLogger();
    }
}