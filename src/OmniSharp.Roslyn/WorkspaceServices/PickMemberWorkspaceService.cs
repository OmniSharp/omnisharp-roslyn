using System;
using System.Reflection;

namespace OmniSharp
{
    public class PickMemberWorkspaceService : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            // IPickMember and PickMemberResults are internal types -> workaround with proxy.
            // This service simply passes all members through as selected and doesn't try show UI.
            // When roslyn exposes this interface and members -> remove this workaround.
            var resultTypeInternal = Assembly.Load("Microsoft.CodeAnalysis.Features").GetType("Microsoft.CodeAnalysis.PickMembers.PickMembersResult");
            return Activator.CreateInstance(resultTypeInternal, new object[] { args[1], args[2] });
        }
    }
}