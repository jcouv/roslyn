// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Simplification;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.CSharp.AvoidVarStackalloc
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal sealed class CSharpAvoidVarStackallocDiagnosticAnalyzer : AbstractCodeStyleDiagnosticAnalyzer
    {
        public CSharpAvoidVarStackallocDiagnosticAnalyzer()
            : base(IDEDiagnosticIds.AvoidVarStackallocDiagnosticId,
                   new LocalizableResourceString(nameof(FeaturesResources.Use_inferred_member_name), FeaturesResources.ResourceManager, typeof(FeaturesResources)),
                   new LocalizableResourceString(nameof(FeaturesResources.Member_name_can_be_simplified), FeaturesResources.ResourceManager, typeof(FeaturesResources)))
        {
            // TODO fix messages

        }

        public override DiagnosticAnalyzerCategory GetAnalyzerCategory()
            => DiagnosticAnalyzerCategory.SemanticSpanAnalysis;

        public override bool OpenFileOnly(Workspace workspace)
            => false;

        protected override void InitializeWorker(AnalysisContext context)
            => context.RegisterSyntaxNodeAction(AnalyzeSyntax, SyntaxKind.StackAllocArrayCreationExpression);

        private void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
        {
            var cancellationToken = context.CancellationToken;

            var node = context.Node;
            var syntaxTree = node.SyntaxTree;
            var optionSet = context.Options.GetDocumentOptionSetAsync(syntaxTree, cancellationToken).GetAwaiter().GetResult();
            if (optionSet == null)
            {
                return;
            }

            var localDeclaration = node.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>();
            if (localDeclaration is null)
            {
                return;
            }

            //var parseOptions = (CSharpParseOptions)syntaxTree.Options;
            //if (!optionSet.GetOption(CSharpCodeStyleOptions.PreferInferredTupleNames).Value)
            //{
            //    return;
            //}

            context.ReportDiagnostic(Diagnostic.Create(WarningDescriptor, node.GetLocation()));
        }
    }
}
