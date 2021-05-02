using System.Collections.Generic;
using System.Composition;
using Microsoft.CodeAnalysis;
using OmniSharp.DotNetTest.TestFrameworks;
using OmniSharp.Extensions;
using OmniSharp.Services;

namespace OmniSharp.DotNetTest
{
    [Shared]
    [Export(typeof(ICodeElementPropertyProvider))]
    internal class TestMethodPropertyProvider : ICodeElementPropertyProvider
    {
        public IEnumerable<(string name, object value)> ProvideProperties(ISymbol symbol)
        {
            if (symbol is IMethodSymbol methodSymbol)
            {
                foreach (var framework in TestFramework.Frameworks)
                {
                    if (framework.IsTestMethod(methodSymbol))
                    {
                        yield return ("testFramework", framework.Name);
                        yield return ("testMethodName", methodSymbol.GetMetadataName());
                    }
                }
            }
        }
    }
}
