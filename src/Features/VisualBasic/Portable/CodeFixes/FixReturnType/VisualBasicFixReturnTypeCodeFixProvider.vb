' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports System.Composition
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.CodeFixes.FixReturnType
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.CodeFixes.FixReturnType
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Friend Class VisualBasicFixReturnTypeCodeFixProvider
        Inherits AbstractFixReturnTypeCodeFixProvider

        ' error BC30647: 'Return' statement in a Sub or a Set cannot return a value.
        Private ReadOnly s_diagnosticIds As ImmutableArray(Of String) = ImmutableArray.Create("BC30647")

        Protected Overrides Function GetTitle() As String
            Return VBFeaturesResources.Fix_return_type
        End Function

        Public Overrides ReadOnly Property FixableDiagnosticIds As ImmutableArray(Of String)
            Get
                Return s_diagnosticIds
            End Get
        End Property
    End Class
End Namespace
