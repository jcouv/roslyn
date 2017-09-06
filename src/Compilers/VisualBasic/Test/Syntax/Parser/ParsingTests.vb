' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Globalization
Imports System.Text
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Syntax
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities

' #define PARSING_TESTS_DUMP

Public Class ParsingTests

    <Fact>
    Public Sub M()
        Dim tree = UsingTree("
Class C
End Class", options:=TestOptions.Regular)


    End Sub

    Private _treeEnumerator As IEnumerator(Of SyntaxNodeOrToken)

    Protected Function ParseTree(text As String, options As VisualBasicParseOptions) As SyntaxTree
        Return SyntaxFactory.ParseSyntaxTree(text, options)
    End Function

    ''' <summary>
    ''' Parses given string and initializes a depth-first preorder enumerator.
    ''' </summary>
    Protected Function UsingTree(text As String, Optional options As VisualBasicParseOptions = Nothing) As SyntaxTree
        Dim tree = ParseTree(text, options)
        Dim nodes = EnumerateNodes(tree.GetCompilationUnitRoot())
#If PARSING_TESTS_DUMP Then
        nodes = nodes.ToArray() ' force eval to dump contents
#End If
        _treeEnumerator = nodes.GetEnumerator()

        Return tree
    End Function

    Private Iterator Function EnumerateNodes(node As VisualBasicSyntaxNode) As IEnumerable(Of SyntaxNodeOrToken)

    End Function

End Class
