#if DNX451

/*

using System.Collections.Generic;
using System.Composition;
using System.Linq;
using Microsoft.CodeAnalysis.CodeFixes;
using OmniSharp.Services;
using RefactoringEssentials.CSharp.CodeRefactorings;

namespace OmniSharp.Roslyn.CSharp.Services.CodeActions
{
    [Export(typeof(ICodeActionProvider))]
    public class NRefactoryCodeActionProvider : AbstractCodeActionProvider
    {
        [ImportingConstructor]
        public NRefactoryCodeActionProvider()
            : base("NRefactory", typeof(AddBracesCodeRefactoringProvider).Assembly)
        {
        }

        public override IEnumerable<CodeFixProvider> CodeFixes => Enumerable.Empty<CodeFixProvider>();
    }
}

*/
#endif
