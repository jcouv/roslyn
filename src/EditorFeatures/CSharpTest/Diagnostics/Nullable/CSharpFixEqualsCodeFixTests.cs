// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.CodeFixes.DeclareAsNullable;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.Diagnostics.DeclareAsNullable
{
    [Trait(Traits.Feature, Traits.Features.CodeActionsDeclareAsNullable)]
    public class CSharpFixEqualsCodeFixTests : AbstractCSharpDiagnosticProviderBasedUserDiagnosticTest
    {
        internal override (DiagnosticAnalyzer, CodeFixProvider) CreateDiagnosticProviderAndFixer(Workspace workspace)
            => (null, new CSharpFixEqualsCodeFixProvider());

        private static readonly TestParameters s_nullableFeature = new TestParameters(parseOptions: new CSharpParseOptions(LanguageVersion.CSharp8));

        [Fact]
        public async Task FixAll()
        {
            await TestInRegularAndScript1Async(
@"
class Program
{
    static string M()
    {
        return {|FixAllInDocument:null|};
    }
    static string M2(bool b)
    {
        if (b)
            return null;
        else
            return null;
    }
}",
@"
class Program
{
    static string? M()
    {
        return null;
    }
    static string? M2(bool b)
    {
        if (b)
            return null;
        else
            return null;
    }
}", parameters: s_nullableFeature);
        }

        [Fact]
        public async Task FixNotEquals()
        {
            await TestInRegularAndScript1Async(
@"
class Program
{
    void M(TypeSymbol x)
    {
        _ = [|x != x|];
    }
}" + typeSymbol,
@"
class Program
{
    void M(TypeSymbol x)
    {
        _ = !TypeSymbol.Equals(x, x, TypeCompareKind.ConsiderEverything2);
    }
}" + typeSymbol, parameters: s_nullableFeature);
        }

        [Fact]
        public async Task FixEquals()
        {
            await TestInRegularAndScript1Async(
@"
class Program
{
    void M(TypeSymbol x)
    {
        _ = [|x == x|];
    }
}" + typeSymbol,
@"
class Program
{
    void M(TypeSymbol x)
    {
        _ = TypeSymbol.Equals(x, x, TypeCompareKind.ConsiderEverything2);
    }
}" + typeSymbol, parameters: s_nullableFeature);
        }

        const string typeSymbol = @"
public class TypeSymbol
{
    [System.Obsolete(""obsolete"", error: true)]
    public static bool operator ==(TypeSymbol left, TypeSymbol right)
    {
        throw null;
    }

    [System.Obsolete(""obsolete"", error: true)]
    public static bool operator !=(TypeSymbol left, TypeSymbol right)
    {
        throw null;
    }
}";
    }
}
