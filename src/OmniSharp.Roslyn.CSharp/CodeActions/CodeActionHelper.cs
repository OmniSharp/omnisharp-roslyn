using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.CodeActions
{
    /// <summary>
    /// This class contains code fixes and refactorings that should be removed for various reasons.
    /// </summary>
    [Export, Shared]
    public class CodeActionHelper
    {
        public const string AddImportProviderName = "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeFixProvider";
        public const string RemoveUnnecessaryUsingsProviderName = "Microsoft.CodeAnalysis.CSharp.RemoveUnnecessaryImports.CSharpRemoveUnnecessaryImportsCodeFixProvider";

        private static readonly HashSet<string> _roslynListToRemove = new HashSet<string>
        {
            "Microsoft.CodeAnalysis.CSharp.AddMissingReference.CSharpAddMissingReferenceCodeFixProvider",
            "Microsoft.CodeAnalysis.CSharp.CodeFixes.Async.CSharpConvertToAsyncMethodCodeFixProvider",
            "Microsoft.CodeAnalysis.CSharp.CodeFixes.Iterator.CSharpChangeToIEnumerableCodeFixProvider",
            "Microsoft.CodeAnalysis.ChangeSignature.ChangeSignatureCodeRefactoringProvider",
            "Microsoft.CodeAnalysis.ExtractInterface.ExtractInterfaceCodeRefactoringProvider"
        };

        private static bool s_validated;

        private static void ValidateRoslynList(IAssemblyLoader loader)
        {
            if (s_validated)
            {
                return;
            }

            // Check to see if the Roslyn code fix and refactoring provider type names can be found.
            // If this fails, OmniSharp has updated to a new version of Roslyn and one of the type names changed.
            var assemblies = new[]
            {
                loader.Load(Configuration.RoslynCSharpFeatures),
                loader.Load(Configuration.RoslynFeatures),
                loader.Load(Configuration.RoslynWorkspaces)
            };

            var typeNames = _roslynListToRemove.Concat(new[] { AddImportProviderName, RemoveUnnecessaryUsingsProviderName });

            foreach (var typeName in typeNames)
            {
                if (!ExistsInAssemblyList(typeName, assemblies))
                {
                    throw new InvalidOperationException($"Could not find '{typeName}'. Has this type name changed?");
                }
            }

            s_validated = true;
        }

        private static bool ExistsInAssemblyList(string typeName, Assembly[] assemblies)
        {
            return assemblies.Any(a => a.GetType(typeName) == null);
        }

        [ImportingConstructor]
        public CodeActionHelper(IAssemblyLoader loader)
        {
            ValidateRoslynList(loader);
        }

        public bool IsDisallowed(string typeName)
        {
            return _roslynListToRemove.Contains(typeName);
        }

        public bool IsDisallowed(CodeFixProvider provider)
        {
            return IsDisallowed(provider.GetType().FullName);
        }

        public bool IsDisallowed(CodeRefactoringProvider provider)
        {
            return IsDisallowed(provider.GetType().FullName);
        }
    }
}
