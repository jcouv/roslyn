// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Diagnostics.ParenthesizeConditional
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = PredefinedCodeFixProviderNames.ParenthesizeConditional), Shared]
    [ExtensionOrder(After = PredefinedCodeFixProviderNames.AddAwait)]
    internal sealed class CSharpParenthesizeConditionalCodeFixProvider : SyntaxEditorBasedCodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds
            => ImmutableArray.Create("CS8361");
        // CS8361: A conditional expression cannot be used directly in a string interpolation because the ':' ends the interpolation. Parenthesize the conditional expression.

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            var root = await context.Document.GetSyntaxRootAsync().ConfigureAwait(false);
            if (GetDirectlyContainingInterpolationPart(root, diagnostic) != null)
            {
                context.RegisterCodeFix(
                    new MyCodeAction(c => FixAsync(context.Document, diagnostic, c)),
                    context.Diagnostics);
            }
        }

        InterpolationSyntax GetDirectlyContainingInterpolationPart(SyntaxNode root, Diagnostic diagnostic)
        {
            var ternary = root.FindNode(diagnostic.Location.SourceSpan);
            var parent = ternary.Parent;
            if (parent.IsKind(SyntaxKind.Interpolation))
            {
                var interpolation = (InterpolationSyntax)parent;
                if (!interpolation.FormatClause.FormatStringToken.ValueText.Contains(':'))
                {
                    return interpolation;
                }
            }

            return null;
        }

        protected override Task FixAllAsync(
            Document document, ImmutableArray<Diagnostic> diagnostics,
            SyntaxEditor editor, CancellationToken cancellationToken)
        {
            var root = editor.OriginalRoot;
            foreach (var diagnostic in diagnostics)
            {
                var interpolation = GetDirectlyContainingInterpolationPart(root, diagnostic);

                if (interpolation != null)
                {
                    editor.ReplaceNode(interpolation, (currentNode, _) =>
                    {
                        var currentInterpolation = (InterpolationSyntax)currentNode;
                        var ternary = (ConditionalExpressionSyntax)currentInterpolation.Expression;

                        var newTernary = ternary
                            .WithColonToken(SyntaxFactory.Token(SyntaxKind.ColonToken))
                            .WithWhenFalse(SyntaxFactory.ParseExpression(interpolation.FormatClause.FormatStringToken.ValueText));

                        return currentInterpolation
                            .WithFormatClause(default)
                            .WithExpression(SyntaxFactory.ParenthesizedExpression(newTernary));
                    });
                }
            }

            return Task.CompletedTask;
        }

        private sealed class MyCodeAction : CodeAction.DocumentChangeAction
        {
            public MyCodeAction(Func<CancellationToken, Task<Document>> createChangedDocument) :
                base(FeaturesResources.Parenthesize_conditional_expression, createChangedDocument, FeaturesResources.Parenthesize_conditional_expression)
            {
            }
        }
    }
}
