using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.PickMembers;
using System.Collections.Immutable;
using System.Composition;

namespace OmniSharp
{
    [Shared]
    [Export(typeof(IOmniSharpPickMembersService))]
    internal class PickMemberWorkspaceService : IOmniSharpPickMembersService
    {
        [ImportingConstructor]
        public PickMemberWorkspaceService()
        {
        }
        public OmniSharpPickMembersResult PickMembers(string title, ImmutableArray<ISymbol> members, ImmutableArray<OmniSharpPickMembersOption> options = default, bool selectAll = true)
        {
            return new OmniSharpPickMembersResult(members, options.IsDefault ? ImmutableArray<OmniSharpPickMembersOption>.Empty : options, selectAll);
        }
    }
}
