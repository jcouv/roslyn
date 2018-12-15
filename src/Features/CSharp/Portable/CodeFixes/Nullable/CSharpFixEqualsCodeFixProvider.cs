// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Simplification;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.CodeFixes.DeclareAsNullable
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = PredefinedCodeFixProviderNames.DeclareAsNullable), Shared]
    internal class CSharpFixEqualsCodeFixProvider : SyntaxEditorBasedCodeFixProvider
    {
        // error CS0619
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("CS0619");

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

            //var declarationTypeToFix = TryGetDeclarationTypeToFix(node);
            //if (declarationTypeToFix == null)
            //{
            //    return;
            //}

            context.RegisterCodeFix(new MyCodeAction(
                c => FixAsync(context.Document, diagnostic, c)),
                context.Diagnostics);
        }

        protected override Task FixAllAsync(
            Document document, ImmutableArray<Diagnostic> diagnostics,
            SyntaxEditor editor, CancellationToken cancellationToken)
        {
            var root = editor.OriginalRoot;

            foreach (var diagnostic in diagnostics)
            {
                var node = diagnostic.Location.FindNode(getInnermostNodeForTie: true, cancellationToken);
                FixEquals(document, editor, node);
            }

            return Task.CompletedTask;
        }

        private static void FixEquals(Document document, SyntaxEditor editor, SyntaxNode node)
        {
            var comparisonToFix = TryGetComparisonToFix(node);
            if (comparisonToFix != null)
            {
                ExpressionSyntax fixedComparison =
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("TypeSymbol"), SyntaxFactory.IdentifierName("Equals")),
                        SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[] {
                            SyntaxFactory.Argument(comparisonToFix.Left),
                            SyntaxFactory.Argument(comparisonToFix.Right),
                            SyntaxFactory.Argument(SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("TypeCompareKind"), SyntaxFactory.IdentifierName("ConsiderEverything2")))
                        })));

                if (comparisonToFix.OperatorToken.IsKind(SyntaxKind.ExclamationEqualsToken))
                {
                    fixedComparison = SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, fixedComparison);
                }
                fixedComparison = fixedComparison.WithAdditionalAnnotations(Simplifier.Annotation);
                editor.ReplaceNode(comparisonToFix, fixedComparison);
            }
        }

        private static BinaryExpressionSyntax TryGetComparisonToFix(SyntaxNode node)
        {
            if (!node.IsKind(SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression))
            {
                return null;
            }
            return (BinaryExpressionSyntax)node;
        }

        private class MyCodeAction : CodeAction.DocumentChangeAction
        {
            public MyCodeAction(Func<CancellationToken, Task<Document>> createChangedDocument) :
                base(CSharpFeaturesResources.Declare_as_nullable,
                     createChangedDocument,
                     CSharpFeaturesResources.Declare_as_nullable)
            {
            }
        }
    }
}
