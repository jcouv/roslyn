' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.VisualBasic.CodeFixes.FixReturnType

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.Diagnostics.FixReturnType
    Public Class FixReturnTypeTests
        Inherits AbstractVisualBasicDiagnosticProviderBasedUserDiagnosticTest

        Friend Overrides Function CreateDiagnosticProviderAndFixer(workspace As Workspace) As (DiagnosticAnalyzer, CodeFixProvider)
            Return (Nothing, New VisualBasicFixReturnTypeCodeFixProvider())
        End Function

        <WorkItem(718494, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/718494")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsCorrectFunctionReturnType)>
        Public Async Function TestAsyncFunction1() As Task
            Await TestInRegularAndScriptAsync(
"Imports System.Threading.Tasks
Module Program
    [|Async Function F()|]
        Return Nothing
    End Function
End Module",
"Imports System.Threading.Tasks
Module Program
    Async Function F() As Task
        Return Nothing
    End Function
End Module")
        End Function
    End Class
End Namespace
