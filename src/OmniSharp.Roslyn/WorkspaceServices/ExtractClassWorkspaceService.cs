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
            // the args correspond to the following interface method on IExtractClassOptionsService
            // http://sourceroslyn.io/#Microsoft.CodeAnalysis.Features/ExtractClass/IExtractClassOptionsService.cs,30b2d7f792fbcc68
            // internal interface IExtractClassOptionsService : IWorkspaceService
            // {
            //   Task<ExtractClassOptions?> GetExtractClassOptionsAsync(Document document, INamedTypeSymbol originalType, ISymbol? selectedMember);
            // }
            // if it changes, this implementation must be changed accordingly

            var featuresAssembly = Assembly.Load("Microsoft.CodeAnalysis.Features");
            var extractClassOptionsType = featuresAssembly.GetType("Microsoft.CodeAnalysis.ExtractClass.ExtractClassOptions");
            var extractClassMemberAnalysisResultType = featuresAssembly.GetType("Microsoft.CodeAnalysis.ExtractClass.ExtractClassMemberAnalysisResult");

            // we need to create Enumerable.Cast<ExtractClassMemberAnalysisResult>() where ExtractClassMemberAnalysisResult is not accessible publicly
            var genericCast = typeof(Enumerable).GetMethod("Cast").MakeGenericMethod(extractClassMemberAnalysisResultType);

            var originalType = (INamedTypeSymbol)args[1];
            var selectedSymbol = (ISymbol)args[2];

            var symbolsToUse = selectedSymbol == null ? originalType.GetMembers().Where(member => member switch
            {
                IMethodSymbol methodSymbol => methodSymbol.MethodKind == MethodKind.Ordinary,
                IFieldSymbol fieldSymbol => !fieldSymbol.IsImplicitlyDeclared,
                _ => member.Kind == SymbolKind.Property || member.Kind == SymbolKind.Event
            }).ToArray() : new ISymbol[1] { selectedSymbol };

            // we need to create ImmutableArray.CreateRange<ExtractClassMemberAnalysisResult>() where ExtractClassMemberAnalysisResult is not accessible publicly
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
                    // at this point we have IEnumerable<object> and need to cast to IEnumerable<ExtractClassMemberAnalysisResult>
                    // which we can then pass to ImmutableArray.CreateRange<ExtractClassMemberAnalysisResult>()
                    genericCast.Invoke(null, new object[]
                    {
                        // this constructor corresponds to
                        // http://sourceroslyn.io/#Microsoft.CodeAnalysis.Features/ExtractClass/ExtractClassOptions.cs,ced9042e0a010e24
                        // public ExtractClassMemberAnalysisResult(ISymbol member,bool makeAbstract)
                        // if it changes, this implementation must be changed accordingly
                        symbolsToUse.Select(symbol => Activator.CreateInstance(extractClassMemberAnalysisResultType, new object[]
                        {
                            symbol,
                            false
                        }))
                    })
                });

            const string name = "NewBaseType";

            // this constructor corresponds to
            // http://sourceroslyn.io/#Microsoft.CodeAnalysis.Features/ExtractClass/ExtractClassOptions.cs,6f65491c71285819,references
            // public ExtractClassOptions(string fileName, string typeName, bool sameFile, ImmutableArray<ExtractClassMemberAnalysisResult> memberAnalysisResults)
            // if it changes, this implementation must be changed accordingly
            var resultObject = Activator.CreateInstance(extractClassOptionsType, new object[] {
                    $"{name}.cs",
                    name,
                    true,
                    extractClassMemberAnalysisResultImmutableArray
                });

            // the return type is Task<ExtractClassOptions>
            return typeof(Task).GetMethod("FromResult").MakeGenericMethod(extractClassOptionsType).Invoke(null, new[] { resultObject });
        }
    }
}
