// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Simplification;

namespace Microsoft.CodeAnalysis.CSharp.InlineMethod
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp,
        Name = PredefinedCodeRefactoringProviderNames.InlineMethod), Shared]
    internal partial class InlineMethodCodeRefactoringProvider : CodeRefactoringProvider
    {
        internal static readonly SyntaxAnnotation DefinitionAnnotation = new SyntaxAnnotation();
        internal static readonly SyntaxAnnotation InvocationAnnotation = new SyntaxAnnotation();
        internal static readonly SyntaxAnnotation ThisAnnotation = new SyntaxAnnotation();

        public InlineMethodCodeRefactoringProvider()
        {
        }

        /// <summary>
        /// Converse of ExtractMethod. Look at IntroduceLocalDeclarationIntoBlockAsync for ideas (FindMatches helper, ComplexifyParentStatements)
        /// See InlineTemporary for ideas.
        /// https://github.com/dotnet/roslyn/issues/22052
        ///
        /// Pri0:
        /// Ensure expression in method body contains no locals
        /// Declaration is expression bodied, versus single expression, versus single return, versus more complicated
        /// Call site is an expression vs. an expression statement with just an invocation vs. a method group conversion
        ///
        /// Pri1:
        /// Parameters, including ref/out/in/params/optional
        /// Locals should be introduced for the parameters to the original function.
        ///     But I think it's useful to offer an option that doesn't add locals for parameters
        ///
        /// Pri2:
        /// Complexification/simplification needs to be applied to avoid conflicts since this refactoring can potentially affect a large code base.
        /// Lambdas?
        /// Invoke from a method usage?
        /// Local functions
        /// Identifiers in the method body need to be renamed if they conflict with any identifiers in the target method body.
        /// Methods with locals
        /// </summary>
        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            if (context.Span.Length > 0)
            {
                return;
            }

            var position = context.Span.Start;
            var document = context.Document;
            var cancellationToken = context.CancellationToken;

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var node = root.FindToken(position).Parent;
            if (node is null)
            {
                return;
            }

            var methodDeclaration = node as MethodDeclarationSyntax;
            if (methodDeclaration is null || !methodDeclaration.GetNameToken().Span.Contains(position))
            {
                return;
            }

            var body = methodDeclaration.Body;
            if (body is null ||
                !methodDeclaration.ReturnType.IsVoid())
            {
                return;
            }

            //var parameters = methodDeclaration.ParameterList;
            //if (parameters == null ||
            //    parameters.Parameters.Count != 0)
            //{
            //    return;
            //}

            var typeParameters = methodDeclaration.TypeParameterList;
            if (typeParameters != null)
            {
                return;
            }

            if (!IsBodyBlockWithSingleExpression(body, out var expression))
            {
                return;
            }

            if (!IsSimple(expression))
            {
                return;
            }

            var references = await GetReferencesAsync(document, methodDeclaration, cancellationToken).ConfigureAwait(false);
            if (!references.Any())
            {
                return;
            }

            context.RegisterRefactoring(
                new MyCodeAction(
                    CSharpFeaturesResources.Inline_simple_method,
                    c => this.InlineMethodAsync(document, methodDeclaration, c)));
        }

        /// <summary>
        /// Checks that the expression is simple, ie. that it doesn't use `base` or locals.
        /// </summary>
        public static bool IsSimple(SyntaxNode node)
        {
            if (node.IsKind(SyntaxKind.BaseExpression, SyntaxKind.DeclarationExpression, SyntaxKind.DeclarationPattern))
            {
                return false;
            }

            return node.ChildNodes().All(n => IsSimple(n));
        }

        private bool IsBodyBlockWithSingleExpression(BlockSyntax body, out ExpressionSyntax expression)
        {
            expression = null;
            if (body.Statements.Count != 1)
            {
                return false;
            }

            var statement = body.Statements[0];
            if (!statement.IsKind(SyntaxKind.ExpressionStatement))
            {
                return false;
            }

            expression = ((ExpressionStatementSyntax)statement).Expression;
            return true;
        }

        private async Task<Document> InlineMethodAsync(Document document, MethodDeclarationSyntax declaration, CancellationToken cancellationToken)
        {
            var workspace = document.Project.Solution.Workspace;

            // Annotate the method declaration so that we can get back to it later.
            document = await document.ReplaceNodeAsync(declaration, declaration.WithAdditionalAnnotations(DefinitionAnnotation), cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            declaration = await FindDeclarationAsync(document, cancellationToken).ConfigureAwait(false);

            // Create the expression that we're actually going to inline.
            _ = IsBodyBlockWithSingleExpression(declaration.Body, out var bodyExpression);
            var expressionToInline = Simplifier.Expand(bodyExpression, semanticModel, workspace, cancellationToken: cancellationToken);

            document = await document.ReplaceNodeAsync(bodyExpression, expressionToInline, cancellationToken).ConfigureAwait(false);
            declaration = await FindDeclarationAsync(document, cancellationToken).ConfigureAwait(false);
            _ = IsBodyBlockWithSingleExpression(declaration.Body, out expressionToInline);
            semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            // Create annotations for parameter declarations
            // TODO

            // Annotate `this`, parameter references
            expressionToInline = AnnotationRewriter.Visit(semanticModel, expressionToInline, cancellationToken);

            // Collect the references.
            var method = semanticModel.GetDeclaredSymbol(declaration, cancellationToken);
            var symbolRefs = await SymbolFinder.FindReferencesAsync(method, document.Project.Solution, cancellationToken).ConfigureAwait(false);
            var references = symbolRefs.Single(r => r.Definition == method).Locations;

            // Collect the topmost parenting expression for each reference.
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var callSites = references
                .Select(loc => syntaxRoot.FindToken(loc.Location.SourceSpan.Start).Parent.Parent)
                .OfType<InvocationExpressionSyntax>();

            // TODO: handle a reference that isn't an invocation

            // Add InvocationAnnotations to identifier nodes being replaced.
            document = await document.ReplaceNodesAsync(
                callSites,
                (o, n) => n.WithAdditionalAnnotations(InvocationAnnotation),
                cancellationToken).ConfigureAwait(false);

            semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            declaration = await FindDeclarationAsync(document, cancellationToken).ConfigureAwait(false);

            // Get the annotated reference nodes.
            callSites = await FindReferenceAnnotatedNodesAsync(document, cancellationToken).ConfigureAwait(false);

            var topmostParentingExpressions = callSites
                .Select(ident => GetTopMostParentingExpression(ident))
                .Distinct();

            // Make each topmost parenting expression semantically explicit.
            document = await document.ReplaceNodesAsync(topmostParentingExpressions, (o, n) => Simplifier.Expand(n, semanticModel, workspace, cancellationToken: cancellationToken), cancellationToken).ConfigureAwait(false);
            semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            //var semanticModelBeforeInline = semanticModel;
            declaration = await FindDeclarationAsync(document, cancellationToken).ConfigureAwait(false);

            callSites = await FindReferenceAnnotatedNodesAsync(document, cancellationToken).ConfigureAwait(false);

            // TODO: deal with multiple call-sites/scopes
            var callSite = callSites.Single();
            var newCallSite = InvocationRewriter.Visit(semanticModel, callSite, declaration, expressionToInline, cancellationToken);

            document = await document.ReplaceNodeAsync(callSite, newCallSite, cancellationToken).ConfigureAwait(false);
            semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            declaration = await FindDeclarationAsync(document, cancellationToken).ConfigureAwait(false);

            // TODO: deal with conflicts annotated by the InvocationRewriter

            // No semantic conflicts, we can remove the declaration.
            document = await document.ReplaceNodeAsync(declaration.Parent, RemoveDeclarationFromScope(declaration, declaration.Parent), cancellationToken).ConfigureAwait(false);

            return document;
        }

        //private async Task<ExpressionSyntax> CreateExpressionToInlineAsync(
        //    MethodDeclarationSyntax methodDeclaration,
        //    Document document,
        //    CancellationToken cancellationToken)
        //{
        //    var updatedDocument = document;

        //    var expression = SkipRedundantExteriorParentheses(variableDeclarator.Initializer.Value);
        //    var semanticModel = await updatedDocument.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        //    var localSymbol = (ILocalSymbol)semanticModel.GetDeclaredSymbol(variableDeclarator, cancellationToken);
        //    var newExpression = InitializerRewriter.Visit(expression, localSymbol, semanticModel);

        //    // If this is an array initializer, we need to transform it into an array creation
        //    // expression for inlining.
        //    if (newExpression.Kind() == SyntaxKind.ArrayInitializerExpression)
        //    {
        //        var arrayType = (ArrayTypeSyntax)localSymbol.Type.GenerateTypeSyntax();
        //        var arrayInitializer = (InitializerExpressionSyntax)newExpression;

        //        // Add any non-whitespace trailing trivia from the equals clause to the type.
        //        var equalsToken = variableDeclarator.Initializer.EqualsToken;
        //        if (equalsToken.HasTrailingTrivia)
        //        {
        //            var trailingTrivia = equalsToken.TrailingTrivia.SkipInitialWhitespace();
        //            if (trailingTrivia.Any())
        //            {
        //                arrayType = arrayType.WithTrailingTrivia(trailingTrivia);
        //            }
        //        }

        //        newExpression = SyntaxFactory.ArrayCreationExpression(arrayType, arrayInitializer);
        //    }

        //    newExpression = newExpression.WithAdditionalAnnotations(InitializerAnnotation);

        //    updatedDocument = await updatedDocument.ReplaceNodeAsync(variableDeclarator.Initializer.Value, newExpression, cancellationToken).ConfigureAwait(false);
        //    semanticModel = await updatedDocument.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        //    newExpression = await FindInitializerAsync(updatedDocument, cancellationToken).ConfigureAwait(false);
        //    var newVariableDeclarator = await FindDeclaratorAsync(updatedDocument, cancellationToken).ConfigureAwait(false);
        //    localSymbol = (ILocalSymbol)semanticModel.GetDeclaredSymbol(newVariableDeclarator, cancellationToken);

        //    var explicitCastExpression = newExpression.CastIfPossible(localSymbol.Type, newVariableDeclarator.SpanStart, semanticModel);
        //    if (explicitCastExpression != newExpression)
        //    {
        //        updatedDocument = await updatedDocument.ReplaceNodeAsync(newExpression, explicitCastExpression, cancellationToken).ConfigureAwait(false);
        //        semanticModel = await updatedDocument.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        //        newVariableDeclarator = await FindDeclaratorAsync(updatedDocument, cancellationToken).ConfigureAwait(false);
        //    }

        //    // Now that the variable declarator is normalized, make its initializer
        //    // value semantically explicit.
        //    newExpression = await Simplifier.ExpandAsync(newVariableDeclarator.Initializer.Value, updatedDocument, cancellationToken: cancellationToken).ConfigureAwait(false);
        //    return newExpression.WithAdditionalAnnotations(ExpressionToInlineAnnotation);
        //}

        private static SyntaxNode GetTopMostParentingExpression(ExpressionSyntax expression)
        {
            return expression.AncestorsAndSelf().OfType<ExpressionSyntax>().Last();
        }

        private static Task<MethodDeclarationSyntax> FindDeclarationAsync(Document document, CancellationToken cancellationToken)
        {
            return document.FindNodeWithAnnotationAsync<MethodDeclarationSyntax>(DefinitionAnnotation, cancellationToken);
        }

        private MethodDeclarationSyntax FindDeclaration(SyntaxNode node)
        {
            var annotatedNodesOrTokens = node.GetAnnotatedNodesAndTokens(DefinitionAnnotation).ToList();
            Contract.Requires(annotatedNodesOrTokens.Count == 1, "Only a single method declaration should have been annotated.");

            return (MethodDeclarationSyntax)annotatedNodesOrTokens.First().AsNode();
        }

        private static async Task<IEnumerable<InvocationExpressionSyntax>> FindReferenceAnnotatedNodesAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            return FindReferenceAnnotatedNodes<InvocationExpressionSyntax>(root, InvocationAnnotation);
        }

        // TODO: factor this method out of InlineTemporaryCodeRefactoringProvider
        private static IEnumerable<T> FindReferenceAnnotatedNodes<T>(SyntaxNode root, SyntaxAnnotation annotation)
        {
            var annotatedNodesAndTokens = root.GetAnnotatedNodesAndTokens(annotation);
            foreach (var nodeOrToken in annotatedNodesAndTokens)
            {
                if (nodeOrToken.IsNode && nodeOrToken.AsNode() is T node)
                {
                    yield return node;
                }
            }
        }

        private async Task<IEnumerable<ReferenceLocation>> GetReferencesAsync(
            Document document,
            MethodDeclarationSyntax methodDeclaration,
            CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var method = semanticModel.GetDeclaredSymbol(methodDeclaration, cancellationToken);

            if (method != null)
            {
                var findReferencesResult = await SymbolFinder.FindReferencesAsync(method, document.Project.Solution, cancellationToken).ConfigureAwait(false);
                var locations = findReferencesResult.Single(r => r.Definition == method).Locations;
                if (!locations.Any(loc => semanticModel.SyntaxTree.OverlapsHiddenPosition(loc.Location.SourceSpan, cancellationToken)))
                {
                    return locations;
                }
            }

            return ImmutableArray<ReferenceLocation>.Empty;
        }

        private SyntaxNode RemoveDeclarationFromScope(MethodDeclarationSyntax methodDeclaration, SyntaxNode scope)
        {
            //var localDeclaration = (LocalDeclarationStatementSyntax)variableDeclaration.Parent;

            //// There's only one variable declarator, so we'll remove the local declaration
            //// statement entirely. This means that we'll concatenate the leading and trailing
            //// trivia of this declaration and move it to the next statement.
            //var leadingTrivia = localDeclaration
            //    .GetLeadingTrivia()
            //    .Reverse()
            //    .SkipWhile(t => t.MatchesKind(SyntaxKind.WhitespaceTrivia))
            //    .Reverse()
            //    .ToSyntaxTriviaList();

            //var trailingTrivia = localDeclaration
            //    .GetTrailingTrivia()
            //    .SkipWhile(t => t.MatchesKind(SyntaxKind.WhitespaceTrivia, SyntaxKind.EndOfLineTrivia))
            //    .ToSyntaxTriviaList();

            //var newLeadingTrivia = leadingTrivia.Concat(trailingTrivia);

            //var nextToken = localDeclaration.GetLastToken().GetNextTokenOrEndOfFile();
            //var newNextToken = nextToken.WithPrependedLeadingTrivia(newLeadingTrivia)
            //                            .WithAdditionalAnnotations(Formatter.Annotation);

            //var newScope = scope.ReplaceToken(nextToken, newNextToken);

            return scope.RemoveNode(methodDeclaration, SyntaxRemoveOptions.KeepNoTrivia);
        }

        //private Task<Document> UpdateDocumentAsync(
        //    Document document, SyntaxNode root, SyntaxNode declaration,
        //    OptionSet options, UseExpressionBodyHelper helper, bool useExpressionBody,
        //    CancellationToken cancellationToken)
        //{
        //    var parseOptions = root.SyntaxTree.Options;
        //    var updatedDeclaration = helper.Update(declaration, options, parseOptions, useExpressionBody);

        //    var parent = declaration is AccessorDeclarationSyntax
        //        ? declaration.Parent
        //        : declaration;
        //    var updatedParent = parent.ReplaceNode(declaration, updatedDeclaration)
        //                              .WithAdditionalAnnotations(Formatter.Annotation);

        //    var newRoot = root.ReplaceNode(parent, updatedParent);
        //    return Task.FromResult(document.WithSyntaxRoot(newRoot));
        //}

        private class MyCodeAction : CodeAction.DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument)
                : base(title, createChangedDocument)
            {
            }
        }
    }
}

namespace Microsoft.CodeAnalysis.CSharp.InlineMethod
{
    internal static class SyntaxNodeExtensions
    {
        // TODO: a number of existing places could use this extension
        public static T WithConflictAnnotation<T>(this T node) where T : SyntaxNode
        {
            return node.WithAdditionalAnnotations(ConflictAnnotation.Create(CSharpFeaturesResources.Conflict_s_detected));
        }
    }

    internal partial class InlineMethodCodeRefactoringProvider
    {
        /// <summary>
        /// Rewrites invocations of a given method using an inlined expression
        /// </summary>
        private class InvocationRewriter : CSharpSyntaxRewriter
        {
            private readonly SemanticModel _semanticModel;
            private readonly IMethodSymbol _methodSymbol;
            private readonly MethodDeclarationSyntax _methodDeclaration;
            private readonly ExpressionSyntax _expressionToInline;
            private readonly CancellationToken _cancellationToken;

            private InvocationRewriter(
                SemanticModel semanticModel,
                MethodDeclarationSyntax methodDeclaration,
                ExpressionSyntax expressionToInline,
                CancellationToken cancellationToken)
            {
                _semanticModel = semanticModel;
                _methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration, cancellationToken);
                _methodDeclaration = methodDeclaration;
                _expressionToInline = expressionToInline;
                _cancellationToken = cancellationToken;
            }

            private bool IsReference(InvocationExpressionSyntax invocation)
            {
                if (GetMethodIdentifier(invocation.Expression).ValueText != _methodDeclaration.Identifier.ValueText)
                {
                    return false;
                }

                var symbol = _semanticModel.GetSymbolInfo(invocation).Symbol;
                return symbol?.Equals(_methodSymbol) == true;
            }

            private SyntaxToken GetMethodIdentifier(ExpressionSyntax expression)
            {
                if (expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                {
                    return ((MemberAccessExpressionSyntax)expression).Name.Identifier;
                }
                if (expression.IsKind(SyntaxKind.IdentifierName))
                {
                    return ((IdentifierNameSyntax)expression).GetNameToken();
                }
                return default;
            }

            public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                if (IsReference(node))
                {
                    // TODO deal with conflicts
                    //if (HasConflict(node, _methodDeclaration))
                    //{
                    //    return node.Update(node.Identifier.WithAdditionalAnnotations(CreateConflictAnnotation()));
                    //}

                    return _expressionToInline
                        .Parenthesize()
                        .WithAdditionalAnnotations(Formatter.Annotation, Simplifier.Annotation);
                }

                return base.VisitInvocationExpression(node);
            }

            public static SyntaxNode Visit(
                SemanticModel semanticModel,
                SyntaxNode scope,
                MethodDeclarationSyntax methodDeclaration,
                ExpressionSyntax expressionToInline,
                CancellationToken cancellationToken)
            {
                var rewriter = new InvocationRewriter(semanticModel, methodDeclaration, expressionToInline, cancellationToken);
                return rewriter.Visit(scope);
            }
        }

        /// <summary>
        /// Annotate `this`, parameter references
        /// </summary>
        private class AnnotationRewriter : CSharpSyntaxRewriter
        {
            private readonly SemanticModel _semanticModel;
            private readonly CancellationToken _cancellationToken;

            private AnnotationRewriter(
                SemanticModel semanticModel,
                CancellationToken cancellationToken)
            {
                _semanticModel = semanticModel;
                _cancellationToken = cancellationToken;
            }

            public override SyntaxNode VisitThisExpression(ThisExpressionSyntax node)
            {
                return node.WithAdditionalAnnotations(ThisAnnotation);
            }

            public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
            {
                var symbol = _semanticModel.GetSymbolInfo(node).Symbol;
                if (symbol?.IsKind(SymbolKind.Parameter) == true)
                {
                    return node.WithConflictAnnotation();
                }

                return node;
            }

            public static ExpressionSyntax Visit(
                SemanticModel semanticModel,
                ExpressionSyntax expressionToInline,
                CancellationToken cancellationToken)
            {
                // TODO: this method should probably accept a map of parameter symbol to annotation

                var rewriter = new AnnotationRewriter(semanticModel, cancellationToken);
                return (ExpressionSyntax)rewriter.Visit(expressionToInline);
            }
        }
    }
}

