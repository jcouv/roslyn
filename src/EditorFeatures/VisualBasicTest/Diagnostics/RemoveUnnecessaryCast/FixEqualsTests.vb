' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.VisualBasic.CodeFixes.FixEquals

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.Diagnostics.FixEquals
    Partial Public Class FixEqualsTests
        Inherits AbstractVisualBasicDiagnosticProviderBasedUserDiagnosticTest

        Friend Overrides Function CreateDiagnosticProviderAndFixer(workspace As Workspace) As (DiagnosticAnalyzer, CodeFixProvider)
            Return (Nothing,
                    New FixEqualsCodeFixProvider())
        End Function

        <Fact>
        Public Async Function TestParenthesizeToKeepParseTheSame1() As Task
            Dim markup =
<File><![CDATA[
Imports System
 
Class TypeSymbol
    Sub Main(x As TypeSymbol)
        If [|x <> x|] Then
        End If
    End Sub

    <Obsolete("obsolete", True)>
    Public Overloads Shared Operator =(left As TypeSymbol, right As TypeSymbol) As Boolean
        Return True
    End Operator

    <Obsolete("obsolete", True)>
    Public Overloads Shared Operator <>(left As TypeSymbol, right As TypeSymbol) As Boolean
        Return True
    End Operator

End Class
]]></File>

            Dim expected =
<File><![CDATA[
Imports System
 
Class TypeSymbol
    Sub Main(x As TypeSymbol)
        If Not TypeSymbol.Equals(x, x, TypeCompareKind.ConsiderEverything2) Then
        End If
    End Sub

    <Obsolete("obsolete", True)>
    Public Overloads Shared Operator =(left As TypeSymbol, right As TypeSymbol) As Boolean
        Return True
    End Operator

    <Obsolete("obsolete", True)>
    Public Overloads Shared Operator <>(left As TypeSymbol, right As TypeSymbol) As Boolean
        Return True
    End Operator

End Class
]]></File>

            Await TestAsync(markup, expected)
        End Function

    End Class
End Namespace
