// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Completion.Providers;
using Microsoft.CodeAnalysis.CSharp.Extensions.ContextQuery;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Completion.KeywordRecommenders
{
    internal class DiscardDesignationRecommender : IKeywordRecommender<CSharpSyntaxContext>
    {
        private bool IsValidContext(CSharpSyntaxContext context, CancellationToken cancellationToken)
        {
            return context.SyntaxTree.IsPossibleDiscardContext(
               context.TargetToken.SpanStart,
               context.TargetToken,
               cancellationToken: cancellationToken);
        }

        public Task<IEnumerable<RecommendedKeyword>> RecommendKeywordsAsync(int position, CSharpSyntaxContext context, CancellationToken cancellationToken)
        {
            if (IsValidContext(context, cancellationToken))
            {
                return Task.FromResult(SpecializedCollections.SingletonEnumerable(new RecommendedKeyword("_")));
            }

            return Task.FromResult<IEnumerable<RecommendedKeyword>>(null);
        }
    }
}
