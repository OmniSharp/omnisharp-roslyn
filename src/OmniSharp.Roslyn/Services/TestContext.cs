using Microsoft.CodeAnalysis;
using OmniSharp.Models.TestCommand;

namespace OmniSharp.Services
{
    public class TestContext
    {
        public string ProjectFile { get; private set; }
        public TestCommandType TestCommandType { get; private set; }
        public ISymbol Symbol { get; private set; }

        public TestContext(string projectFile, TestCommandType testCommandType, ISymbol symbol)
        {
            ProjectFile = projectFile;
            TestCommandType = testCommandType;
            Symbol = symbol;
        }
    }
}