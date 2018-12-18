Imports System.Collections.Immutable
Imports System.Composition
Imports System.Threading
Imports Microsoft.CodeAnalysis.CodeActions
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeAnalysis.Editing
Imports Microsoft.CodeAnalysis.Simplification

Namespace Microsoft.CodeAnalysis.VisualBasic.CodeFixes.FixEquals
    <ExportCodeFixProvider(LanguageNames.VisualBasic, Name:=PredefinedCodeFixProviderNames.FixEquals), [Shared]>
    Friend Class FixEqualsCodeFixProvider
        Inherits SyntaxEditorBasedCodeFixProvider

        Public NotOverridable Overrides ReadOnly Property FixableDiagnosticIds As ImmutableArray(Of String)
            Get
                Return ImmutableArray.Create("BC30668")
            End Get
        End Property

        Public Overrides Async Function RegisterCodeFixesAsync(ByVal context As CodeFixContext) As Task
            Dim diagnostic = context.Diagnostics.First()
            Dim root = Await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(False)
            Dim node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie:=True)
            context.RegisterCodeFix(New MyCodeAction(Function(c) FixAsync(context.Document, diagnostic, c)), context.Diagnostics)
        End Function

        Protected Overrides Function FixAllAsync(ByVal document As Document, ByVal diagnostics As ImmutableArray(Of Diagnostic), ByVal editor As SyntaxEditor, ByVal cancellationToken As CancellationToken) As Task
            Dim root = editor.OriginalRoot

            For Each diagnostic In diagnostics
                Dim node = diagnostic.Location.FindNode(getInnermostNodeForTie:=True, cancellationToken)
                FixEquals(document, editor, node)
            Next

            Return Task.CompletedTask
        End Function

        Private Shared Sub FixEquals(ByVal document As Document, ByVal editor As SyntaxEditor, ByVal node As SyntaxNode)
            Dim comparisonToFix = TryGetComparisonToFix(node)

            If comparisonToFix IsNot Nothing Then
                Dim fixedComparison As ExpressionSyntax =
                    SyntaxFactory.InvocationExpression(
                    SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("TypeSymbol"), SyntaxFactory.IdentifierName("Equals")),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(New ArgumentSyntax() {
                        SyntaxFactory.SimpleArgument(comparisonToFix.Left),
                        SyntaxFactory.SimpleArgument(comparisonToFix.Right),
                        SyntaxFactory.SimpleArgument(SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("TypeCompareKind"), SyntaxFactory.IdentifierName("ConsiderEverything2")))})))

                If comparisonToFix.OperatorToken.IsKind(SyntaxKind.LessThanGreaterThanToken) Then
                    fixedComparison = SyntaxFactory.NotExpression(fixedComparison)
                End If

                fixedComparison = fixedComparison.WithTriviaFrom(comparisonToFix)
                fixedComparison = fixedComparison.WithAdditionalAnnotations(Simplifier.Annotation)
                editor.ReplaceNode(comparisonToFix, fixedComparison)
            End If
        End Sub

        Private Shared Function TryGetComparisonToFix(ByVal node As SyntaxNode) As BinaryExpressionSyntax
            If Not node.IsKind(SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression) Then
                Return Nothing
            End If

            Return CType(node, BinaryExpressionSyntax)
        End Function

        Private Class MyCodeAction
            Inherits CodeAction.DocumentChangeAction

            Public Sub New(ByVal createChangedDocument As Func(Of CancellationToken, Task(Of Document)))
                MyBase.New("Fix Equals", createChangedDocument, "Fix Equals")
            End Sub
        End Class
    End Class
End Namespace
