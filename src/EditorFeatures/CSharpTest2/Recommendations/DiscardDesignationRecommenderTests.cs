// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Completion.KeywordRecommenders;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.Recommendations
{
    [Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
    public class DiscardDesignationRecommenderTests : RecommenderTests
    {
        private readonly DiscardDesignationRecommender _recommender = new DiscardDesignationRecommender();

        public DiscardDesignationRecommenderTests()
        {
            this.keywordText = "_";
            this.RecommendKeywordsAsync = (position, context) => _recommender.RecommendKeywordsAsync(position, context, CancellationToken.None);
        }

        [Fact]
        public async Task TestDiscardDesignationInAssignment()
        {
            await VerifyKeywordAsync(AddInsideMethod("int _something = 1; $$"));
        }

        [Fact]
        public async Task TestDiscardDesignationInOutVar()
        {
            await VerifyKeywordAsync(AddInsideMethod("M(out var $$"));
        }

        [Fact]
        public async Task TestDiscardDesignationInOutVarAfterComma()
        {
            await VerifyKeywordAsync(AddInsideMethod("M(x, out var $$"));
        }

        [Fact]
        public async Task TestDiscardDesignationInOutVarAfterComma2()
        {
            await VerifyKeywordAsync(AddInsideMethod("M(x, out System.Int32 $$"));
        }

        [Fact]
        public async Task TestShortDiscardDesignationInOutVar()
        {
            await VerifyKeywordAsync(AddInsideMethod("M(out $$"));
        }

        [Fact]
        public async Task TestShortDiscardDesignationInOutVarAfterComma()
        {
            await VerifyKeywordAsync(AddInsideMethod("M(x, out $$"));
        }
    }
}
