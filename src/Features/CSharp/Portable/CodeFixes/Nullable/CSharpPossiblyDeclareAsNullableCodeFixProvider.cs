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
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.CodeFixes.PossiblyDeclareAsNullable
{
    /// <summary>
    /// If you apply a null test on a symbol that isn't nullable, then we'll help you make that symbol nullable.
    /// For example: `nonNull == null`, `nonNull?.Property`
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = PredefinedCodeFixProviderNames.PossiblyDeclareAsNullable), Shared]
    internal class CSharpPossiblyDeclareAsNullableCodeFixProvider : SyntaxEditorBasedCodeFixProvider
    {
        // TODO2 consider changing the base type

        [ImportingConstructor]
        public CSharpPossiblyDeclareAsNullableCodeFixProvider() : base(supportsFixAll: false)
        {
        }

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(IDEDiagnosticIds.PossiblyDeclareAsNullableDiagnosticId);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

            var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            var symbolToFix = CSharpPossiblyDeclareAsNullableDiagnosticAnalyzer.IsFixable(node, model);

            if (symbolToFix is object)
            {
                context.RegisterCodeFix(new MyCodeAction(
                    c => FixAsync(context.Document, diagnostic, c)),
                    context.Diagnostics);
            }
        }

        protected override Task FixAllAsync(
            Document document, ImmutableArray<Diagnostic> diagnostics,
            SyntaxEditor editor, CancellationToken cancellationToken)
        {
            //var root = editor.OriginalRoot;

            //// a method can have multiple `return null;` statements, but we should only fix its return type once
            //var alreadyHandled = PooledHashSet<TypeSyntax>.GetInstance();

            //foreach (var diagnostic in diagnostics)
            //{
            //    var node = diagnostic.Location.FindNode(getInnermostNodeForTie: true, cancellationToken);
            //    await MakeDeclarationNullableAsync(document, editor, node, alreadyHandled, cancellationToken).ConfigureAwait(false);
            //}

            //alreadyHandled.Free();
            throw new NotImplementedException();
        }

        private static async Task MakeDeclarationNullableAsync(Document document, SyntaxEditor editor, SyntaxNode node, HashSet<TypeSyntax> alreadyHandled, CancellationToken cancellationToken)
        {
            //var declarationTypeToFix = TryGetDeclarationTypeToFix(node);
            //if (declarationTypeToFix != null && alreadyHandled.Add(declarationTypeToFix))
            //{
            //    var fixedDeclaration = SyntaxFactory.NullableType(declarationTypeToFix.WithoutTrivia()).WithTriviaFrom(declarationTypeToFix);
            //    editor.ReplaceNode(declarationTypeToFix, fixedDeclaration);
            //}

            //var textSpan = context.Span;
            //var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            //var symbolToFix = await CSharpPossiblyDeclareAsNullableDiagnosticAnalyzer.IsFixableAsync(document, editor, node, alreadyHandled, cancellationToken).ConfigureAwait(false);
            //if (symbolToFix == null)
            //{
            //    return;
            //}

            //var declarationLocation = symbolToFix.Locations[0];
            //var node = declarationLocation.FindNode(getInnermostNodeForTie: true, cancellationToken);

            //var typeToFix = CSharpPossiblyDeclareAsNullableDiagnosticAnalyzer.TryGetTypeToFix(node);
            //if (typeToFix == null || typeToFix is NullableTypeSyntax)
            //{
            //    return;
            //}
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
