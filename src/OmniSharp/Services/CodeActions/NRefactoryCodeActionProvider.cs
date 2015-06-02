#if DNX451
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using ICSharpCode.NRefactory6.CSharp.Refactoring;
namespace OmniSharp.Services
{
    public class NRefactoryCodeActionProvider : AbstractCodeActionProvider
    {
        public NRefactoryCodeActionProvider() : base(typeof(UseVarKeywordAction).Assembly)
        {
        }

        public override string ProviderName => "NRefactory";

        internal override ImmutableArray<Assembly> MefAssemblies =>
            ImmutableArray.Create<Assembly>(typeof(UseVarKeywordAction).Assembly);
    }
}
#endif
