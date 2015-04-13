using Microsoft.CodeAnalysis;

namespace OmniSharp.AspNet5
{
    public class AspNet5TestCommandProvider : ITestCommandProvider
    {
        private readonly AspNet5Context _context;

        public AspNet5TestCommandProvider(AspNet5Context context)
        {
            _context = context;
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
            return "k test" + arguments;
        }
    }
}