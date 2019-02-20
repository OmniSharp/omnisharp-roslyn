using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Microsoft.CodeAnalysis;

namespace OmniSharp
{
    public class ExtractInterfaceWorkspaceService : IInterceptor
    {
        public void Intercept(IInvocation invocation)
        {
            // IPickMember and PickMemberResults are internal types -> workaround with proxy.
            // This service simply passes all members through as selected and doesn't try show UI.
            // When roslyn exposes this interface and members -> get rid of this workaround.
            var resultTypeInternal = Assembly.Load("Microsoft.CodeAnalysis.ExtractInterface").GetType("Microsoft.CodeAnalysis.PickMembers.PickMembersResult");

            var resultObject = Activator.CreateInstance(resultTypeInternal, new object[] {
                    false, // isCancelled
                    ((List<ISymbol>)invocation.Arguments[2]).ToImmutableArray(), // InterfaceMembers selected -> take all.
                    invocation.Arguments[3], // Interface name, use default.
                    $"{invocation.Arguments[3]}.cs", // FileName
                    0 // to same file (enum)
                });

            invocation.ReturnValue = Task.Run(() => resultObject);
        }
    }
}