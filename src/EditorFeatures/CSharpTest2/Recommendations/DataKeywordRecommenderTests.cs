// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.Recommendations
{
    [Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
    public class DataKeywordRecommenderTests : KeywordRecommenderTests
    {
        internal Task VerifyKeywordAsync(string text)
        {
            // Not testing recommender in script yet
            // Tracked by https://github.com/dotnet/roslyn/issues/44865
            return VerifyWorkerAsync(text, absent: false, options: TestOptions.RegularPreview);
        }

        [Fact]
        public async Task TestNotAtRoot_Interactive()
        {
            await VerifyAbsenceAsync(SourceCodeKind.Script,
@"$$");
        }

        [Fact]
        public async Task TestNotAfterClass_Interactive()
        {
            await VerifyAbsenceAsync(SourceCodeKind.Script,
@"class C { }
$$");
        }

        [Fact]
        public async Task TestNotAfterGlobalStatement_Interactive()
        {
            await VerifyAbsenceAsync(SourceCodeKind.Script,
@"System.Console.WriteLine();
$$");
        }

        [Fact]
        public async Task TestNotAfterGlobalVariableDeclaration_Interactive()
        {
            await VerifyAbsenceAsync(SourceCodeKind.Script,
@"int i = 0;
$$");
        }

        [Fact]
        public async Task TestNotInUsingAlias()
        {
            await VerifyAbsenceAsync(
@"using Goo = $$");
        }

        [Fact]
        public async Task TestNotInEmptyStatement()
        {
            await VerifyAbsenceAsync(AddInsideMethod(
@"$$"));
        }

        [Fact]
        public async Task TestNotInCompilationUnit()
        {
            await VerifyAbsenceAsync(
@"$$");
        }

        [Fact]
        public async Task TestNotAfterExtern()
        {
            await VerifyAbsenceAsync(
@"extern alias Goo;
$$");
        }

        [Fact]
        public async Task TestNotAfterUsing()
        {
            await VerifyAbsenceAsync(
@"using Goo;
$$");
        }

        [Fact]
        public async Task TestNotAfterNamespace()
        {
            await VerifyAbsenceAsync(
@"namespace N {}
$$");
        }

        [Fact]
        public async Task TestNotAfterTypeDeclaration()
        {
            await VerifyAbsenceAsync(
@"class C {}
$$");
        }

        [Fact]
        public async Task TestNotAfterDelegateDeclaration()
        {
            await VerifyAbsenceAsync(
@"delegate void Goo();
$$");
        }

        [Fact]
        public async Task TestAfterMethodInRecord()
        {
            await VerifyKeywordAsync(
@"record C {
  void Goo() {}
  $$");
        }

        [Fact]
        public async Task TestAfterField()
        {
            await VerifyKeywordAsync(
@"record C {
  int i;
  $$");
        }

        [Fact]
        public async Task TestAfterProperty()
        {
            await VerifyKeywordAsync(
@"record C {
  int i { get; }
  $$");
        }

        [Fact]
        public async Task TestNotBeforeUsing()
        {
            await VerifyAbsenceAsync(SourceCodeKind.Regular,
@"$$
using Goo;");
        }

        [Fact]
        public async Task TestNotAfterAssemblyAttribute()
        {
            await VerifyAbsenceAsync(
@"[assembly: goo]
$$");
        }

        [Fact]
        public async Task TestNotAfterRootAttribute()
        {
            await VerifyAbsenceAsync(
@"[goo]
$$");
        }

        [Fact]
        public async Task TestAfterNestedAttribute()
        {
            await VerifyKeywordAsync(
@"record C {
  [goo]
  $$");
        }

        [Fact]
        public async Task TestNotInsideStruct()
        {
            await VerifyAbsenceAsync(
@"struct S {
   $$");
        }

        [Fact]
        public async Task TestNotInsideInterface()
        {
            await VerifyAbsenceAsync(
@"interface I {
   $$");
        }

        [Fact]
        public async Task TestNotInsideClass()
        {
            await VerifyAbsenceAsync(
@"class C {
   $$");
        }

        [Fact]
        public async Task TestInsideRecord()
            => await VerifyKeywordAsync(@"record C { $$");

        [Fact]
        public async Task TestNotAfterPartial()
            => await VerifyAbsenceAsync(@"record C { partial $$");

        [Fact]
        public async Task TestNotAfterData()
            => await VerifyAbsenceAsync(@"data $$");

        [Fact]
        public async Task TestNotAfterPublicData()
            => await VerifyAbsenceAsync(@"public data $$");

        [Fact]
        public async Task TestAfterAbstract()
            => await VerifyKeywordAsync(@"record C { abstract $$");

        [Fact]
        public async Task TestAfterInternal()
            => await VerifyKeywordAsync(@"record C { internal $$");

        [Fact]
        public async Task TestAfterStaticPublic()
            => await VerifyKeywordAsync(@"record C { static public $$");

        [Fact]
        public async Task TestAfterPublicStatic()
            => await VerifyKeywordAsync(@"record C { public static $$");

        [Fact]
        public async Task TestNotAfterInvalidPublic()
            => await VerifyAbsenceAsync(@"virtual public $$");

        [Fact]
        public async Task TestAfterPublic()
            => await VerifyKeywordAsync(@"record C { public $$");

        [Fact]
        public async Task TestAfterPrivate()
            => await VerifyKeywordAsync(@"record C { private $$");

        [Fact]
        public async Task TestAfterProtected()
            => await VerifyKeywordAsync(@"record C { protected $$");

        [Fact]
        public async Task TestAfterSealed()
            => await VerifyKeywordAsync(@"record C { sealed $$");

        [Fact]
        public async Task TestAfterStatic() 
            => await VerifyKeywordAsync(@"record C { static $$");

        [Fact]
        public async Task TestNotAfterStaticInUsingDirective()
            => await VerifyAbsenceAsync(@"using static $$");

        [Fact]
        public async Task TestNotAfterClass()
            => await VerifyAbsenceAsync(@"class $$");

        [Fact]
        public async Task TestNotBetweenUsings()
        {
            await VerifyAbsenceAsync(AddInsideMethod(
@"using Goo;
$$
using Bar;"));
        }

        [Fact]
        public async Task TestNotAfterClassTypeParameterConstraint()
            => await VerifyAbsenceAsync(@"class C<T> where T : $$");
    }
}
