// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.CSharp.CodeRefactorings.PossiblyDeclareAsNullable
{
    // old
    //[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = PredefinedCodeRefactoringProviderNames.PossiblyDeclareAsNullable), Shared]
    //internal class PossiblyDeclareAsNullableCodeRefactoringProvider : CodeRefactoringProvider
    //{
    //    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    //    {
    //        var document = context.Document;
    //        var textSpan = context.Span;
    //        var cancellationToken = context.CancellationToken;
    //        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

    //        var symbolToFix = await TryGetSymbolToFixAsync(document, root, textSpan, cancellationToken).ConfigureAwait(false);
    //        if (symbolToFix == null ||
    //            symbolToFix.Locations.Length != 1 ||
    //            !symbolToFix.IsNonImplicitAndFromSource())
    //        {
    //            return;
    //        }

    //        if (!IsFixableType(symbolToFix))
    //        {
    //            return;
    //        }

    //        var declarationLocation = symbolToFix.Locations[0];
    //        var node = declarationLocation.FindNode(getInnermostNodeForTie: true, cancellationToken);

    //        var typeToFix = TryGetTypeToFix(node);
    //        if (typeToFix == null || typeToFix is NullableTypeSyntax)
    //        {
    //            return;
    //        }

    //        context.RegisterRefactoring(
    //            new MyCodeAction(
    //                CSharpFeaturesResources.Declare_as_nullable,
    //                c => UpdateDocumentAsync(document, typeToFix, c)));
    //    }

    //    private static async Task<Document> UpdateDocumentAsync(Document document, TypeSyntax typeToFix, CancellationToken cancellationToken)
    //    {
    //        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

    //        var fixedType = SyntaxFactory.NullableType(typeToFix.WithoutTrivia()).WithTriviaFrom(typeToFix);
    //        var newRoot = root.ReplaceNode(typeToFix, fixedType);

    //        return document.WithSyntaxRoot(newRoot);
    //    }

    //    private class MyCodeAction : CodeAction.DocumentChangeAction
    //    {
    //        public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument) :
    //            base(title, createChangedDocument)
    //        {
    //        }
    //    }
    //}
}
