// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Completion.Providers;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Completion.Providers;
using Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.Completion.CompletionProviders;
using Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces;
using Microsoft.CodeAnalysis.Packaging;
using Microsoft.CodeAnalysis.SymbolSearch;
using Microsoft.CodeAnalysis.Test.Utilities;
using Moq;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.Completion.CompletionSetSources
{
    [Trait(Traits.Feature, Traits.Features.Completion)]
    public partial class KnownSymbolCompletionProviderTests : AbstractCSharpCompletionProviderTests
    {
        public KnownSymbolCompletionProviderTests(CSharpTestWorkspaceFixture workspaceFixture) : base(workspaceFixture)
        {
        }

        const string NugetOrgSource = "nuget.org";
        private static readonly ImmutableArray<PackageSource> NugetPackageSources = ImmutableArray.Create(new PackageSource(NugetOrgSource, "http://nuget.org"));

        internal override CompletionProvider CreateCompletionProvider()
        {
            // Make a loose mock for the installer service.  We don't care what this test
            // calls on it.
            var installerServiceMock = new Mock<IPackageInstallerService>(MockBehavior.Loose);
            installerServiceMock.Setup((i) => i.IsEnabled(It.IsAny<ProjectId>())).Returns(true);
            installerServiceMock.SetupGet((i) => i.PackageSources).Returns(NugetPackageSources);
            installerServiceMock.Setup((s) => s.TryInstallPackage(It.IsAny<Workspace>(), It.IsAny<DocumentId>(), It.IsAny<string>(), "NuGetPackage", It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).
                                 Returns(true);

            var packageServiceMock = new Mock<ISymbolSearchService>();
            packageServiceMock.Setup((s) => s.FindPackagesWithTypeAsync(NugetOrgSource, "NuGetType", 0, It.IsAny<CancellationToken>())).
                Returns(CreateSearchResult("NuGetPackage", "NuGetType", CreateNameParts("NS1", "NS2")));

            return new KnownSymbolCompletionProvider(installerServiceMock.Object, packageServiceMock.Object);
        }

        private Task<IList<PackageWithTypeResult>> CreateSearchResult(string packageName, string typeName, ImmutableArray<string> nameParts)
        {
            return CreateSearchResult(new PackageWithTypeResult(
                packageName: packageName,
                typeName: typeName,
                version: default,
                rank: 0,
                containingNamespaceNames: nameParts));
        }

        private Task<IList<PackageWithTypeResult>> CreateSearchResult(params PackageWithTypeResult[] results)
        {
            return Task.FromResult<IList<PackageWithTypeResult>>(ImmutableArray.Create(results));
        }

        private ImmutableArray<string> CreateNameParts(params string[] parts)
        {
            return parts.ToImmutableArray();
        }

        [Fact]
        public async Task NamespaceName_Qualified_WithNested()
        {
            var source = @"
class C
{
    List$$
}
";

            await VerifyItemExistsAsync(source, "List", sourceCodeKind: SourceCodeKind.Regular);
        }
    }
}
