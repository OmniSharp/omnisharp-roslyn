using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace OmniSharp
{
    public class ExtractClassWorkspaceService : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            var featuresAssembly = Assembly.Load("Microsoft.CodeAnalysis.Features");
            var extractClassOptionsType = featuresAssembly.GetType("Microsoft.CodeAnalysis.ExtractClass.ExtractClassOptions");
            var extractClassMemberAnalysisResultType = featuresAssembly.GetType("Microsoft.CodeAnalysis.ExtractClass.ExtractClassMemberAnalysisResult");

            var originalType = (INamedTypeSymbol)args[1];
            var selectedSymbol = (ISymbol)args[2];

            var symbolsToUse = selectedSymbol == null ? originalType.GetMembers().Where(member => member switch
            {
                IMethodSymbol methodSymbol => methodSymbol.MethodKind == MethodKind.Ordinary,
                IFieldSymbol fieldSymbol => !fieldSymbol.IsImplicitlyDeclared,
                _ => member.Kind == SymbolKind.Property || member.Kind == SymbolKind.Event
            }).ToArray() : new ISymbol[1] { selectedSymbol };

            var extractClassMemberAnalysisResultImmutableArray = typeof(ImmutableArray).GetMethods()
                .Where(x => x.Name == "CreateRange")
                .Select(method => new
                {
                    method,
                    parameters = method.GetParameters(),
                    genericArguments = method.GetGenericArguments()
                })
                .Where(method => method.genericArguments.Length == 1 && method.parameters.Length == 1)
                .Select(x => x.method)
                .First()
                .MakeGenericMethod(extractClassMemberAnalysisResultType).Invoke(null, new object[]
                {
                    typeof(Enumerable).GetMethod("Cast")
                    .MakeGenericMethod(extractClassMemberAnalysisResultType).Invoke(null, new object[]
                    {
                        symbolsToUse.Select(symbol => Activator.CreateInstance(extractClassMemberAnalysisResultType, new object[]
                        {
                            symbol,
                            false // mark abstract
                        }))
                    })
                });

            const string name = "NewBaseType";

            var resultObject = Activator.CreateInstance(extractClassOptionsType, new object[] {
                    $"{name}.cs",
                    name,
                    true, // same file
                    extractClassMemberAnalysisResultImmutableArray
                });

            return typeof(Task).GetMethod("FromResult").MakeGenericMethod(extractClassOptionsType).Invoke(null, new[] { resultObject });
        }
    }
}
