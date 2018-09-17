﻿using OmniSharp.Services;
using System.Composition;

namespace OmniSharp.Roslyn.CSharp.Services
{
    [Export(typeof(ITestCommandProvider))]
    class TestProvider : ITestCommandProvider
    {
        public string GetTestCommand(TestContext testContext)
        {
            string test = string.Empty;

            switch(testContext.TestCommandType)
            {
                case OmniSharp.Models.TestCommand.TestCommandType.All:
                    test = testContext.Symbol.ContainingAssembly.MetadataName;
                    break;
                case OmniSharp.Models.TestCommand.TestCommandType.Fixture:
                    test = testContext.Symbol.GetDocumentationCommentId().Substring(2);
                    break;
                case OmniSharp.Models.TestCommand.TestCommandType.Single:
                    test = testContext.Symbol.GetDocumentationCommentId().Substring(2);
                    break;
            }
            return test;
        }
    }
}
