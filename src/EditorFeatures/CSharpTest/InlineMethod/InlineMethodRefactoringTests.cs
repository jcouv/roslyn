// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp.InlineMethod;
using Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.CodeRefactorings;
using Microsoft.CodeAnalysis.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.InlineMethod
{
    [Trait(Traits.Feature, Traits.Features.CodeActionsInlineMethod)]
    public class InlineMethodRefactoringTests : AbstractCSharpCodeActionTest
    {
        protected override CodeRefactoringProvider CreateCodeRefactoringProvider(Workspace workspace, TestParameters parameters)
            => new InlineMethodCodeRefactoringProvider();

        [Fact]
        public async Task TestStaticVoidMethodInBlockBody()
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

        [Fact]
        public async Task TestStaticIntMethodInBody()
        {
            await TestInRegularAndScript1Async(
@"class C
{
    int M()
    {
        return InlineMe();
    }
    int [||]InlineMe()
    {
        return int.Add(1, 1);
    }
}",
@"class C
{
    int M()
    {
        return int.Add(1, 1);
    }
}");
        }

        [Fact]
        public async Task TestStaticIntMethodInExpressionBody()
        {
            await TestInRegularAndScript1Async(
@"class C
{
    int M()
    {
        return InlineMe();
    }
    int [||]InlineMe() => 1 + 1;
}",
@"class C
{
    int M()
    {
        return 1 + 1;
    }
}");
        }

        [Fact]
        public async Task TestStaticIntMethodInExpressionBody_InExpressionBodiedCallSite()
        {
            await TestInRegularAndScript1Async(
@"class C
{
    int M() => InlineMe();
    int [||]InlineMe() => 1 + 1;
}",
@"class C
{
    int M() => 1 + 1;
}");
        }

        [Fact]
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

        [Fact]
        public async Task TestNotInMethodUsingParameters()
        {
            await TestMissingInRegularAndScriptAsync(
@"class C
{
    void M()
    {
        InlineMe(1);
    }
    void [||]InlineMe(int a)
    {
        System.Console.Write(1);
    }
}");
        }

        [Fact]
        public async Task TestNotInMethodUsingTypeParameters()
        {
            await TestMissingInRegularAndScriptAsync(
@"class C
{
    void M()
    {
        InlineMe<int>();
    }
    void [||]InlineMe<T>()
    {
        System.Console.Write(default(T));
    }
}");
        }

        [Fact]
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

        [Fact]
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

        [Fact]
        public async Task TestInstanceVoidMethodInBody()
        {
            await TestInRegularAndScript1Async(
@"class C
{
    void M()
    {
        this.InlineMe();
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
        Print();
    }
    void Print() => throw null;
}");
        }

        [Fact]
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

        [Fact]
        public async Task TestInstanceVoidMethodInBody_ReplaceTargetInstance()
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

        [Fact]
        public async Task TestInstanceVoidMethodInBody_ReplaceTargetInstance_Multiple()
        {
            // TODO: if the target is used multiple times in the method to inline, then
            // the target instance should be extracted to a local, or the operation should produce a conflict/warning.
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
        this.Print(this);
    }
    public void Print(D d) => throw null;
}",
@"class C
{
    void M(D d)
    {
        d.Print(d);
    }
}
public class D
{
    public void Print(D d) => throw null;
}");
        }

        [Fact]
        public async Task TestInstanceVoidMethodInBody_ReplaceStaticTarget()
        {
            await TestInRegularAndScript1Async(
@"class C
{
    void M()
    {
        D.InlineMe();
    }
}
public class D
{
    static public void [||]InlineMe()
    {
        Print();
    }
    static public void Print() => throw null;
}",
@"class C
{
    void M()
    {
        D.Print();
    }
}
public class D
{
    static public void Print() => throw null;
}");
        }

        [Fact(Skip = "TODO")]
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
