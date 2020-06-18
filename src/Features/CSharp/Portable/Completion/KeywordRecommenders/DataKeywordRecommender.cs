// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Extensions.ContextQuery;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.CSharp.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Completion.KeywordRecommenders
{
    internal class DataKeywordRecommender : AbstractSyntacticSingleKeywordRecommender
    {
        public DataKeywordRecommender()
            : base(SyntaxKind.DataKeyword)
        {
        }

        public static readonly ISet<SyntaxKind> RecordTypeDeclarations = new HashSet<SyntaxKind>(SyntaxFacts.EqualityComparer)
        {
            SyntaxKindEx.RecordDeclaration,
        };

        protected override bool IsValidContext(int position, CSharpSyntaxContext context, CancellationToken cancellationToken)
        {
            if (context.IsMemberDeclarationContext(
                validModifiers: SyntaxKindSet.AllMemberModifiers,
                validTypeDeclarations: RecordTypeDeclarations,
                canBePartial: false,
                cancellationToken: cancellationToken))
            {
                return true;
            }

            return false;
        }
    }
}
