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
        public async Task TestStaticVoidMethodInBody()
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

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsInlineMethod)]
        public async Task TestNotInMethodUsingBase()
        {
            await TestMissingInRegularAndScriptAsync(
@"class C
{
    void M()
    {
        InlineMe();
    }
    void [||]InlineMe()
    {
        System.Console.Write(base.ToString());
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsInlineMethod)]
        public async Task TestNotInMethodUsingDeclarationExpression()
        {
            await TestMissingInRegularAndScriptAsync(
@"class C
{
    void M()
    {
        InlineMe();
    }
    void [||]InlineMe()
    {
        M(out var i);
    }
    void M(out int x) => throw null;
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsInlineMethod)]
        public async Task TestNotInMethodUsingDeclarationPattern()
        {
            await TestMissingInRegularAndScriptAsync(
@"class C
{
    void M()
    {
        InlineMe();
    }
    bool [||]InlineMe()
    {
        return true is bool b && b;
    }
}");
        }

        [Fact(Skip = "TODO"), Trait(Traits.Feature, Traits.Features.CodeActionsInlineMethod)]
        public async Task TestInstanceVoidMethodInBody()
        {
            await TestInRegularAndScript1Async(
@"class C
{
    void M()
    {
        this.InlineMe(); // bug: this is not recognized as a call-site
    }
    void [||]InlineMe()
    {
        Print();
    }
    void Print() => throw null;
}",
@"class C
{
    void M()
    {
        this.Print();
    }
    void Print() => throw null;
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsInlineMethod)]
        public async Task TestInstanceVoidMethodInBody2()
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
        InstanceMethod();
    }
    void InstanceMethod() => throw null;
}",
@"class C
{
    void M()
    {
        InstanceMethod();
    }
    void InstanceMethod() => throw null;
}");
            // TODO: there should be a conflict reported because `this.InstanceMethod()`. Maybe the simplifier should leave nodes with conflict annotation?
        }

        [Fact(Skip = "TODO"), Trait(Traits.Feature, Traits.Features.CodeActionsInlineMethod)]
        public async Task TestInstanceVoidMethodInBody_CalledFromAnotherType()
        {
            await TestInRegularAndScript1Async(
@"class C
{
    void M(D d)
    {
        d.InlineMe();
    }
}
public class D
{
    public void [||]InlineMe()
    {
        this.Print();
    }
    public void Print() => throw null;
}",
@"class C
{
    void M(D d)
    {
        d.Print();
    }
}
public class D
{
    public void Print() => throw null;
}");
        }

        [Fact(Skip = "TODO"), Trait(Traits.Feature, Traits.Features.CodeActionsInlineMethod)]
        public async Task TestInstanceVoidMethodInBody_CalledFromAnotherType_Inaccessible()
        {
            await TestInRegularAndScript1Async(
@"class C
{
    void M(D d)
    {
        d.InlineMe();
    }
}
public class D
{
    public void [||]InlineMe()
    {
        this.Print();
    }
    private void Print() => throw null;
}",
@"class C
{
    void M(D d)
    {
        d.Print(); // error
    }
}
public class D
{
    private void Print() => throw null;
}");
        }
    }
}
