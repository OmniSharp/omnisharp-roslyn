#if DNX451
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ICSharpCode.NRefactory6.CSharp.Refactoring;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace OmniSharp.Services
{
    public class NRefactoryCodeActionProvider : ICodeActionProvider
    {
        public IEnumerable<CodeRefactoringProvider> GetProviders()
        {
            // todo , replace this with the nrefactory filtered list of providers
            
            var types = Assembly.Load("Microsoft.CodeAnalysis.CSharp.Features, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35")
                                .GetTypes()
                                .Where(t => typeof(CodeRefactoringProvider).IsAssignableFrom(t));

            IEnumerable<CodeRefactoringProvider> providers =
                types
                    .Where(type => !type.IsInterface
                            && !type.IsAbstract
                            && !type.ContainsGenericParameters) // TODO: handle providers with generic params 
                    .Select(type => (CodeRefactoringProvider)Activator.CreateInstance(type));

            return providers;
        }
    }
}
#endif
