using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Services;

namespace OmniSharp.Dnx
{
    [Export(typeof(ITestCommandProvider))]
    public class DnxTestCommandProvider : ITestCommandProvider
    {
        private readonly DnxContext _context;
        private readonly string _dnx;

        [ImportingConstructor]
        public DnxTestCommandProvider(DnxContext context,
                                      IOmnisharpEnvironment env,
                                      ILoggerFactory loggerFactory,
                                      IEventEmitter emitter)
        {
            _context = context;
            var dnxPaths = new DnxPaths(env, context.Options, loggerFactory);
            _dnx = dnxPaths.Dnx + " .";
        }

        public string GetTestCommand(TestContext testContext)
        {
            if (!_context.ProjectContextMapping.ContainsKey(testContext.ProjectFile))
            {
                return null;
            }

            var projectCounter = _context.ProjectContextMapping[testContext.ProjectFile];
            var project = _context.Projects[projectCounter];

            if (!project.Commands.ContainsKey("test"))
            {
                return null;
            }

            // Find the test command, if any and use that
            var symbol = testContext.Symbol;
            string arguments = "";

            var containingNamespace = "";
            if (!symbol.ContainingNamespace.IsGlobalNamespace)
            {
                containingNamespace = symbol.ContainingNamespace + ".";
            }

            switch (testContext.TestCommandType)
            {
                case TestCommandType.Fixture:
                    if (symbol is IMethodSymbol)
                    {
                        arguments = " -class " + containingNamespace
                            + symbol.ContainingType.Name;
                    }
                    else if (symbol is INamedTypeSymbol)
                    {
                        arguments = " -class " + containingNamespace + symbol.Name;
                    }
                    break;
                case TestCommandType.Single:
                    if (symbol is IMethodSymbol)
                    {
                        arguments = " -method " + containingNamespace +
                            symbol.ContainingType.Name + "." + symbol.Name;
                    }
                    else if (symbol is INamedTypeSymbol)
                    {
                        arguments = " -class " + containingNamespace +
                            symbol.Name;
                    }
                    break;
            }
            return _dnx + " test" + arguments;
        }
    }
}
