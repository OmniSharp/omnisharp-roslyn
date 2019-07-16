using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace OmniSharp
{
    public class ExtractInterfaceWorkspaceService : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            // IExtractInterfaceOptionsService and extract interface results are internal types -> workaround with proxy.
            // This service simply passes all members through as selected and doesn't try show UI.
            // When roslyn exposes this interface and members -> remove this workaround.
            var resultTypeInternal = Assembly.Load("Microsoft.CodeAnalysis.Features").GetType("Microsoft.CodeAnalysis.ExtractInterface.ExtractInterfaceOptionsResult");
            var enumType = resultTypeInternal.GetNestedTypes().Single(x => x.Name == "ExtractLocation");

            var toSameFileEnumValue = Enum.Parse(enumType, "SameFile");

            var interfaceName = args[3] ?? throw new InvalidOperationException($"{nameof(ExtractInterfaceWorkspaceService)} default interface name was null.");

            var resultObject = Activator.CreateInstance(resultTypeInternal, new object[] {
                    false, // isCancelled
                    ((List<ISymbol>)args[2]).ToImmutableArray(), // InterfaceMembers selected -> select all.
                    interfaceName,
                    $"{interfaceName}.cs",
                    toSameFileEnumValue
                });

            return typeof(Task).GetMethod("FromResult").MakeGenericMethod(resultTypeInternal).Invoke(null, new[] { resultObject });
        }
    }
}
