// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Diagnostics.ParenthesizeConditional;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.Diagnostics;
using Microsoft.CodeAnalysis.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.ParenthesizeConditional
{
    [Trait(Traits.Feature, Traits.Features.CodeActionsParenthesizeConditional)]
    public partial class ParenthesizeConditionalTests : AbstractCSharpDiagnosticProviderBasedUserDiagnosticTest
    {
        internal override (DiagnosticAnalyzer, CodeFixProvider) CreateDiagnosticProviderAndFixer(Workspace workspace)
            => (null, new CSharpParenthesizeConditionalCodeFixProvider());

        [Fact]
        public async Task Simple()
        {
            await TestInRegularAndScriptAsync(
@"class Program
{
    void M(bool x)
    {
        _ = $""{[|x ? 1|] : 2}"";
    }
}",
@"class Program
{
    void M(bool x)
    {
        _ = $""{(x ? 1 : 2)}"";
    }
}");
        }

        [Fact]
        public async Task Comparison()
        {
            await TestInRegularAndScriptAsync(
@"class Program
{
    void M()
    {
        _ = $""{[|0 == 1 ? 1|] : 2}"";
    }
}",
@"class Program
{
    void M()
    {
        _ = $""{(0 == 1 ? 1 : 2)}"";
    }
}");
        }

        [Fact]
        public async Task ActualFormat()
        {
            await TestMissingInRegularAndScriptAsync(
@"class Program
{
    void M(bool x)
    {
        _ = $""{[|x ? 1|] : 2 : D3}"";
    }
}");
        }

        [Fact]
        public async Task NoFormatColon()
        {
            await TestMissingInRegularAndScriptAsync(
@"class Program
{
    void M(bool x)
    {
        _ = $""{[|x ? 1|]}"";
    }
}");
        }

        [Fact]
        public async Task NoFormat()
        {
            await TestInRegularAndScriptAsync(
@"class Program
{
    void M(bool x)
    {
        _ = $""{[|x ? 1|] : }"";
    }
}",
@"class Program
{
    void M(bool x)
    {
        _ = $""{(x ? 1 :)}"";
    }
}");
        }

        [Fact]
        public async Task NestedConditional()
        {
            await TestInRegularAndScriptAsync(
@"class Program
{
    void M(bool x)
    {
        _ = $""{x ? ([|x ? 1 : 2|]) : 3}"";
    }
}",
@"class Program
{
    void M(bool x)
    {
        _ = $""{(x ? (x ? 1 : 2) : 3)}"";
    }
}");
        }

        [Fact]
        public async Task NestedConditional2()
        {
            await TestInRegularAndScriptAsync(
@"class Program
{
    void M(bool x)
    {
        _ = $""{x ? $""{[|x ? 1|] : 2}"" : null}"";
    }
}",
@"class Program
{
    void M(bool x)
    {
        _ = $""{(x ? $""{x ? 1 : 2}"" : null)}"";
    }
}");
        }

        [Fact]
        public async Task NestedConditional2_FixAll()
        {
            await TestInRegularAndScriptAsync(
@"class Program
{
    void M(bool x)
    {
        _ = $""{x ? $""{{|FixAllInDocument:x ? 1|} : 2}"" : null}"";
    }
}",
@"class Program
{
    void M(bool x)
    {
        _ = $""{(x ? $""{(x ? 1 : 2)}"" : null)}"";
    }
}");
        }

        [Fact]
        public async Task NestedConditional3()
        {
            await TestInRegularAndScriptAsync(
@"class Program
{
    void M(bool x)
    {
        _ = $""{(x ? $""{[|x ? 1|] : 2}"" : null)}"";
    }
}",
@"class Program
{
    void M(bool x)
    {
        _ = $""{(x ? $""{(x ? 1 : 2)}"" : null)}"";
    }
}");
        }
    }
}
