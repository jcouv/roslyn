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
        public CSharpPossiblyDeclareAsNullableCodeFixProvider()
        {
        }

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(IDEDiagnosticIds.PossiblyDeclareAsNullableDiagnosticId);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

            var symbolToFix = await IsFixableAsync(context.Document, null, node, null, context.CancellationToken).ConfigureAwait(false);
            if (symbolToFix is object)
            {
                context.RegisterCodeFix(new MyCodeAction(
                    c => FixAsync(context.Document, diagnostic, c)),
                    context.Diagnostics);
            }
        }

        //protected override async Task FixAllAsync(
        //    Document document, ImmutableArray<Diagnostic> diagnostics,
        //    SyntaxEditor editor, CancellationToken cancellationToken)
        //{
        //    var root = editor.OriginalRoot;

        //    // a method can have multiple `return null;` statements, but we should only fix its return type once
        //    var alreadyHandled = PooledHashSet<TypeSyntax>.GetInstance();

        //    foreach (var diagnostic in diagnostics)
        //    {
        //        var node = diagnostic.Location.FindNode(getInnermostNodeForTie: true, cancellationToken);
        //        await MakeDeclarationNullableAsync(document, editor, node, alreadyHandled, cancellationToken).ConfigureAwait(false);
        //    }

        //    alreadyHandled.Free();
        //}

        private static async Task<ISymbol> IsFixableAsync(Document document, SyntaxEditor editor, SyntaxNode node, HashSet<TypeSyntax> alreadyHandled, CancellationToken cancellationToken)
        {
            var symbolToFix = await TryGetSymbolToFixAsync(document, root, textSpan, cancellationToken).ConfigureAwait(false);
            if (symbolToFix == null ||
                symbolToFix.Locations.Length != 1 ||
                !symbolToFix.IsNonImplicitAndFromSource())
            {
                return null;
            }

            if (!IsFixableType(symbolToFix))
            {
                return null;
            }

            return symbolToFix;
        }

        private static async Task MakeDeclarationNullableAsync(Document document, SyntaxEditor editor, SyntaxNode node, HashSet<TypeSyntax> alreadyHandled, CancellationToken cancellationToken)
        {
            //var declarationTypeToFix = TryGetDeclarationTypeToFix(node);
            //if (declarationTypeToFix != null && alreadyHandled.Add(declarationTypeToFix))
            //{
            //    var fixedDeclaration = SyntaxFactory.NullableType(declarationTypeToFix.WithoutTrivia()).WithTriviaFrom(declarationTypeToFix);
            //    editor.ReplaceNode(declarationTypeToFix, fixedDeclaration);
            //}

            var textSpan = context.Span;
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var symbolToFix = await IsFixableAsync(document, editor, node, alreadyHandled, cancellationToken).ConfigureAwait(false);
            if (symbolToFix == null)
            {
                return;
            }

            var declarationLocation = symbolToFix.Locations[0];
            var node = declarationLocation.FindNode(getInnermostNodeForTie: true, cancellationToken);

            var typeToFix = TryGetTypeToFix(node);
            if (typeToFix == null || typeToFix is NullableTypeSyntax)
            {
                return;
            }
        }

        private static TypeSyntax TryGetTypeToFix(SyntaxNode node)
        {
            switch (node)
            {
                case ParameterSyntax parameter:
                    return parameter.Type;

                case VariableDeclaratorSyntax declarator:
                    if (declarator.IsParentKind(SyntaxKind.VariableDeclaration))
                    {
                        var declaration = (VariableDeclarationSyntax)declarator.Parent;
                        return declaration.Variables.Count == 1 ? declaration.Type : null;
                    }

                    return null;

                case PropertyDeclarationSyntax property:
                    return property.Type;

                case MethodDeclarationSyntax method:
                    if (method.Modifiers.Any(SyntaxKind.PartialKeyword))
                    {
                        // partial methods should only return void (ie. already an error scenario)
                        return null;
                    }

                    return method.ReturnType;
            }

            return null;
        }


        private static bool IsFixableType(ISymbol symbolToFix)
        {
            ITypeSymbol type = null;
            switch (symbolToFix)
            {
                case IParameterSymbol parameter:
                    type = parameter.Type;
                    break;
                case ILocalSymbol local:
                    type = local.Type;
                    break;
                case IPropertySymbol property:
                    type = property.Type;
                    break;
                case IMethodSymbol method when method.IsDefinition:
                    type = method.ReturnType;
                    break;
                case IFieldSymbol field:
                    type = field.Type;
                    break;
                default:
                    return false;
            }

            return type?.IsReferenceType == true;
        }

        private static async Task<ISymbol> TryGetSymbolToFixAsync(Document document, SyntaxNode root, TextSpan textSpan, CancellationToken cancellationToken)
        {
            var token = root.FindToken(textSpan.Start);

            if (!token.IsKind(SyntaxKind.EqualsEqualsToken, SyntaxKind.ExclamationEqualsToken, SyntaxKind.NullKeyword))
            {
                return null;
            }

            BinaryExpressionSyntax equals;
            if (token.Parent.IsKind(SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression))
            {
                equals = (BinaryExpressionSyntax)token.Parent;
            }
            else if (token.Parent.IsKind(SyntaxKind.NullLiteralExpression) && token.Parent.IsParentKind(SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression))
            {
                equals = (BinaryExpressionSyntax)token.Parent.Parent;
            }
            else
            {
                return null;
            }

            ExpressionSyntax value;
            if (equals.Right.IsKind(SyntaxKind.NullLiteralExpression))
            {
                value = equals.Left;
            }
            else if (equals.Left.IsKind(SyntaxKind.NullLiteralExpression))
            {
                value = equals.Right;
            }
            else
            {
                return null;
            }

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            return semanticModel.GetSymbolInfo(value).Symbol;
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
