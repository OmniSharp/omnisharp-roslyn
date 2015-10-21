// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Extensions.ContextQuery;

namespace OmniSharp.Intellisense
{
    internal class YieldKeywordRecommender : AbstractSyntacticSingleKeywordRecommender
    {
        public YieldKeywordRecommender()
            : base(SyntaxKind.YieldKeyword)
        {
        }

        protected override bool IsValidContext(int position, CSharpSyntaxContext context, CancellationToken cancellationToken)
        {
            return context.IsStatementContext;
        }
    }
}
