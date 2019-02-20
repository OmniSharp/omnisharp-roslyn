using System;
using System.Reflection;
using Castle.DynamicProxy;

namespace OmniSharp
{
    public class PickMemberWorkspaceService : IInterceptor
    {
        public void Intercept(IInvocation invocation)
        {
            // IPickMember and PickMemberResults are internal types -> workaround with proxy.
            // This service simply passes all members through as selected and doesn't try show UI.
            // When roslyn exposes this interface and members -> get rid of this workaround.
            var resultTypeInternal = Assembly.Load("Microsoft.CodeAnalysis.Features").GetType("Microsoft.CodeAnalysis.PickMembers.PickMembersResult");
            var resultInstance = Activator.CreateInstance(resultTypeInternal, new object[] { invocation.Arguments[1], invocation.Arguments[2] });
            invocation.ReturnValue = resultInstance;
        }
    }
}