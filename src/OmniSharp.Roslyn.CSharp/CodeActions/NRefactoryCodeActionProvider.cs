using System.Collections.Generic;
using System.Composition;
using System.Linq;
//using ICSharpCode.NRefactory6.CSharp.Refactoring;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.CodeActions
{
    [Export(typeof(ICodeActionProvider))]
    public class NRefactoryCodeActionProvider : AbstractCodeActionProvider
    {
        [ImportingConstructor]
        public NRefactoryCodeActionProvider(IOmnisharpAssemblyLoader loader)
            : base("NRefactory", loader)
        { }

        public override IEnumerable<CodeFixProvider> CodeFixes => Enumerable.Empty<CodeFixProvider>();

        public override IEnumerable<CodeRefactoringProvider> Refactorings => Enumerable.Empty<CodeRefactoringProvider>();
    }
}
