// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.AddImport;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Completion.Providers;
using Microsoft.CodeAnalysis.CSharp.Extensions.ContextQuery;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Packaging;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions.ContextQuery;
using Microsoft.CodeAnalysis.SymbolSearch;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.CSharp.Completion.Providers
{
    /// <summary>
    /// This provider triggers when a known type was typed. This way Intellisense doesn't get in the way of typing `new List()` when
    /// the `List` type is not yet referenced.
    /// </summary>
    internal sealed class KnownSymbolCompletionProvider : AbstractRecommendationServiceBasedCompletionProvider
    {
        private readonly IPackageInstallerService _packageInstallerService;
        private readonly ISymbolSearchService _symbolSearchService;

        internal KnownSymbolCompletionProvider(IPackageInstallerService packageInstallerService = null, ISymbolSearchService symbolSearchService = null) : base( )
        {
            _packageInstallerService = packageInstallerService;
            _symbolSearchService = symbolSearchService;
        }

        protected override async Task<IEnumerable<CompletionItem>> GetItemsWorkerAsync(Document document, int position, OptionSet options, bool preselect, CancellationToken cancellationToken)
        {
            var symbolSearchService = _symbolSearchService ?? document.Project.Solution.Workspace.Services.GetService<ISymbolSearchService>();
            var packageSources = symbolSearchService != null ? GetPackageSources(document) : ImmutableArray<PackageSource>.Empty;

            var span = new TextSpan(position, 0);

            var addImportService = document.GetLanguageService<IAddImportFeatureService>();

            // PROTOTYPE: I think a dedicated API would work better (could get the symbol name, its namespace, its package)
            var fixes = await addImportService.GetFixesAsync(
                document, span, diagnosticId: IDEDiagnosticIds.UnboundIdentifierId,
                placeSystemNamespaceFirst: false,
                symbolSearchService, searchReferenceAssemblies: false,
                packageSources, cancellationToken).ConfigureAwait(false);

            if (fixes.IsEmpty)
            {
                return ImmutableArray<CompletionItem>.Empty;
            }

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var node = root.FindToken(span.Start, findInsideTrivia: true)
               .GetAncestor(n => n.Span.Contains(span) && n != root);

            // PROTOTOTYPE: for some reason the suggestion only appears if Intellisense is triggered explicitly

            var value = "List"; // node.ToString();
            var props = ImmutableDictionary<string, string>.Empty;
            props = props.Add("InsertionText", value);
            props = props.Add("ContextPosition", position.ToString());
            props = props.Add("SymbolName", value);
            props = props.Add("SymbolKind", ((int)SymbolKind.NamedType).ToString());

            return ImmutableArray.Create(CompletionItem.Create(value, properties: props));
        }

        private IPackageInstallerService GetPackageInstallerService(Document document)
            => _packageInstallerService ?? document.Project.Solution.Workspace.Services.GetService<IPackageInstallerService>();

        private ImmutableArray<PackageSource> GetPackageSources(Document document)
            => GetPackageInstallerService(document)?.PackageSources ?? ImmutableArray<PackageSource>.Empty;

        protected override bool IsInstrinsic(ISymbol s)
        {
            return false;
        }

        internal override bool IsInsertionTrigger(SourceText text, int characterPosition, OptionSet options)
        {
            return CompletionUtilities.IsTriggerCharacter(text, characterPosition, options);
        }

        protected override async Task<bool> IsSemanticTriggerCharacterAsync(Document document, int characterPosition, CancellationToken cancellationToken)
        {
            bool? result = await IsTriggerOnDotAsync(document, characterPosition, cancellationToken).ConfigureAwait(false);
            if (result.HasValue)
            {
                return result.Value;
            }

            return true;
        }

        private async Task<bool?> IsTriggerOnDotAsync(Document document, int characterPosition, CancellationToken cancellationToken)
        {
            var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            if (text[characterPosition] != '.')
            {
                return null;
            }

            // don't want to trigger after a number.  All other cases after dot are ok.
            var tree = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var token = tree.FindToken(characterPosition);
            if (token.Kind() == SyntaxKind.DotToken)
            {
                token = token.GetPreviousToken();
            }

            return token.Kind() != SyntaxKind.NumericLiteralToken;
        }

        protected override async Task<SyntaxContext> CreateContext(Document document, int position, CancellationToken cancellationToken)
        {
            var workspace = document.Project.Solution.Workspace;
            var span = new TextSpan(position, 0);
            var semanticModel = await document.GetSemanticModelForSpanAsync(span, cancellationToken).ConfigureAwait(false);
            return CSharpSyntaxContext.CreateContext(workspace, semanticModel, position, cancellationToken);
        }

        protected override (string displayText, string insertionText) GetDisplayAndInsertionText(ISymbol symbol, SyntaxContext context)
            => CompletionUtilities.GetDisplayAndInsertionText(symbol, context);

        protected override CompletionItemRules GetCompletionItemRules(List<ISymbol> symbols, SyntaxContext context, bool preselect)
        {
            cachedRules.TryGetValue(ValueTuple.Create(context.IsInImportsDirective, preselect, context.IsPossibleTupleContext), out var rule);

            return rule ?? CompletionItemRules.Default;
        }

        private static readonly Dictionary<ValueTuple<bool, bool, bool>, CompletionItemRules> cachedRules = InitCachedRules();

        private static Dictionary<ValueTuple<bool, bool, bool>, CompletionItemRules> InitCachedRules()
        {
            var result = new Dictionary<ValueTuple<bool, bool, bool>, CompletionItemRules>();

            for (int importDirective = 0; importDirective < 2; importDirective++)
            {
                for (int preselect = 0; preselect < 2; preselect++)
                {
                    for (int tupleLiteral = 0; tupleLiteral < 2; tupleLiteral++)
                    {
                        if (importDirective == 1 && tupleLiteral == 1)
                        {
                            // this combination doesn't make sense, we can skip it
                            continue;
                        }

                        var context = ValueTuple.Create(importDirective == 1, preselect == 1, tupleLiteral == 1);
                        result[context] = MakeRule(importDirective, preselect, tupleLiteral);
                    }
                }
            }

            return result;
        }

        private static CompletionItemRules MakeRule(int importDirective, int preselect, int tupleLiteral)
        {
            return MakeRule(importDirective == 1, preselect == 1, tupleLiteral == 1);
        }

        private static CompletionItemRules MakeRule(bool importDirective, bool preselect, bool tupleLiteral)
        {
            // '<' should not filter the completion list, even though it's in generic items like IList<>
            var generalBaseline = CompletionItemRules.Default.
                WithFilterCharacterRule(CharacterSetModificationRule.Create(CharacterSetModificationKind.Remove, '<')).
                WithCommitCharacterRule(CharacterSetModificationRule.Create(CharacterSetModificationKind.Add, '<'));

            var importDirectiveBaseline = CompletionItemRules.Create(commitCharacterRules:
                ImmutableArray.Create(CharacterSetModificationRule.Create(CharacterSetModificationKind.Replace, '.', ';')));

            var rule = importDirective ? importDirectiveBaseline : generalBaseline;

            if (preselect)
            {
                rule = rule.WithSelectionBehavior(CompletionItemSelectionBehavior.HardSelection);
            }

            if (tupleLiteral)
            {
                rule = rule
                    .WithCommitCharacterRule(CharacterSetModificationRule.Create(CharacterSetModificationKind.Remove, ':'));
            }

            return rule;
        }

        protected override CompletionItemSelectionBehavior PreselectedItemSelectionBehavior => CompletionItemSelectionBehavior.HardSelection;
    }
}
