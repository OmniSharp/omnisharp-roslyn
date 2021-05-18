using System.Composition;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.CodeRefactorings.WorkspaceServices;

namespace OmniSharp
{
    [Shared]
    [Export(typeof(IOmniSharpSymbolRenamedCodeActionOperationFactoryWorkspaceService))]
    public class OmniSharpSymbolRenamedCodeActionOperationFactoryWorkspaceService : IOmniSharpSymbolRenamedCodeActionOperationFactoryWorkspaceService
    {
        [ImportingConstructor]
        public OmniSharpSymbolRenamedCodeActionOperationFactoryWorkspaceService()
        {
        }

        public CodeActionOperation CreateSymbolRenamedOperation(ISymbol symbol, string newName, Solution startingSolution, Solution updatedSolution)
        {
            return new RenameSymbolOperation(
                symbol,
                newName,
                updatedSolution);
        }

        private class RenameSymbolOperation : CodeActionOperation
        {
            private readonly ISymbol _symbol;
            private readonly string _newName;
            private readonly Solution _updatedSolution;

            public RenameSymbolOperation(
                ISymbol symbol,
                string newName,
                Solution updatedSolution)
            {
                _symbol = symbol;
                _newName = newName;
                _updatedSolution = updatedSolution;
            }

            public override void Apply(Workspace workspace, CancellationToken cancellationToken = default)
            {
                workspace.TryApplyChanges(_updatedSolution);
            }

            public override string Title => $"Rename {_symbol.Name} to {_newName}";
        }
    }
}
