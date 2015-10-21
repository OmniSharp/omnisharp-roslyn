//// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
//
//using System.Threading;
//using Microsoft.CodeAnalysis.CSharp.Extensions.ContextQuery;
//using Microsoft.CodeAnalysis.CSharp;
//
//namespace OmniSharp.Intellisense
//{
//    internal class WhenKeywordRecommender : AbstractSyntacticSingleKeywordRecommender
//    {
//        public WhenKeywordRecommender()
//			: base(SyntaxKind.WhenKeyword, isValidInPreprocessorContext: true)
//        {
//        }
//
//        protected override bool IsValidContext(int position, CSharpSyntaxContext context, CancellationToken cancellationToken)
//        {
//            return context.IsCatchFilterContext;
//        }
//    }
//}
