// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using Microsoft.CodeAnalysis.CodeGen;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.ExpressionEvaluator;
using Microsoft.CodeAnalysis.ExpressionEvaluator.UnitTests;
using Microsoft.CodeAnalysis.PooledObjects;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.ExpressionEvaluator.UnitTests
{
    public class LocalFunctionTests : ExpressionCompilerTestBase
    {
        /// <summary>
        /// Returns the 1-based line number of the first line in <paramref name="source"/> containing <paramref name="marker"/>.
        /// </summary>
        private static int LineOf(string source, string marker)
        {
            var lines = source.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(marker))
                {
                    return i + 1;
                }
            }

            throw new System.ArgumentException($"Marker '{marker}' not found in source.");
        }

        [Fact]
        public void NoLocals()
        {
            var source =
@"class C
{
    void F(int x)
    {
        int y = x + 1;
        int G()
        {
            return 0;
        };
        int z = G();
    }
}";
            var compilation0 = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(compilation0, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.<F>g__G|0_0");
                var testData = new CompilationTestData();
                var locals = ArrayBuilder<LocalAndMethod>.GetInstance();
                string typeName;
                var assembly = context.CompileGetLocals(locals, argumentsOnly: false, typeName: out typeName, testData: testData);
                Assert.NotNull(assembly);
                Assert.Empty(assembly);
                Assert.Empty(locals);
                locals.Free();
            });
        }

        [Fact]
        public void Locals()
        {
            var source =
@"class C
{
    void F(int x)
    {
        int G(int y)
        {
            int z = y + 1;
            return z;
        };
        G(x + 1);
    }
}";
            var compilation0 = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(compilation0, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.<F>g__G|0_0");
                var testData = new CompilationTestData();
                var locals = ArrayBuilder<LocalAndMethod>.GetInstance();
                string typeName;
                var assembly = context.CompileGetLocals(locals, argumentsOnly: false, typeName: out typeName, testData: testData);
                Assert.Equal(2, locals.Count);
                VerifyLocal(testData, typeName, locals[0], "<>m0", "y", expectedILOpt:
@"{
  // Code size        2 (0x2)
  .maxstack  1
  .locals init (int V_0, //z
                int V_1)
  IL_0000:  ldarg.0
  IL_0001:  ret
}");
                VerifyLocal(testData, typeName, locals[1], "<>m1", "z", expectedILOpt:
@"{
  // Code size        2 (0x2)
  .maxstack  1
  .locals init (int V_0, //z
                int V_1)
  IL_0000:  ldloc.0
  IL_0001:  ret
}");
                locals.Free();
                string error;
                context.CompileExpression("this.F(1)", out error, testData);
                Assert.Equal("error CS0027: Keyword 'this' is not available in the current context", error);
            });
        }

        [Fact]
        public void CapturedVariable()
        {
            var source =
@"class C
{
    int x;
    void F(int y)
    {
        int G()
        {
            return x + y;
        };
        int z = G();
    }
}";
            var compilation0 = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(compilation0, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.<F>g__G|1_0");
                var testData = new CompilationTestData();
                var locals = ArrayBuilder<LocalAndMethod>.GetInstance();
                string typeName;
                var assembly = context.CompileGetLocals(locals, argumentsOnly: false, typeName: out typeName, testData: testData);
                Assert.Equal(2, locals.Count);
                VerifyLocal(testData, typeName, locals[0], "<>m0", "this", expectedILOpt:
@"{
  // Code size        7 (0x7)
  .maxstack  1
  .locals init (int V_0)
  IL_0000:  ldarg.1
  IL_0001:  ldfld      ""C C.<>c__DisplayClass1_0.<>4__this""
  IL_0006:  ret
}");
                VerifyLocal(testData, typeName, locals[1], "<>m1", "y", expectedILOpt:
@"{
  // Code size        7 (0x7)
  .maxstack  1
  .locals init (int V_0)
  IL_0000:  ldarg.1
  IL_0001:  ldfld      ""int C.<>c__DisplayClass1_0.y""
  IL_0006:  ret
}");
                locals.Free();
                testData = new CompilationTestData();
                string error;
                context.CompileExpression("this.F(1)", out error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL(
 @"{
  // Code size       13 (0xd)
  .maxstack  2
  .locals init (int V_0)
  IL_0000:  ldarg.1
  IL_0001:  ldfld      ""C C.<>c__DisplayClass1_0.<>4__this""
  IL_0006:  ldc.i4.1
  IL_0007:  callvirt   ""void C.F(int)""
  IL_000c:  ret
}");
            });
        }

        [Fact]
        public void MultipleDisplayClasses()
        {
            var source =
@"class C
{
    void F1(int x)
    {
        int F2(int y)
        {
            int F3() => x + y;
            return F3();
        };
        F2(1);
    }
}";
            var compilation0 = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(compilation0, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.<F1>g__F3|0_1");
                var testData = new CompilationTestData();
                var locals = ArrayBuilder<LocalAndMethod>.GetInstance();
                string typeName;
                var assembly = context.CompileGetLocals(locals, argumentsOnly: false, typeName: out typeName, testData: testData);
                Assert.Equal(2, locals.Count);
                VerifyLocal(testData, typeName, locals[0], "<>m0", "x", expectedILOpt:
@"{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  ldfld      ""int C.<>c__DisplayClass0_0.x""
  IL_0006:  ret
}");
                VerifyLocal(testData, typeName, locals[1], "<>m1", "y", expectedILOpt:
@"{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.1
  IL_0001:  ldfld      ""int C.<>c__DisplayClass0_1.y""
  IL_0006:  ret
}");
                locals.Free();
                testData = new CompilationTestData();
                string error;
                context.CompileExpression("x + y", out error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL(
 @"{
  // Code size       14 (0xe)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldfld      ""int C.<>c__DisplayClass0_0.x""
  IL_0006:  ldarg.1
  IL_0007:  ldfld      ""int C.<>c__DisplayClass0_1.y""
  IL_000c:  add
  IL_000d:  ret
}");
            });
        }

        // Should not bind to unnamed display class parameters
        // (unnamed parameters are treated as named "value").
        [Fact]
        public void CapturedVariableNamedValue()
        {
            var source =
@"class C
{
    void F(int value)
    {
        int G()
        {
            return value + 1;
        };
        G();
    }
}";
            var compilation0 = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(compilation0, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.<F>g__G|0_0");
                var testData = new CompilationTestData();
                var locals = ArrayBuilder<LocalAndMethod>.GetInstance();
                string typeName;
                var assembly = context.CompileGetLocals(locals, argumentsOnly: false, typeName: out typeName, testData: testData);
                Assert.Equal(1, locals.Count);
                VerifyLocal(testData, typeName, locals[0], "<>m0", "value", expectedILOpt:
@"{
  // Code size        7 (0x7)
  .maxstack  1
  .locals init (int V_0)
  IL_0000:  ldarg.0
  IL_0001:  ldfld      ""int C.<>c__DisplayClass0_0.value""
  IL_0006:  ret
}");
                locals.Free();
                testData = new CompilationTestData();
                string error;
                context.CompileExpression("value", out error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL(
 @"{
  // Code size        7 (0x7)
  .maxstack  1
  .locals init (int V_0)
  IL_0000:  ldarg.0
  IL_0001:  ldfld      ""int C.<>c__DisplayClass0_0.value""
  IL_0006:  ret
}");
            });
        }

        // Should not bind to unnamed display class parameters
        // (unnamed parameters are treated as named "value").
        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/18426")]
        public void DisplayClassParameter_01()
        {
            var source =
@"class C
{
    void F(int x)
    {
        int G()
        {
            return x + 1;
        };
        G();
    }
}";
            var compilation0 = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(compilation0, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.<F>g__G|0_0");
                var testData = new CompilationTestData();
                string error;
                context.CompileExpression("value", out error, testData);
                Assert.Equal("error CS0103: The name 'value' does not exist in the current context", error);
            });
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/18426")]
        public void DisplayClassParameter_02()
        {
            var source =
@"class C
{
    void F(int x)
    {
        int G(int value)
        {
            return x + value;
        };
        G(1);
    }
}";
            var compilation0 = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(compilation0, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.<F>g__G|0_0");
                var testData = new CompilationTestData();
                string error;
                context.CompileExpression("value", out error, testData);

                Assert.Null(error);
                var data = testData.GetMethodData("<>x.<>m0");

                Assert.True(data.Method.IsStatic);
                Assert.Equal("System.Int32 <>x.<>m0(System.Int32 value, ref C.<>c__DisplayClass0_0 value)", ((Symbol)data.Method).ToTestDisplayString());
                data.VerifyIL(
 @"{
  // Code size        2 (0x2)
  .maxstack  1
  .locals init (int V_0)
  IL_0000:  ldarg.0
  IL_0001:  ret
}");
            });
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void CallLocalFunction()
        {
            var source =
@"class C
{
    void F(int x)
    {
        int G(int y)
        {
            return y + 1;
        };
        int z = G(x);
    }
}";
            var compilation0 = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(compilation0, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F");
                var testData = new CompilationTestData();
                string error;
                context.CompileExpression("G(1)", out error, testData);
                Assert.Null(error);
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/18426")]
        public void DisplayClassParameter_03()
        {
            var source =
@"class C
{
    void F(int x)
    {
        int G()
        {
            int value = 1;
            return x + Value();

            int Value() => value;
        };
        G();
    }
}";
            var compilation0 = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(compilation0, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.<F>g__G|0_0");
                var testData = new CompilationTestData();
                string error;
                context.CompileExpression("value", out error, testData);

                Assert.Null(error);
                var data = testData.GetMethodData("<>x.<>m0");

                Assert.True(data.Method.IsStatic);
                Assert.Equal("System.Int32 <>x.<>m0(ref C.<>c__DisplayClass0_0 value)", ((Symbol)data.Method).ToTestDisplayString());
                data.VerifyIL(
 @"{
  // Code size        7 (0x7)
  .maxstack  1
  .locals init (C.<>c__DisplayClass0_1 V_0, //CS$<>8__locals0
                int V_1)
  IL_0000:  ldloc.0
  IL_0001:  ldfld      ""int C.<>c__DisplayClass0_1.value""
  IL_0006:  ret
}");
            });
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/59093")]
        public void DeclaringCompilationIsNotNull()
        {
            var source = @"
using System;

class C
{
    static void Main()
    {
    }
}
";
            var comp = CreateCompilationWithMscorlib461(source, options: TestOptions.UnsafeDebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.Main");
                string error;
                var testData = new CompilationTestData();
                context.CompileExpression(@"
new Action<int>(x =>
{
    int F(int y)
    {
        switch (y)
        {
            case > 0: return 1;
            case < 0: return -1;
            case 0: return 0;
            default: return 0;
        }
    }
    F(x);
}).Invoke(1)
", out error, testData);
                Assert.Null(error);
            });
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_01()
        {
            var source = """
class C
{
    void F(int x)
    {
        int G(int y)
        {
            return y + 1;
        };
        int z = G(x);
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F");
                var testData = new CompilationTestData();
                context.CompileExpression("G(1)", out var error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size        7 (0x7)
  .maxstack  1
  .locals init (int V_0) //z
  IL_0000:  ldc.i4.1
  IL_0001:  call       "int G(int)"
  IL_0006:  ret
}
""");
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_02()
        {
            // outside scope of local function
            var source = """
class C
{
    void F(int x)
    {
        if (x > 0)
        {
            int G(int y) { return y + 1; }
            _ = G(x);
        }
        _ = x; /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                // Break at "_ = x", which is outside the block where G is declared.
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("G(1)", out var error, testData);
                Assert.Equal("error CS0103: The name 'G' does not exist in the current context", error);
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_03()
        {
            // nested local function: Inner visible inside G, not visible in F
            var source = """
class C
{
    void F()
    {
        int G()
        {
            int Inner() => 42;
            return Inner(); /*IN_G*/
        }
        _ = G(); /*IN_F*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                // Inside G: Inner is visible
                var context = CreateMethodContext(runtime, "C.<F>g__G|0_0", atLineNumber: LineOf(source, "IN_G"));
                var testData = new CompilationTestData();
                context.CompileExpression("Inner()", out var error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size        6 (0x6)
  .maxstack  1
  .locals init (int V_0)
  IL_0000:  call       "int Inner()"
  IL_0005:  ret
}
""");

                // Inside F: G is visible but Inner is not
                context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "IN_F"));
                testData = new CompilationTestData();
                context.CompileExpression("G()", out error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size        6 (0x6)
  .maxstack  1
  IL_0000:  call       "int G()"
  IL_0005:  ret
}
""");
                context.CompileExpression("Inner()", out error, testData: new CompilationTestData());
                Assert.Equal("error CS0103: The name 'Inner' does not exist in the current context", error);
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_04()
        {
            // local function declared in try block
            var source = """
class C
{
    void F(int x)
    {
        try
        {
            int G(int y) { return y + 1; }
            _ = G(x); /*IN_TRY*/
        }
        catch
        {
            _ = x; /*IN_CATCH*/
        }
        finally
        {
            _ = x; /*IN_FINALLY*/
        }
        _ = x; /*AFTER*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                // G is visible inside the try block
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "IN_TRY"));
                context.CompileExpression("G(1)", out var error, testData: new CompilationTestData());
                Assert.Null(error);

                // G is not visible in the catch block
                context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "IN_CATCH"));
                context.CompileExpression("G(1)", out error, testData: new CompilationTestData());
                Assert.Equal("error CS0103: The name 'G' does not exist in the current context", error);

                // G is not visible in the finally block
                context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "IN_FINALLY"));
                context.CompileExpression("G(1)", out error, testData: new CompilationTestData());
                Assert.Equal("error CS0103: The name 'G' does not exist in the current context", error);

                // G is not visible after the try/catch/finally
                context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "AFTER"));
                context.CompileExpression("G(1)", out error, testData: new CompilationTestData());
                Assert.Equal("error CS0103: The name 'G' does not exist in the current context", error);
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_05()
        {
            // local function declared in catch block
            var source = """
class C
{
    void F(int x)
    {
        try
        {
            _ = x; /*IN_TRY*/
        }
        catch
        {
            int G(int y) { return y + 1; }
            _ = G(x); /*IN_CATCH*/
        }
        finally
        {
            _ = x; /*IN_FINALLY*/
        }
        _ = x; /*AFTER*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                // G is not visible in the try block
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "IN_TRY"));
                context.CompileExpression("G(1)", out var error, testData: new CompilationTestData());
                Assert.Equal("error CS0103: The name 'G' does not exist in the current context", error);

                // G is visible inside the catch block
                context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "IN_CATCH"));
                context.CompileExpression("G(1)", out error, testData: new CompilationTestData());
                Assert.Null(error);

                // G is not visible in the finally block
                context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "IN_FINALLY"));
                context.CompileExpression("G(1)", out error, testData: new CompilationTestData());
                Assert.Equal("error CS0103: The name 'G' does not exist in the current context", error);

                // G is not visible after the try/catch/finally
                context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "AFTER"));
                context.CompileExpression("G(1)", out error, testData: new CompilationTestData());
                Assert.Equal("error CS0103: The name 'G' does not exist in the current context", error);
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_06()
        {
            // local function declared in finally block
            var source = """
class C
{
    void F(int x)
    {
        try
        {
            _ = x; /*IN_TRY*/
        }
        catch
        {
            _ = x; /*IN_CATCH*/
        }
        finally
        {
            int G(int y) { return y + 1; }
            _ = G(x); /*IN_FINALLY*/
        }
        _ = x; /*AFTER*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                // G is not visible in the try block
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "IN_TRY"));
                context.CompileExpression("G(1)", out var error, testData: new CompilationTestData());
                Assert.Equal("error CS0103: The name 'G' does not exist in the current context", error);

                // G is not visible in the catch block
                context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "IN_CATCH"));
                context.CompileExpression("G(1)", out error, testData: new CompilationTestData());
                Assert.Equal("error CS0103: The name 'G' does not exist in the current context", error);

                // G is visible inside the finally block
                context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "IN_FINALLY"));
                context.CompileExpression("G(1)", out error, testData: new CompilationTestData());
                Assert.Null(error);

                // G is not visible after the try/catch/finally
                context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "AFTER"));
                context.CompileExpression("G(1)", out error, testData: new CompilationTestData());
                Assert.Equal("error CS0103: The name 'G' does not exist in the current context", error);
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_07()
        {
            // async method
            var source = """
class C
{
    async System.Threading.Tasks.Task F(int x)
    {
        int G(int y) { return y + 1; }
        await System.Threading.Tasks.Task.Yield();
        _ = G(x); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.<F>d__0.MoveNext", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("G(1)", out var error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size        7 (0x7)
  .maxstack  1
  .locals init (int V_0,
                System.Runtime.CompilerServices.YieldAwaitable.YieldAwaiter V_1,
                System.Runtime.CompilerServices.YieldAwaitable V_2,
                C.<F>d__0 V_3,
                System.Exception V_4)
  IL_0000:  ldc.i4.1
  IL_0001:  call       "int G(int)"
  IL_0006:  ret
}
""");
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_08()
        {
            // async method compiled with runtime-async
            var source = """
class C
{
    async System.Threading.Tasks.Task F(int x)
    {
        int G(int y) { return y + 1; }
        await System.Threading.Tasks.Task.Yield();
        _ = G(x); /*BREAK*/
    }
}
""";
            var comp = CreateRuntimeAsyncCompilation(source, TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                // With runtime-async, there is no state machine — the method name is C.F
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("G(1)", out var error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size        7 (0x7)
  .maxstack  1
  .locals init (System.Runtime.CompilerServices.YieldAwaitable.YieldAwaiter V_0,
                System.Runtime.CompilerServices.YieldAwaitable V_1)
  IL_0000:  ldc.i4.1
  IL_0001:  call       "int G(int)"
  IL_0006:  ret
}
""");
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_09()
        {
            // iterator
            var source = """
class C
{
    System.Collections.Generic.IEnumerable<int> F(int x)
    {
        int G(int y) { return y + 1; }
        yield return G(x); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.<F>d__0.MoveNext", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("G(1)", out var error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size        7 (0x7)
  .maxstack  1
  .locals init (int V_0)
  IL_0000:  ldc.i4.1
  IL_0001:  call       "int G(int)"
  IL_0006:  ret
}
""");
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_10()
        {
            // Local function name should not be found when used in a type context
            // (e.g. typeof, nameof with type, or as a type in a declaration).
            // This exercises the CanConsiderLocals check in EELocalFunctionBinder.
            var source = """
class C
{
    void F(int x)
    {
        int G(int y) { return y + 1; }
        _ = G(x); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));

                // G works as an invocation
                context.CompileExpression("G(1)", out var error, testData: new CompilationTestData());
                Assert.Null(error);

                // G should not be found in a type context
                context.CompileExpression("typeof(G)", out error, testData: new CompilationTestData());
                Assert.Contains("CS0246", error);

                context.CompileExpression("default(G)", out error, testData: new CompilationTestData());
                Assert.Contains("CS0246", error);
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_11()
        {
            // CompileGetLocals exercises AddLookupSymbolsInfoInSingleBinder.
            // Local functions should NOT appear in the locals/autos window —
            // they are invocable names, not variables.
            var source = """
class C
{
    void F(int x)
    {
        int G(int y) { return y + 1; }
        _ = G(x); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var locals = ArrayBuilder<LocalAndMethod>.GetInstance();
                context.CompileGetLocals(locals, argumentsOnly: false, typeName: out _, testData: new CompilationTestData());

                // Only this and x should appear, not G
                Assert.Equal(2, locals.Count);
                Assert.Equal("this", locals[0].LocalName);
                Assert.Equal("x", locals[1].LocalName);
                locals.Free();
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_12()
        {
            // Calling a local function with wrong number of arguments
            var source = """
class C
{
    void F(int x)
    {
        int G(int y) { return y + 1; }
        _ = G(x); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));

                // No arguments
                context.CompileExpression("G()", out var error, testData: new CompilationTestData());
                Assert.Contains("CS7036", error);

                // Too many arguments
                context.CompileExpression("G(1, 2)", out error, testData: new CompilationTestData());
                Assert.Contains("CS1501", error);

                // Wrong argument type
                context.CompileExpression("G(\"hello\")", out error, testData: new CompilationTestData());
                Assert.Contains("CS1503", error);

                // Correct call still works
                context.CompileExpression("G(1)", out error, testData: new CompilationTestData());
                Assert.Null(error);
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_13()
        {
            // Calling a non-generic local function with type arguments
            var source = """
class C
{
    void F(int x)
    {
        int G(int y) { return y + 1; }
        _ = G(x); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));

                // Non-generic local function called with type arguments
                context.CompileExpression("G<int>(1)", out var error, testData: new CompilationTestData());
                Assert.Contains("CS0308", error);

                // Correct call still works
                context.CompileExpression("G(1)", out error, testData: new CompilationTestData());
                Assert.Null(error);
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_14()
        {
            // generic local function with its own type parameter
            var source = """
class C
{
    void F(int x)
    {
        T G<T>(T y) { return y; }
        _ = G(x); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));

                // Call with explicit type argument
                var testData = new CompilationTestData();
                context.CompileExpression("G<int>(42)", out var error, testData);
                Assert.Null(error);

                // Call with inferred type argument
                testData = new CompilationTestData();
                context.CompileExpression("G(\"hello\")", out error, testData);
                Assert.Null(error);
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_15()
        {
            // Named arguments
            var source = """
class C
{
    void F()
    {
        int G(int x, int y) { return x - y; }
        _ = G(1, 2); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("G(x: 10, y: 20)", out var error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size       10 (0xa)
  .maxstack  2
  IL_0000:  ldc.i4.s   10
  IL_0002:  ldc.i4.s   20
  IL_0004:  call       "int G(int, int)"
  IL_0009:  ret
}
""");
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_16()
        {
            // Named arguments in reversed order
            var source = """
class C
{
    void F()
    {
        int G(int x, int y) { return x - y; }
        _ = G(1, 2); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("G(y: 20, x: 10)", out var error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size       10 (0xa)
  .maxstack  2
  IL_0000:  ldc.i4.s   10
  IL_0002:  ldc.i4.s   20
  IL_0004:  call       "int G(int, int)"
  IL_0009:  ret
}
""");
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_17()
        {
            // Non-trailing named argument
            var source = """
class C
{
    void F()
    {
        int G(int x, int y, int z) { return x + y + z; }
        _ = G(1, 2, 3); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("G(y: 20, x: 10, z: 30)", out var error, testData);
                Assert.Null(error);
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_18()
        {
            // ref parameter
            var source = """
class C
{
    void F()
    {
        void G(ref int x) { x += 1; }
        int v = 10;
        G(ref v); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("G(ref v)", out var error, testData);
                Assert.Null(error);
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_19()
        {
            // out parameter
            var source = """
class C
{
    void F()
    {
        void G(out int x) { x = 42; }
        int v = 0;
        G(out v); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("G(out v)", out var error, testData);
                Assert.Null(error);
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_20()
        {
            // in parameter
            var source = """
class C
{
    void F()
    {
        int G(in int x) { return x + 1; }
        int v = 10;
        _ = G(in v); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("G(in v)", out var error, testData);
                Assert.Null(error);

                // in parameter can be passed without the modifier
                testData = new CompilationTestData();
                context.CompileExpression("G(v)", out error, testData);
                Assert.Null(error);

                // in parameter also accepts a literal (no variable needed)
                testData = new CompilationTestData();
                context.CompileExpression("G(42)", out error, testData);
                Assert.Null(error);
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_21()
        {
            // Mixed ref kinds
            var source = """
class C
{
    void F()
    {
        int G(int a, ref int b, out int c, in int d) { c = a + b + d; return c; }
        int x = 1, y = 2;
        _ = G(x, ref y, out int z, in x); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("G(10, ref y, out z, in x)", out var error, testData);
                Assert.Null(error);
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_22()
        {
            // Params parameter
            var source = """
class C
{
    void F()
    {
        int G(params int[] values) { return values.Length; }
        _ = G(1, 2, 3); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));

                // Expanded form
                var testData = new CompilationTestData();
                context.CompileExpression("G(10, 20)", out var error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size       22 (0x16)
  .maxstack  4
  IL_0000:  ldc.i4.2
  IL_0001:  newarr     "int"
  IL_0006:  dup
  IL_0007:  ldc.i4.0
  IL_0008:  ldc.i4.s   10
  IL_000a:  stelem.i4
  IL_000b:  dup
  IL_000c:  ldc.i4.1
  IL_000d:  ldc.i4.s   20
  IL_000f:  stelem.i4
  IL_0010:  call       "int G(params int[])"
  IL_0015:  ret
}
""");

                // Normal form (passing an array)
                testData = new CompilationTestData();
                context.CompileExpression("G(new int[] { 10, 20 })", out error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size       22 (0x16)
  .maxstack  4
  IL_0000:  ldc.i4.2
  IL_0001:  newarr     "int"
  IL_0006:  dup
  IL_0007:  ldc.i4.0
  IL_0008:  ldc.i4.s   10
  IL_000a:  stelem.i4
  IL_000b:  dup
  IL_000c:  ldc.i4.1
  IL_000d:  ldc.i4.s   20
  IL_000f:  stelem.i4
  IL_0010:  call       "int G(params int[])"
  IL_0015:  ret
}
""");

                // Empty params (expanded form)
                testData = new CompilationTestData();
                context.CompileExpression("G()", out error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size       11 (0xb)
  .maxstack  1
  IL_0000:  call       "int[] System.Array.Empty<int>()"
  IL_0005:  call       "int G(params int[])"
  IL_000a:  ret
}
""");
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_23()
        {
            // Optional parameter
            var source = """
class C
{
    void F()
    {
        int G(int x, int y = 100) { return x + y; }
        _ = G(1); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));

                // Both arguments provided
                var testData = new CompilationTestData();
                context.CompileExpression("G(10, 20)", out var error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size       10 (0xa)
  .maxstack  2
  IL_0000:  ldc.i4.s   10
  IL_0002:  ldc.i4.s   20
  IL_0004:  call       "int G(int, int)"
  IL_0009:  ret
}
""");

                // Using default value
                testData = new CompilationTestData();
                context.CompileExpression("G(10)", out error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size       10 (0xa)
  .maxstack  2
  IL_0000:  ldc.i4.s   10
  IL_0002:  ldc.i4.s   100
  IL_0004:  call       "int G(int, int)"
  IL_0009:  ret
}
""");

                // Named, skipping optional
                testData = new CompilationTestData();
                context.CompileExpression("G(x: 10)", out error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size       10 (0xa)
  .maxstack  2
  IL_0000:  ldc.i4.s   10
  IL_0002:  ldc.i4.s   100
  IL_0004:  call       "int G(int, int)"
  IL_0009:  ret
}
""");
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_24()
        {
            // composing local function calls: G(H(1))
            var source = """
class C
{
    void F(int x)
    {
        int G(int y) { return y + 1; }
        int H(int y) { return y * 2; }
        _ = G(H(x)); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("G(H(1))", out var error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size       12 (0xc)
  .maxstack  1
  IL_0000:  ldc.i4.1
  IL_0001:  call       "int H(int)"
  IL_0006:  call       "int G(int)"
  IL_000b:  ret
}
""");
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_25()
        {
            // assigning (effectively static) local function to delegate
            var source = """
class C
{
    void F(int x)
    {
        int G(int y) { return y + 1; }
        _ = G(x); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("new System.Func<int, int>(G)", out var error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size       13 (0xd)
  .maxstack  2
  IL_0000:  ldnull
  IL_0001:  ldftn      "int C.<F>g__G|0_0(int)"
  IL_0007:  newobj     "System.Func<int, int>..ctor(object, System.IntPtr)"
  IL_000c:  ret
}
""");
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_25a()
        {
            // assigning static local function to delegate
            var source = """
class C
{
    void F(int x)
    {
        static int G(int y) { return y + 1; }
        _ = G(x); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("new System.Func<int, int>(G)", out var error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size       13 (0xd)
  .maxstack  2
  IL_0000:  ldnull
  IL_0001:  ldftn      "int C.<F>g__G|0_0(int)"
  IL_0007:  newobj     "System.Func<int, int>..ctor(object, System.IntPtr)"
  IL_000c:  ret
}
""");
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_26()
        {
            // local function in top-level statement
            var source = """
int G(int y) { return y + 1; }
_ = G(1); /*BREAK*/
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugExe);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "Program.<Main>$", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("G(42)", out var error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size        8 (0x8)
  .maxstack  1
  IL_0000:  ldc.i4.s   42
  IL_0002:  call       "int G(int)"
  IL_0007:  ret
}
""");
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_27()
        {
            // local function that captures a local variable
            var source = """
class C
{
    void F()
    {
        int captured = 10;
        int G() { return captured + 1; }
        _ = G(); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("G()", out var error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size        8 (0x8)
  .maxstack  1
  .locals init (C.<>c__DisplayClass0_0 V_0) //CS$<>8__locals0
  IL_0000:  ldloca.s   V_0
  IL_0002:  call       "int C.<F>g__G|0_0(ref C.<>c__DisplayClass0_0)"
  IL_0007:  ret
}
""");
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_28()
        {
            // Calling a local function that has multiple hidden display class parameters.
            // F2 captures from two different scopes (x from F1, y from F2's own scope),
            // resulting in two display class parameters on the lowered method.
            var source = """
class C
{
    void F1(int x)
    {
        int F2(int y)
        {
            int F3() => x + y;
            _ = F3(); /*BREAK*/
            return 0;
        }
        F2(1);
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.<F1>g__F2|0_0", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("F3()", out var error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size        9 (0x9)
  .maxstack  2
  .locals init (C.<>c__DisplayClass0_1 V_0, //CS$<>8__locals0
                int V_1)
  IL_0000:  ldarg.1
  IL_0001:  ldloca.s   V_0
  IL_0003:  call       "int C.<F1>g__F3|0_1(ref C.<>c__DisplayClass0_0, ref C.<>c__DisplayClass0_1)"
  IL_0008:  ret
}
""");
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_29()
        {
            // Display class passed by value (class-based, not struct).
            // A lambda capturing the same variable forces a class-based display class.
            // The local function becomes an instance method on the display class,
            // which the rewriter supplies as the receiver.
            var source = """
class C
{
    void F()
    {
        int captured = 10;
        System.Func<int> lambda = () => captured;
        int G() { return captured + 1; }
        _ = G(); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("G()", out var error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size        7 (0x7)
  .maxstack  1
  .locals init (C.<>c__DisplayClass0_0 V_0, //CS$<>8__locals0
                System.Func<int> V_1) //lambda
  IL_0000:  ldloc.0
  IL_0001:  callvirt   "int C.<>c__DisplayClass0_0.<F>g__G|1()"
  IL_0006:  ret
}
""");
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_30()
        {
            // Local function that captures 'this'
            var source = """
class C
{
    int field = 42;
    void F()
    {
        int G() { return field + 1; }
        _ = G(); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("G()", out var error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  callvirt   "int C.<F>g__G|1_0()"
  IL_0006:  ret
}
""");
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_31()
        {
            // Calling a capturing local function with user-provided arguments.
            // The lowered method has both visible params and hidden display class params (ref).
            var source = """
class C
{
    void F()
    {
        int captured = 10;
        int G(int y) { return captured + y; }
        _ = G(1); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("G(42)", out var error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size       10 (0xa)
  .maxstack  2
  .locals init (C.<>c__DisplayClass0_0 V_0) //CS$<>8__locals0
  IL_0000:  ldc.i4.s   42
  IL_0002:  ldloca.s   V_0
  IL_0004:  call       "int C.<F>g__G|0_0(int, ref C.<>c__DisplayClass0_0)"
  IL_0009:  ret
}
""");
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_32()
        {
            // Calling a capturing local function with user-provided arguments
            // where the local function captures from two different scopes,
            // resulting in multiple hidden display class parameters plus a visible parameter.
            var source = """
class C
{
    void F1(int x)
    {
        int F2(int y)
        {
            int F3(int z) => x + y + z;
            _ = F3(1); /*BREAK*/
            return 0;
        }
        F2(1);
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.<F1>g__F2|0_0", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("F3(42)", out var error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size       11 (0xb)
  .maxstack  3
  .locals init (C.<>c__DisplayClass0_1 V_0, //CS$<>8__locals0
                int V_1)
  IL_0000:  ldc.i4.s   42
  IL_0002:  ldarg.1
  IL_0003:  ldloca.s   V_0
  IL_0005:  call       "int C.<F1>g__F3|0_1(int, ref C.<>c__DisplayClass0_0, ref C.<>c__DisplayClass0_1)"
  IL_000a:  ret
}
""");
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_33()
        {
            // Params parameter on a capturing local function.
            // Tests that expanded-form params and display class args coexist correctly.
            var source = """
class C
{
    void F()
    {
        int captured = 10;
        int G(params int[] values) { return captured + values.Length; }
        _ = G(1, 2, 3); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));

                // Expanded form
                var testData = new CompilationTestData();
                context.CompileExpression("G(10, 20)", out var error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size       24 (0x18)
  .maxstack  4
  .locals init (C.<>c__DisplayClass0_0 V_0) //CS$<>8__locals0
  IL_0000:  ldc.i4.2
  IL_0001:  newarr     "int"
  IL_0006:  dup
  IL_0007:  ldc.i4.0
  IL_0008:  ldc.i4.s   10
  IL_000a:  stelem.i4
  IL_000b:  dup
  IL_000c:  ldc.i4.1
  IL_000d:  ldc.i4.s   20
  IL_000f:  stelem.i4
  IL_0010:  ldloca.s   V_0
  IL_0012:  call       "int C.<F>g__G|0_0(params int[], ref C.<>c__DisplayClass0_0)"
  IL_0017:  ret
}
""");

                // Empty params (expanded form)
                testData = new CompilationTestData();
                context.CompileExpression("G()", out error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size       13 (0xd)
  .maxstack  2
  .locals init (C.<>c__DisplayClass0_0 V_0) //CS$<>8__locals0
  IL_0000:  call       "int[] System.Array.Empty<int>()"
  IL_0005:  ldloca.s   V_0
  IL_0007:  call       "int C.<F>g__G|0_0(params int[], ref C.<>c__DisplayClass0_0)"
  IL_000c:  ret
}
""");
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_34()
        {
            // Delegate conversion of a capturing local function
            var source = """
class C
{
    void F()
    {
        int captured = 10;
        int G(int y) { return captured + y; }
        _ = G(1); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("new System.Func<int, int>(G)", out var error, testData);
                Assert.Equal("error CS9360: Converting a local function 'G' to a delegate is not supported during debugging (consider using a lambda)", error);

                // Lambda workaround
                testData = new CompilationTestData();
                context.CompileExpression("(System.Func<int, int>)((int y) => G(y))", out error, testData);
                Assert.Null(error);
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_35()
        {
            // Delegate conversion of a capturing local function, where the original source
            // also converts G to a delegate. The underlying method is an instance method
            // on a class display class since there was a delegate conversion in original source.
            var source = """
class C
{
    void F()
    {
        int captured = 10;
        int G(int y) { return captured + y; }
        System.Func<int, int> f = G;
        _ = f(1); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("new System.Func<int, int>(G)", out var error, testData);
                Assert.Equal("error CS9360: Converting a local function 'G' to a delegate is not supported during debugging (consider using a lambda)", error);

                // Lambda workaround
                testData = new CompilationTestData();
                context.CompileExpression("(System.Func<int, int>)((int y) => G(y))", out error, testData);
                Assert.Null(error);
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_36()
        {
            // Generic local function (type parameters on the local function itself)
            var source = """
class C
{
    void F()
    {
        T G<T>(T x) { return x; }
        _ = G(42); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("""G<string>("hello")""", out var error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size       11 (0xb)
  .maxstack  1
  IL_0000:  ldstr      "hello"
  IL_0005:  call       "string G<string>(string)"
  IL_000a:  ret
}
""");
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_37()
        {
            // Generic local function that also captures a local variable
            var source = """
class C
{
    void F()
    {
        int captured = 10;
        T G<T>(T x) { _ = captured; return x; }
        _ = G(42); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("""G<string>("hello")""", out var error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size       11 (0xb)
  .maxstack  1
  .locals init (C.<>c__DisplayClass0_0 V_0) //CS$<>8__locals0
  IL_0000:  ldstr      "hello"
  IL_0005:  call       "string G<string>(string)"
  IL_000a:  ret
}
""");
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_38()
        {
            // TODO2: Local function using containing method's type parameter.
            // The EELocalFunctionMethodSymbol exposes the alpha-renamed type parameter
            // but the EE context doesn't have the mapping, so type inference fails.
            var source = """
class C
{
    void F<T>(T x)
    {
        T G() { return x; }
        _ = G(); /*BREAK*/
    }
}
""";
            var compilation0 = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(compilation0, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("G()", out var error, testData);
                Assert.Contains("CS0411", error);
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_39()
        {
            // TODO2: Local function with its own type parameter plus containing method's type parameter.
            // The EELocalFunctionMethodSymbol exposes all alpha-renamed type parameters,
            // so the user-visible arity is wrong (2 instead of 1).
            var source = """
class C
{
    void F<T>(T x)
    {
        U G<U>(T a, U b) { _ = a; return b; }
        _ = G(x, 42); /*BREAK*/
    }
}
""";
            var compilation0 = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(compilation0, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("""G<string>(x, "hello")""", out var error, testData);
                Assert.Contains("CS0305", error);
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_40()
        {
            // TODO2: Local function in a generic class, using class type parameter.
            // Crashes with "Unexpected type parameter T owned by C<T>".
            var source = """
class C<T>
{
    void F(T x)
    {
        T G() { return x; }
        _ = G(); /*BREAK*/
    }
}
""";
            var compilation0 = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(compilation0, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                Assert.ThrowsAny<System.Exception>(() => context.CompileExpression("G()", out var error, testData));
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_41()
        {
            // static local function (no captures allowed)
            var source = """
class C
{
    void F()
    {
        static int G(int x) { return x + 1; }
        _ = G(1); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("G(42)", out var error, testData);
                Assert.Null(error);
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_42()
        {
            // TODO2: Local function capturing from an async method (state machine hoisted locals).
            // The display class is a field on the state machine struct, not a local/parameter
            // directly accessible in the EE method. FindDisplayClassInstance can't find it.
            var source = """
using System.Threading.Tasks;
class C
{
    async Task F()
    {
        int captured = 10;
        int G() { return captured + 1; }
        _ = G();
        await Task.Yield(); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.<F>d__0.MoveNext", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("G()", out var error, testData);
                // TODO2: G is not found as a local function because the local function map
                // is emitted on the original method (C.F), not on the state machine's MoveNext.
                // Need to propagate local function info to state machine methods.
                Assert.Null(error);
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_43()
        {
            // scoped ref parameter
            var source = """
class C
{
    void F()
    {
        int G(scoped ref int x) { return x; }
        int v = 10;
        _ = G(ref v); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("G(ref v)", out var error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size        8 (0x8)
  .maxstack  1
  .locals init (int V_0) //v
  IL_0000:  ldloca.s   V_0
  IL_0002:  call       "int G(scoped ref int)"
  IL_0007:  ret
}
""");
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_44()
        {
            // scoped in parameter
            var source = """
class C
{
    void F()
    {
        int G(scoped in int x) { return x; }
        int v = 10;
        _ = G(in v); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));

                // with modifier
                var testData = new CompilationTestData();
                context.CompileExpression("G(in v)", out var error, testData);
                Assert.Null(error);
                testData.GetMethodData("<>x.<>m0").VerifyIL("""
{
  // Code size        8 (0x8)
  .maxstack  1
  .locals init (int V_0) //v
  IL_0000:  ldloca.s   V_0
  IL_0002:  call       "int G(scoped in int)"
  IL_0007:  ret
}
""");

                // without modifier
                testData = new CompilationTestData();
                context.CompileExpression("G(v)", out error, testData);
                Assert.Null(error);
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/60358")]
        public void LocalFunction_45()
        {
            // scoped ReadOnlySpan parameter
            var source = """
using System;
class C
{
    void F()
    {
        int G(scoped ReadOnlySpan<int> s) { return s.Length; }
        var arr = new int[] { 1, 2, 3 };
        _ = G(arr); /*BREAK*/
    }
}
""";
            var comp = CreateCompilation(source, options: TestOptions.DebugDll, targetFramework: TargetFramework.Net80);
            WithRuntimeInstance(comp, runtime =>
            {
                var context = CreateMethodContext(runtime, "C.F", atLineNumber: LineOf(source, "BREAK"));
                var testData = new CompilationTestData();
                context.CompileExpression("G(arr)", out var error, testData);
                Assert.Null(error);
            }, targetDebugFormat: DebugInformationFormat.PortablePdb);
        }
    }
}
