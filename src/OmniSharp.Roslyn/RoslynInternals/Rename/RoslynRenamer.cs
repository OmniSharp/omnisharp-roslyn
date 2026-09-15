#nullable enable

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Roslyn.Reflection;

namespace OmniSharp.Roslyn.RoslynInternals.Rename
{
    public readonly struct OmniSharpRenameOptions
    {
        public bool RenameInComments { get; }
        public bool RenameInStrings { get; }
        public bool RenameOverloads { get; }

        public OmniSharpRenameOptions(bool RenameInComments, bool RenameInStrings, bool RenameOverloads)
        {
            this.RenameInComments = RenameInComments;
            this.RenameInStrings = RenameInStrings;
            this.RenameOverloads = RenameOverloads;
        }
    }

    public static class RoslynRenamer
    {
        public readonly struct RenameResult
        {
            public Solution? Solution { get; }
            public string? ErrorMessage { get; }
            public RenameResult(Solution? solution, string? errorMessage) => (Solution, ErrorMessage) = (solution, errorMessage);
            public void Deconstruct(out Solution? solution, out string? errorMessage) => (solution, errorMessage) = (Solution, ErrorMessage);
        }

        public static async Task<RenameResult> RenameSymbolAsync(
            Solution solution, ISymbol symbol, string newName, OmniSharpRenameOptions options,
            ImmutableHashSet<ISymbol>? nonConflictSymbols, CancellationToken cancellationToken)
        {
            var optionsType = RoslynReflection.GetType(RoslynReflection.WorkspacesAssembly, "Microsoft.CodeAnalysis.Rename.SymbolRenameOptions");
            var roslynOptions = RoslynReflection.CreateInstance(optionsType,
                options.RenameOverloads, options.RenameInStrings, options.RenameInComments, false);
            var renamerType = RoslynReflection.GetType(RoslynReflection.WorkspacesAssembly, "Microsoft.CodeAnalysis.Rename.Renamer");
            var method = RoslynReflection.GetMethod(renamerType, "RenameSymbolAsync",
                m => m.IsStatic && m.GetParameters().Length == 5 &&
                     m.GetParameters()[3].ParameterType == optionsType);
            var resolution = await RoslynReflection.AwaitResultAsync(
                RoslynReflection.Invoke(method, null, solution, symbol, newName, roslynOptions, cancellationToken)).ConfigureAwait(false);
            return new RenameResult(
                (Solution?)RoslynReflection.GetFieldValue(resolution, "NewSolution"),
                (string?)RoslynReflection.GetFieldValue(resolution, "ErrorMessage"));
        }
    }
}
