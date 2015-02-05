//#if ASPNET50
//using ICSharpCode.NRefactory6.CSharp.Refactoring;
//#endif
//using Microsoft.CodeAnalysis.CodeRefactorings;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Reflection;

//namespace OmniSharp.Services
//{
//#if ASPNET50
//    public class NRefactoryCodeActionProvider : ICodeActionProvider
//    {
//        public IEnumerable<CodeRefactoringProvider> GetProviders()
//        {
//            //todo , replace this with the nrefactory filtered list of providers
//            var types = Assembly.GetAssembly(typeof(UseVarKeywordAction))
//                                .GetTypes()
//                                .Where(t => typeof(CodeRefactoringProvider).IsAssignableFrom(t));

//            IEnumerable<CodeRefactoringProvider> providers =
//                types
//                    .Where(type => !type.IsInterface
//                            && !type.IsAbstract
//                            && !type.ContainsGenericParameters) //TODO: handle providers with generic params 
//                    .Select(type => (CodeRefactoringProvider)Activator.CreateInstance(type));

//            return providers;
//        }
//    }
//#endif
//}
