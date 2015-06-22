#if DNX451
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using ICSharpCode.NRefactory6.CSharp.Refactoring;
using Microsoft.CodeAnalysis.CodeFixes;

namespace OmniSharp.Services
{
    public class NRefactoryCodeActionProvider : AbstractCodeActionProvider
    {
        public NRefactoryCodeActionProvider() : base(typeof(UseVarKeywordAction).Assembly)
        {
        }

        public override IEnumerable<CodeFixProvider> CodeFixes => Enumerable.Empty<CodeFixProvider>();

        public override string ProviderName => "NRefactory";

        internal static ImmutableArray<Assembly> MefAssemblies =>
            ImmutableArray.Create<Assembly>(typeof(UseVarKeywordAction).Assembly);
    }
}
#endif
