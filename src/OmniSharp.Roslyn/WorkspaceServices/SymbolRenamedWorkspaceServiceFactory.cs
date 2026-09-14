#nullable enable

using System;
using System.Composition;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using OmniSharp.Roslyn.Reflection;

namespace OmniSharp.Roslyn.WorkspaceServices
{
    [Shared]
    [ExportWorkspaceServiceFactoryWithAssemblyQualifiedName(
        RoslynReflection.FeaturesAssembly,
        "Microsoft.CodeAnalysis.CodeActions.WorkspaceServices.ISymbolRenamedCodeActionOperationFactoryWorkspaceService")]
    internal sealed class SymbolRenamedWorkspaceServiceFactory : InternalWorkspaceServiceFactory
    {
        [ImportingConstructor]
        public SymbolRenamedWorkspaceServiceFactory()
            : base(
                RoslynReflection.FeaturesAssembly,
                "Microsoft.CodeAnalysis.CodeActions.WorkspaceServices.ISymbolRenamedCodeActionOperationFactoryWorkspaceService")
        {
        }

        protected override object? Invoke(MethodInfo method, object?[] arguments)
        {
            if (method.Name != "CreateSymbolRenamedOperation")
                throw new MissingMethodException(method.DeclaringType?.FullName, method.Name);
            return new RenameSymbolOperation((ISymbol)arguments[0]!, (string)arguments[1]!, (Solution)arguments[3]!);
        }

        private sealed class RenameSymbolOperation : CodeActionOperation
        {
            private readonly ISymbol _symbol;
            private readonly string _newName;
            private readonly Solution _solution;

            public RenameSymbolOperation(ISymbol symbol, string newName, Solution solution)
                => (_symbol, _newName, _solution) = (symbol, newName, solution);

            public override string Title => $"Rename {_symbol.Name} to {_newName}";
            public override void Apply(Workspace workspace, CancellationToken cancellationToken = default)
                => workspace.TryApplyChanges(_solution);
        }
    }
}
