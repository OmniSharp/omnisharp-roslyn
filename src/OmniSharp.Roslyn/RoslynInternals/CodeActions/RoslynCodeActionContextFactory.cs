#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Roslyn.Reflection;

namespace OmniSharp.Roslyn.RoslynInternals.CodeActions
{
    public static class RoslynCodeActionContextFactory
    {
        public static CodeFixContext CreateCodeFixContext(
            Document document, TextSpan span, ImmutableArray<Diagnostic> diagnostics,
            Action<CodeAction, ImmutableArray<Diagnostic>> registerCodeFix, CancellationToken cancellationToken)
        {
            var constructor = typeof(CodeFixContext).GetConstructors(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(c => c.GetParameters().Length == 5 &&
                             c.GetParameters()[0].ParameterType == typeof(Document) &&
                             c.GetParameters()[1].ParameterType == typeof(TextSpan));
            return (CodeFixContext)RoslynReflection.Invoke(
                constructor, document, span, diagnostics, registerCodeFix, cancellationToken);
        }

        public static CodeRefactoringContext CreateCodeRefactoringContext(
            Document document, TextSpan span, Action<CodeAction, TextSpan?> registerRefactoring,
            CancellationToken cancellationToken)
        {
            var constructor = typeof(CodeRefactoringContext).GetConstructors(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(c => c.GetParameters().Length == 4 &&
                             c.GetParameters()[0].ParameterType.IsAssignableFrom(typeof(Document)) &&
                             c.GetParameters()[2].ParameterType == typeof(Action<CodeAction, TextSpan?>));
            return (CodeRefactoringContext)RoslynReflection.Invoke(
                constructor, document, span, registerRefactoring, cancellationToken);
        }

        public static FixAllContext CreateFixAllContext(
            Document? document, TextSpan? diagnosticSpan, Project project, CodeFixProvider codeFixProvider,
            FixAllScope scope, string? codeActionEquivalenceKey, IEnumerable<string> diagnosticIds,
            FixAllContext.DiagnosticProvider fixAllDiagnosticProvider, CancellationToken cancellationToken)
        {
            var stateType = RoslynReflection.GetType(RoslynReflection.WorkspacesAssembly, "Microsoft.CodeAnalysis.CodeFixes.FixAllState");
            var noOpType = RoslynReflection.GetType(RoslynReflection.WorkspacesAssembly, "Microsoft.CodeAnalysis.CodeFixes.NoOpFixAllProvider");
            var noOp = noOpType.GetField("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
                ?? throw new InvalidOperationException("Roslyn no-op fix-all provider instance was not found.");
            var state = RoslynReflection.CreateInstance(stateType, noOp, diagnosticSpan, document, project,
                codeFixProvider, scope, codeActionEquivalenceKey, diagnosticIds, fixAllDiagnosticProvider);
            var progressType = RoslynReflection.GetType(RoslynReflection.WorkspacesAssembly, "Microsoft.CodeAnalysis.CodeAnalysisProgress");
            var progress = progressType.GetField("None", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
                ?? throw new InvalidOperationException("Roslyn no-op code analysis progress instance was not found.");
            return (FixAllContext)RoslynReflection.CreateInstance(typeof(FixAllContext), state, progress, cancellationToken);
        }
    }
}
