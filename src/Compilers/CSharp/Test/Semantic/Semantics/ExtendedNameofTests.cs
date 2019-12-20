// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Xunit;
using System.Linq;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests;

public class ExtendedNameofTests : CSharpTestBase
{
    // TODO2:
    // TODO2 test on record declaration
    // TODO2 test IDE completion
    // TODO2 test IDE QuickInfo
    [Fact]
    public void NameOfOnMethod()
    {
        var source = @"
class C
{
    [My(nameof(parameter))]
    void M(string parameter) { }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(source, parseOptions: TestOptions.Regular10);
        comp.VerifyDiagnostics(
            // (4,16): error CS8652: The feature 'extended nameof scope' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
            //     [My(nameof(parameter))]
            Diagnostic(ErrorCode.ERR_FeatureInPreview, "parameter").WithArguments("extended nameof scope").WithLocation(4, 16)
            );

        comp = CreateCompilation(source, parseOptions: TestOptions.RegularNext);
        comp.VerifyDiagnostics();
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter);
        // TODO2 test speculative semantic model
    }

    [Fact]
    public void NameOfOnMethod_FieldFromContainingType()
    {
        var source = @"
public class C
{
    public int field = 0;

    [My(nameof(field))]
    void M(string field) { }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";

        // binder -> binder (*) -> binder ->
        // lookup1: ignoring *
        // lookup2: full lookup (result is from * and langver is old, then langver error)

        // [Attribute(parameter)] // not found
        // nameof(parameter)
        // nameof(Method(parameter))

        // TODO2 need to reduce this break
        // Compat break: this previously was valid C# 10
        var comp = CreateCompilation(source, parseOptions: TestOptions.Regular10);
        comp.VerifyDiagnostics(
            // (6,16): error CS8652: The feature 'extended nameof scope' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
            //     [My(nameof(field))]
            Diagnostic(ErrorCode.ERR_FeatureInPreview, "field").WithArguments("extended nameof scope").WithLocation(6, 16)
            );
        CheckSymbolInNameof(comp, "nameof(field)", SymbolKind.Parameter, "void local(System.String parameter)");

        comp = CreateCompilation(source, parseOptions: TestOptions.RegularNext);
        comp.VerifyDiagnostics(
            // (6,16): error CS8652: The feature 'extended nameof scope' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
            //     [My(nameof(field))]
            Diagnostic(ErrorCode.ERR_FeatureInPreview, "field").WithArguments("extended nameof scope").WithLocation(6, 16)
            );
        CheckSymbolInNameof(comp, "nameof(field)", SymbolKind.Parameter, "void C.M(System.String field)");
    }

    [Fact]
    public void NameOfOnMethod_ConstantFromType()
    {
        var source = @"
public static class Type
{
    public const string Constant = ""hello"";
}
public class C
{
    [My(nameof(Type.Constant))]
    void M(string Type) { }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        // TODO2 need to bind to Type.Constant
        // TODO2 need to reduce this break
        // Compat break: this previously was valid C# 10
        var comp = CreateCompilation(source, parseOptions: TestOptions.Regular10);
        comp.VerifyDiagnostics(
            // (8,16): error CS8652: The feature 'extended nameof scope' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
            //     [My(nameof(Type.Constant))]
            Diagnostic(ErrorCode.ERR_FeatureInPreview, "Type").WithArguments("extended nameof scope").WithLocation(8, 16),
            // (8,21): error CS1061: 'string' does not contain a definition for 'Constant' and no accessible extension method 'Constant' accepting a first argument of type 'string' could be found (are you missing a using directive or an assembly reference?)
            //     [My(nameof(Type.Constant))]
            Diagnostic(ErrorCode.ERR_NoSuchMemberOrExtension, "Constant").WithArguments("string", "Constant").WithLocation(8, 21)
            );
        CheckSymbolInNameof(comp, "nameof(field)", SymbolKind.Parameter, "void local(System.String parameter)");

        comp = CreateCompilation(source, parseOptions: TestOptions.RegularNext);
        comp.VerifyDiagnostics(
            // (6,16): error CS8652: The feature 'extended nameof scope' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
            //     [My(nameof(field))]
            Diagnostic(ErrorCode.ERR_FeatureInPreview, "field").WithArguments("extended nameof scope").WithLocation(6, 16)
            );
        CheckSymbolInNameof(comp, "nameof(field)", SymbolKind.Parameter, "void C.M(System.String field)");
    }

    [Fact]
    public void NameOfOnConstructor()
    {
        var source = @"
class C
{
    [My(nameof(parameter))]
    C(string parameter) { }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(source, parseOptions: TestOptions.Regular10);
        comp.VerifyDiagnostics(
            // (4,16): error CS8652: The feature 'extended nameof scope' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
            //     [My(nameof(parameter))]
            Diagnostic(ErrorCode.ERR_FeatureInPreview, "parameter").WithArguments("extended nameof scope").WithLocation(4, 16)
            );
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter);

        comp = CreateCompilation(source, parseOptions: TestOptions.RegularNext);
        comp.VerifyDiagnostics();
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter);
    }

    [Fact]
    public void ConstantOnMethod_CompatScenario()
    {
        var source = @"
class C
{
    const string constant = """";
    [My(constant)]
    void M(string constant) { }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(source, parseOptions: TestOptions.Regular10);
        comp.VerifyDiagnostics();
        checkSymbol(comp);

        comp = CreateCompilation(source, parseOptions: TestOptions.RegularNext);
        comp.VerifyDiagnostics();
        checkSymbol(comp);

        // Binding outside nameof unaffected

        static void checkSymbol(CSharpCompilation comp)
        {
            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var expression = tree.GetRoot().DescendantNodes().OfType<AttributeArgumentSyntax>().Single().Expression;
            Assert.Equal("constant", expression.ToString());
            Assert.Equal(SymbolKind.Field, model.GetSymbolInfo(expression).Symbol.Kind);
        }
    }

    [Fact]
    public void MethodNamedNameof()
    {
        var source = @"
class C
{
    const string constant = """";
    [My(nameof(constant))]
    void M(string constant) { }

    static string nameof(string x) => throw null;
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(source);
        comp.VerifyDiagnostics(
            // (5,9): error CS0182: An attribute argument must be a constant expression, typeof expression or array creation expression of an attribute parameter type
            //     [My(nameof(constant))]
            Diagnostic(ErrorCode.ERR_BadAttributeArgument, "nameof(constant)").WithLocation(5, 9)
            );
        checkSymbol(comp);

        static void checkSymbol(CSharpCompilation comp)
        {
            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var expression = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single().ArgumentList.Arguments[0].Expression;
            Assert.Equal("constant", expression.ToString());
            Assert.Equal(SymbolKind.Field, model.GetSymbolInfo(expression).Symbol.Kind);
        }
    }

    [Fact]
    public void ConstantOnConstructor_CompatScenario()
    {
        var source = @"
class C
{
    const string constant = """";
    [My(constant)]
    C(string constant) { }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(source, parseOptions: TestOptions.Regular10);
        comp.VerifyDiagnostics();
        checkSymbol(comp);

        comp = CreateCompilation(source, parseOptions: TestOptions.RegularNext);
        comp.VerifyDiagnostics();
        checkSymbol(comp);

        // Binding outside nameof unaffected

        static void checkSymbol(CSharpCompilation comp)
        {
            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var expression = tree.GetRoot().DescendantNodes().OfType<AttributeArgumentSyntax>().Single().Expression;
            Assert.Equal("constant", expression.ToString());
            Assert.Equal(SymbolKind.Field, model.GetSymbolInfo(expression).Symbol.Kind);
        }
    }

    [Fact]
    public void ConstantOnIndexer_CompatScenario()
    {
        var source = @"
class C
{
    const string constant = """";
    [My(constant)]
    int this[string constant] { get { throw null; } set { throw null; } }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(source, parseOptions: TestOptions.Regular10);
        comp.VerifyDiagnostics();
        checkSymbol(comp);

        comp = CreateCompilation(source, parseOptions: TestOptions.RegularNext);
        comp.VerifyDiagnostics();
        checkSymbol(comp);

        // Binding outside nameof unaffected

        static void checkSymbol(CSharpCompilation comp)
        {
            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var expression = tree.GetRoot().DescendantNodes().OfType<AttributeArgumentSyntax>().Single().Expression;
            Assert.Equal("constant", expression.ToString());
            Assert.Equal(SymbolKind.Field, model.GetSymbolInfo(expression).Symbol.Kind);
        }
    }

    [Fact]
    public void NameofOnIndexer()
    {
        var source = @"
class C
{
    const string constant = """";
    [My(nameof(constant))]
    int this[string constant] { get { throw null; } set { throw null; } }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(source, parseOptions: TestOptions.Regular10);
        comp.VerifyDiagnostics();
        checkSymbol(comp);

        comp = CreateCompilation(source, parseOptions: TestOptions.RegularNext);
        comp.VerifyDiagnostics();
        checkSymbol(comp);

        static void checkSymbol(CSharpCompilation comp)
        {
            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var expression = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single().ArgumentList.Arguments[0].Expression;
            Assert.Equal("constant", expression.ToString());
            Assert.Equal(SymbolKind.Field, model.GetSymbolInfo(expression).Symbol.Kind);
        }
    }

    [Fact]
    public void ConstantFieldOnMethod_CompatScenario()
    {
        var source = @"
class C
{
    const string field = """";
    [My(nameof(field))]
    void M<TParameter>(string field) { }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        // Binding inside nameof now finds parameter instead of constant field

        var comp = CreateCompilation(source, parseOptions: TestOptions.Regular10);
        comp.VerifyDiagnostics(
            // (5,16): error CS8652: The feature 'extended nameof scope' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
            //     [My(nameof(field))]
            Diagnostic(ErrorCode.ERR_FeatureInPreview, "field").WithArguments("extended nameof scope").WithLocation(5, 16)
            );
        CheckSymbolInNameof(comp, "nameof(field)", SymbolKind.Parameter);

        comp = CreateCompilation(source, parseOptions: TestOptions.RegularNext);
        comp.VerifyDiagnostics();
        CheckSymbolInNameof(comp, "nameof(field)", SymbolKind.Parameter);
    }

    static void CheckSymbolInNameof(CSharpCompilation comp, string expectedSyntax, SymbolKind expectedKind, string containingSymbol = null)
    {
        var tree = comp.SyntaxTrees.First();
        var model = comp.GetSemanticModel(tree, ignoreAccessibility: false);
        var invocation = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Where(i => i.Expression.ToString() == "nameof").Single();
        Assert.Equal(expectedSyntax, invocation.ToString());
        var symbol = model.GetSymbolInfo(invocation.ArgumentList.Arguments.Single().Expression).Symbol;
        Assert.Equal(expectedKind, symbol.Kind);

        if (containingSymbol is not null)
        {
            Assert.Equal(containingSymbol, symbol.ContainingSymbol.ToTestDisplayString());
        }
    }

    [Fact]
    public void NameOfOnMethod_TypeParameter()
    {
        var source = @"
class C
{
    [My(nameof(TParameter))]
    void M<TParameter>() { }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(source);
        comp.VerifyDiagnostics(
            // (4,16): error CS0103: The name 'TParameter' does not exist in the current context
            //     [My(nameof(TParameter))]
            Diagnostic(ErrorCode.ERR_NameNotInContext, "TParameter").WithArguments("TParameter").WithLocation(4, 16)
            );
    }

    [Fact]
    public void NameOfOnMethod_BothParameterAndTypeParameter()
    {
        var source = @"
class C
{
    [My(nameof(P))]
    void M<P>(string P) { }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(source);
        CheckSymbolInNameof(comp, "nameof(P)", SymbolKind.Parameter);
        comp.VerifyDiagnostics(
            // (5,22): error CS0412: 'P': a parameter, local variable, or local function cannot have the same name as a method type parameter
            //     void M<P>(string P) { }
            Diagnostic(ErrorCode.ERR_LocalSameNameAsTypeParam, "P").WithArguments("P").WithLocation(5, 22)
            );
    }

    [Fact]
    public void NameOfOnLocalFunction_ParameterFromContainingMethod()
    {
        var source = @"
class C
{
    void M(string parameter)
    {
        local(null);

        [My(nameof(parameter))]
        void local(string other) { }
    }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(source, parseOptions: TestOptions.Regular10);
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter, "void C.M(System.String parameter)");
        comp.VerifyDiagnostics();

        comp = CreateCompilation(source, parseOptions: TestOptions.RegularNext);
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter, "void C.M(System.String parameter)");
        comp.VerifyDiagnostics();
    }

    [Fact]
    public void NameOfOnLocalFunction_ParameterFromContainingMethod_Shadowed()
    {
        var source = @"
class C
{
    void M(string parameter)
    {
        local(null);

        [My(nameof(parameter))]
        void local(string parameter) { }
    }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        // Compat break: this previously was valid C# 10
        var comp = CreateCompilation(source, parseOptions: TestOptions.Regular10);
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter, "void local(System.String parameter)");
        comp.VerifyDiagnostics(
            // (8,20): error CS8652: The feature 'extended nameof scope' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
            //         [My(nameof(parameter))]
            Diagnostic(ErrorCode.ERR_FeatureInPreview, "parameter").WithArguments("extended nameof scope").WithLocation(8, 20)
            );

        comp = CreateCompilation(source, parseOptions: TestOptions.RegularNext);
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter, "void local(System.String parameter)");
        comp.VerifyDiagnostics();
    }

    [Fact]
    public void NameOfOnLocalFunction_ParameterFromContainingMethod_Shadowed_TopLevelStatement()
    {
        var source = @"
local(null);

[My(nameof(args))]
void local(string args) { }

public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(source, parseOptions: TestOptions.Regular10);
        CheckSymbolInNameof(comp, "nameof(args)", SymbolKind.Parameter, "void local(System.String args)");
        comp.VerifyDiagnostics(
            // (4,12): error CS8652: The feature 'extended nameof scope' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
            // [My(nameof(args))]
            Diagnostic(ErrorCode.ERR_FeatureInPreview, "args").WithArguments("extended nameof scope").WithLocation(4, 12)
            );

        comp = CreateCompilation(source, parseOptions: TestOptions.RegularNext);
        CheckSymbolInNameof(comp, "nameof(args)", SymbolKind.Parameter, "void local(System.String args)");
        comp.VerifyDiagnostics();
    }

    [Fact]
    public void NameOfOnLocalFunction_TypeParameter()
    {
        var source = @"
class C
{
    void M()
    {
        local<object>(null);

        [My(nameof(TParameter))]
        void local<TParameter>(string parameter) { }
    }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        // The type parameter of the local function should not be in scope outside of the local function (expecting error)
        // Tracked by https://github.com/dotnet/roslyn/issues/59775
        var comp = CreateCompilation(source, parseOptions: TestOptions.Regular10);
        comp.VerifyDiagnostics();
        CheckSymbolInNameof(comp, "nameof(TParameter)", SymbolKind.TypeParameter);

        comp = CreateCompilation(source);
        comp.VerifyDiagnostics();
        CheckSymbolInNameof(comp, "nameof(TParameter)", SymbolKind.TypeParameter);
    }

    [Fact]
    public void NameOfOnLocalFunction()
    {
        var source = @"
class C
{
    void M()
    {
        local<object>(null);

        [My(nameof(parameter))]
        void local<TParameter>(string parameter) { }
    }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(source, parseOptions: TestOptions.Regular10);
        comp.VerifyDiagnostics(
            // (8,20): error CS8652: The feature 'extended nameof scope' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
            //         [My(nameof(parameter))]
            Diagnostic(ErrorCode.ERR_FeatureInPreview, "parameter").WithArguments("extended nameof scope").WithLocation(8, 20)
            );
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter, "void local<TParameter>(System.String parameter)");

        comp = CreateCompilation(source, parseOptions: TestOptions.RegularNext);
        comp.VerifyDiagnostics();
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter, "void local<TParameter>(System.String parameter)");
    }

    [Fact]
    public void NameOfOnLocalFunction_TypeParameterFromContainingMethod()
    {
        var source = @"
class C
{
    void M<TParameter>(string parameter)
    {
        local<object>(null);

        [My(nameof(TParameter))]
        void local<TParameter>(string parameter) { }
    }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(source, parseOptions: TestOptions.Regular10);
        comp.VerifyDiagnostics(
            // (9,20): warning CS8387: Type parameter 'TParameter' has the same name as the type parameter from outer method 'C.M<TParameter>(string)'
            //         void local<TParameter>(string parameter) { }
            Diagnostic(ErrorCode.WRN_TypeParameterSameAsOuterMethodTypeParameter, "TParameter").WithArguments("TParameter", "C.M<TParameter>(string)").WithLocation(9, 20)
            );
        CheckSymbolInNameof(comp, "nameof(TParameter)", SymbolKind.TypeParameter, "void local<TParameter>(System.String parameter)");

        comp = CreateCompilation(source);
        comp.VerifyDiagnostics(
            // (9,20): warning CS8387: Type parameter 'TParameter' has the same name as the type parameter from outer method 'C.M<TParameter>(string)'
            //         void local<TParameter>(string parameter) { }
            Diagnostic(ErrorCode.WRN_TypeParameterSameAsOuterMethodTypeParameter, "TParameter").WithArguments("TParameter", "C.M<TParameter>(string)").WithLocation(9, 20)
            );
        CheckSymbolInNameof(comp, "nameof(TParameter)", SymbolKind.TypeParameter, "void local<TParameter>(System.String parameter)");
    }

    [Fact]
    public void NameOfOnLocalFunction_ShadowedParameterFromContainingMethod()
    {
        var source = @"
class C
{
    void M<TParameter>(string parameter)
    {
        local<object>(null);

        [My(nameof(parameter))]
        void local<TParameter>(string parameter) { }
    }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(source, parseOptions: TestOptions.Regular10);
        comp.VerifyDiagnostics(
            // (8,20): error CS8652: The feature 'extended nameof scope' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
            //         [My(nameof(parameter))]
            Diagnostic(ErrorCode.ERR_FeatureInPreview, "parameter").WithArguments("extended nameof scope").WithLocation(8, 20),
            // (9,20): warning CS8387: Type parameter 'TParameter' has the same name as the type parameter from outer method 'C.M<TParameter>(string)'
            //         void local<TParameter>(string parameter) { }
            Diagnostic(ErrorCode.WRN_TypeParameterSameAsOuterMethodTypeParameter, "TParameter").WithArguments("TParameter", "C.M<TParameter>(string)").WithLocation(9, 20)
            );
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter, "void local<TParameter>(System.String parameter)");

        comp = CreateCompilation(source);
        comp.VerifyDiagnostics(
            // (9,20): warning CS8387: Type parameter 'TParameter' has the same name as the type parameter from outer method 'C.M<TParameter>(string)'
            //         void local<TParameter>(string parameter) { }
            Diagnostic(ErrorCode.WRN_TypeParameterSameAsOuterMethodTypeParameter, "TParameter").WithArguments("TParameter", "C.M<TParameter>(string)").WithLocation(9, 20)
            );
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter, "void local<TParameter>(System.String parameter)");
    }

    [Fact]
    public void NameOfOnLocalFunction_TypeParameterFromContainingMethod_ShadowedByParameter()
    {
        var source = @"
class C
{
    void M<P>()
    {
        local(null);

        [My(nameof(P))]
        void local(string P) { }
    }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(source, parseOptions: TestOptions.Regular10);
        comp.VerifyDiagnostics(
            // (8,20): error CS8652: The feature 'extended nameof scope' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
            //         [My(nameof(P))]
            Diagnostic(ErrorCode.ERR_FeatureInPreview, "P").WithArguments("extended nameof scope").WithLocation(8, 20)
            );
        CheckSymbolInNameof(comp, "nameof(P)", SymbolKind.Parameter, "void local(System.String P)");

        comp = CreateCompilation(source, parseOptions: TestOptions.RegularNext);
        comp.VerifyDiagnostics();
        CheckSymbolInNameof(comp, "nameof(P)", SymbolKind.Parameter, "void local(System.String P)");
    }

    [Fact]
    public void NameOfOnLambda()
    {
        var source = @"
class C
{
    void M<TParameter>(string parameter)
    {
        var lambda = [My(nameof(parameter))] void (string parameter) => { };
    }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(source, parseOptions: TestOptions.Regular10);
        comp.VerifyDiagnostics(
            // (6,33): error CS8652: The feature 'extended nameof scope' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
            //         var lambda = [My(nameof(parameter))] void (string parameter) => { };
            Diagnostic(ErrorCode.ERR_FeatureInPreview, "parameter").WithArguments("extended nameof scope").WithLocation(6, 33)
            );
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter, "lambda expression");

        comp = CreateCompilation(source);
        comp.VerifyDiagnostics();
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter, "lambda expression");
    }

    [Fact]
    public void NameOfOnLambdaParameter()
    {
        var source = @"
class C
{
    void M()
    {
        var x = void ([My(nameof(parameter))] string parameter) => { };
    }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(source, parseOptions: TestOptions.Regular10);
        comp.VerifyDiagnostics(
            // (6,34): error CS8652: The feature 'extended nameof scope' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
            //         var x = void ([My(nameof(parameter))] string parameter) => { };
            Diagnostic(ErrorCode.ERR_FeatureInPreview, "parameter").WithArguments("extended nameof scope").WithLocation(6, 34)
            );
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter, "lambda expression");

        comp = CreateCompilation(source);
        comp.VerifyDiagnostics();
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter, "lambda expression");
    }

    [Fact]
    public void NameOfOnAnonymousFunction()
    {
        var source = @"
class C
{
    void M()
    {
        var x = [My(nameof(parameter))] delegate([My(nameof(parameter2))] string parameter, string parameter2) { };
    }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(source);
        comp.VerifyDiagnostics(
            // (6,17): error CS1525: Invalid expression term '['
            //         var x = [My(nameof(parameter))] delegate([My(nameof(parameter2))] string parameter, string parameter2) { };
            Diagnostic(ErrorCode.ERR_InvalidExprTerm, "[").WithArguments("[").WithLocation(6, 17),
            // (6,18): error CS0103: The name 'My' does not exist in the current context
            //         var x = [My(nameof(parameter))] delegate([My(nameof(parameter2))] string parameter, string parameter2) { };
            Diagnostic(ErrorCode.ERR_NameNotInContext, "My").WithArguments("My").WithLocation(6, 18),
            // (6,28): error CS0103: The name 'parameter' does not exist in the current context
            //         var x = [My(nameof(parameter))] delegate([My(nameof(parameter2))] string parameter, string parameter2) { };
            Diagnostic(ErrorCode.ERR_NameNotInContext, "parameter").WithArguments("parameter").WithLocation(6, 28),
            // (6,41): error CS1002: ; expected
            //         var x = [My(nameof(parameter))] delegate([My(nameof(parameter2))] string parameter, string parameter2) { };
            Diagnostic(ErrorCode.ERR_SemicolonExpected, "delegate").WithLocation(6, 41),
            // (6,41): error CS0201: Only assignment, call, increment, decrement, await, and new object expressions can be used as a statement
            //         var x = [My(nameof(parameter))] delegate([My(nameof(parameter2))] string parameter, string parameter2) { };
            Diagnostic(ErrorCode.ERR_IllegalStatement, "delegate([My(nameof(parameter2))] string parameter, string parameter2) { }").WithLocation(6, 41),
            // (6,50): error CS7014: Attributes are not valid in this context.
            //         var x = [My(nameof(parameter))] delegate([My(nameof(parameter2))] string parameter, string parameter2) { };
            Diagnostic(ErrorCode.ERR_AttributesNotAllowed, "[My(nameof(parameter2))]").WithLocation(6, 50)
            );
    }

    [Fact]
    public void NameOfOnMethodParameter()
    {
        var source = @"
class C
{
    void M([My(nameof(parameter))] string parameter)
    {
    }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(source, parseOptions: TestOptions.Regular10);
        comp.VerifyDiagnostics(
            // (4,23): error CS8652: The feature 'extended nameof scope' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
            //     void M([My(nameof(parameter))] string parameter)
            Diagnostic(ErrorCode.ERR_FeatureInPreview, "parameter").WithArguments("extended nameof scope").WithLocation(4, 23)
            );
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter);

        comp = CreateCompilation(source);
        comp.VerifyDiagnostics();
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter);
    }

    [Fact]
    public void NameOfOnMethodParameter_ReferencingOtherParameter()
    {
        var source = @"
class C
{
    void M([My(nameof(parameter))] string x, string parameter)
    {
    }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(source, parseOptions: TestOptions.Regular10);
        comp.VerifyDiagnostics(
            // (4,23): error CS8652: The feature 'extended nameof scope' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
            //     void M([My(nameof(parameter))] string x,  string parameter)
            Diagnostic(ErrorCode.ERR_FeatureInPreview, "parameter").WithArguments("extended nameof scope").WithLocation(4, 23)
            );
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter);

        comp = CreateCompilation(source);
        comp.VerifyDiagnostics();
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter);
    }

    [Fact]
    public void NameOfOnMethodParameter_ReferencingOtherParameter_OtherOrder()
    {
        var source = @"
class C
{
    void M(string parameter, [My(nameof(parameter))] string x)
    {
    }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(source, parseOptions: TestOptions.Regular10);
        comp.VerifyDiagnostics(
            // (4,41): error CS8652: The feature 'extended nameof scope' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
            //     void M(string parameter, [My(nameof(parameter))] string x)
            Diagnostic(ErrorCode.ERR_FeatureInPreview, "parameter").WithArguments("extended nameof scope").WithLocation(4, 41)
            );
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter);

        comp = CreateCompilation(source);
        comp.VerifyDiagnostics();
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter);
    }

    [Fact]
    public void NameOfOnConstructorParameter()
    {
        var source = @"
class C
{
    C([My(nameof(parameter))] string parameter) { }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(source, parseOptions: TestOptions.Regular10);
        comp.VerifyDiagnostics(
            // (4,18): error CS8652: The feature 'extended nameof scope' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
            //     C([My(nameof(parameter))] string parameter) { }
            Diagnostic(ErrorCode.ERR_FeatureInPreview, "parameter").WithArguments("extended nameof scope").WithLocation(4, 18)
            );
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter);

        comp = CreateCompilation(source);
        comp.VerifyDiagnostics();
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter);
    }

    [Fact]
    public void NameOfOnMethod_WithOutVar()
    {
        var source = @"
class C
{
    [My(M2(out var parameter), nameof(parameter))]
    void M(string parameter) { }

    static string M2(out string p) => throw null;
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string a, string b) { }
}
";
        var comp = CreateCompilation(source, parseOptions: TestOptions.Regular10);
        comp.VerifyDiagnostics(
            // (4,9): error CS0182: An attribute argument must be a constant expression, typeof expression or array creation expression of an attribute parameter type
            //     [My(M2(out var parameter), nameof(parameter))]
            Diagnostic(ErrorCode.ERR_BadAttributeArgument, "M2(out var parameter)").WithLocation(4, 9),
            // (4,39): error CS8652: The feature 'extended nameof scope' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
            //     [My(M2(out var parameter), nameof(parameter))]
            Diagnostic(ErrorCode.ERR_FeatureInPreview, "parameter").WithArguments("extended nameof scope").WithLocation(4, 39)
            );
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter);

        comp = CreateCompilation(source);
        comp.VerifyDiagnostics(
            // (4,9): error CS0182: An attribute argument must be a constant expression, typeof expression or array creation expression of an attribute parameter type
            //     [My(M2(out var parameter), nameof(parameter))]
            Diagnostic(ErrorCode.ERR_BadAttributeArgument, "M2(out var parameter)").WithLocation(4, 9)
            );
        CheckSymbolInNameof(comp, "nameof(parameter)", SymbolKind.Parameter);
    }

    [Fact]
    public void NameOfOnRecord()
    {
        var source = @"
[My(nameof(property))]
record C(string property)
{
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(new[] { source, IsExternalInitTypeDefinition }, parseOptions: TestOptions.Regular10);
        comp.VerifyDiagnostics();
        CheckSymbolInNameof(comp, "nameof(property)", SymbolKind.Property);

        comp = CreateCompilation(new[] { source, IsExternalInitTypeDefinition }, parseOptions: TestOptions.RegularNext);
        comp.VerifyDiagnostics();
        CheckSymbolInNameof(comp, "nameof(property)", SymbolKind.Property);
    }

    [Fact]
    public void NameOfOnRecordConstructor()
    {
        var source = @"
[method: My(nameof(parameter))]
record C(string parameter)
{
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(new[] { source, IsExternalInitTypeDefinition });
        comp.VerifyDiagnostics(
            // (2,2): warning CS0657: 'method' is not a valid attribute location for this declaration. Valid attribute locations for this declaration are 'type'. All attributes in this block will be ignored.
            // [method: My(nameof(parameter))]
            Diagnostic(ErrorCode.WRN_AttributeLocationOnBadDeclaration, "method").WithArguments("method", "type").WithLocation(2, 2)
            );
    }

    [Fact]
    public void OnlyInNameOf()
    {
        var source = @"
class C
{
    [My(parameter)]
    void M(string parameter, [My(parameter)] string x)
    {
        var lambda = [My(parameter2)] void(string parameter2, [My(parameter2)] string x2) => { };

        local(null, null);

        [My(parameter3)]
        void local(string parameter3, [My(parameter3)] string x3) { }
    }
}
public class MyAttribute : System.Attribute
{
    public MyAttribute(string name) { }
}
";
        var comp = CreateCompilation(source);
        comp.VerifyDiagnostics(
            // (4,9): error CS0103: The name 'parameter' does not exist in the current context
            //     [My(parameter)]
            Diagnostic(ErrorCode.ERR_NameNotInContext, "parameter").WithArguments("parameter").WithLocation(4, 9),
            // (5,34): error CS0103: The name 'parameter' does not exist in the current context
            //     void M(string parameter, [My(parameter)] string x)
            Diagnostic(ErrorCode.ERR_NameNotInContext, "parameter").WithArguments("parameter").WithLocation(5, 34),
            // (7,26): error CS0103: The name 'parameter2' does not exist in the current context
            //         var lambda = [My(parameter2)] void(string parameter2, [My(parameter2)] string x2) => { };
            Diagnostic(ErrorCode.ERR_NameNotInContext, "parameter2").WithArguments("parameter2").WithLocation(7, 26),
            // (7,67): error CS0182: An attribute argument must be a constant expression, typeof expression or array creation expression of an attribute parameter type
            //         var lambda = [My(parameter2)] void(string parameter2, [My(parameter2)] string x2) => { };
            Diagnostic(ErrorCode.ERR_BadAttributeArgument, "parameter2").WithLocation(7, 67),
            // (11,13): error CS0103: The name 'parameter3' does not exist in the current context
            //         [My(parameter3)]
            Diagnostic(ErrorCode.ERR_NameNotInContext, "parameter3").WithArguments("parameter3").WithLocation(11, 13),
            // (12,43): error CS0103: The name 'parameter3' does not exist in the current context
            //         void local(string parameter3, [My(parameter3)] string x3) { }
            Diagnostic(ErrorCode.ERR_NameNotInContext, "parameter3").WithArguments("parameter3").WithLocation(12, 43)
            );
    }
}
