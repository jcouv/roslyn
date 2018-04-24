// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp.InlineMethod;
using Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.CodeRefactorings;
using Microsoft.CodeAnalysis.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.InlineMethod
{
    public class InlineMethodRefactoringTests : AbstractCSharpCodeActionTest
    {
        protected override CodeRefactoringProvider CreateCodeRefactoringProvider(Workspace workspace, TestParameters parameters)
            => new InlineMethodCodeRefactoringProvider();

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsInlineMethod)]
        public async Task TestStaticVoidMethod()
        {
            await TestInRegularAndScript1Async(
@"class C
{
    void M()
    {
        InlineMe();
    }
    void [||]InlineMe()
    {
        System.Console.Write(""hello"");
    }
}",
@"class C
{
    void M()
    {
        System.Console.Write(""hello"");
    }
}");
        }

    }
}
