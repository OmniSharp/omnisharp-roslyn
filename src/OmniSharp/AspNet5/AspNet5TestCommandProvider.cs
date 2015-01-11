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
            string testsToRun = "";
            
            if (symbol is IMethodSymbol)
            {
                testsToRun = symbol.ContainingType.Name + "." + symbol.Name;
            }
            else if (symbol is INamedTypeSymbol)
            {
                testsToRun = symbol.Name;
            }

            if (!symbol.ContainingNamespace.IsGlobalNamespace)
            {
                testsToRun = symbol.ContainingNamespace + "." + testsToRun;
            }

            string testCommand = null;

            switch (testContext.TestCommandType)
            {
                case TestCommandType.All:
                    testCommand = "k test";
                    break;
                case TestCommandType.Single:
                case TestCommandType.Fixture:
                    testCommand = "k test --test " + testsToRun;
                    break;
            }
            return testCommand;
        }
    }
}