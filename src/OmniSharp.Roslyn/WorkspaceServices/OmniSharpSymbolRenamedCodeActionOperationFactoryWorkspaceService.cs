using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;

namespace OmniSharp
{
    public class OmniSharpSymbolRenamedCodeActionOperationFactoryWorkspaceService : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            return new RenameSymbolOperation(
                (ISymbol)args[0],
                (string)args[1],
                (Solution)args[2],
                (Solution)args[3]);
        }

        private class RenameSymbolOperation : CodeActionOperation
        {
            private readonly ISymbol _symbol;
            private readonly string _newName;
            private readonly Solution _startingSolution;
            private readonly Solution _updatedSolution;

            public RenameSymbolOperation(
                ISymbol symbol,
                string newName,
                Solution startingSolution,
                Solution updatedSolution)
            {
                _symbol = symbol;
                _newName = newName;
                _startingSolution = startingSolution;
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
