// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp;

internal class EscapeAnalysis : BoundTreeVisitor<EscapeAnalysis.Arguments, EscapeAnalysis.Result>
{
    internal struct Arguments
    {
        internal static readonly Arguments Default = default;
        internal readonly bool CheckValEscape;
        internal readonly uint ValEscapeTo;

        internal readonly bool CheckRefEscape;
        internal readonly uint RefEscapeTo;

        // TODO2 probably need this
        //internal readonly bool CheckingReceiver;

        private Arguments(bool checkValEscape, uint valEscapeTo, bool checkRefEscape, uint refEscapeTo)
        {
            CheckValEscape = checkValEscape;
            ValEscapeTo = valEscapeTo;
            CheckRefEscape = checkRefEscape;
            RefEscapeTo = refEscapeTo;
        }

        internal static Arguments MakeEscapeConstraint(bool isByRef, uint valEscapeTo, uint refEscapeTo)
        {
            return new Arguments(!isByRef, valEscapeTo, isByRef, refEscapeTo);
        }
    }

    /// <summary>
    /// TODO2
    /// <seealso cref="Binder.ExternalScope"/>
    /// <seealso cref="Binder.TopLevelScope"/>
    /// </summary>
    internal struct Result
    {
        internal static readonly Result ExternalScope = new Result(Binder.ExternalScope, Binder.ExternalScope);
        internal static readonly Result TopLevelScope = new Result(Binder.TopLevelScope, Binder.TopLevelScope);
        internal static readonly Result NotApplicable = new Result(uint.MaxValue, uint.MaxValue, hasError: true);

        internal readonly bool HasError;
        internal readonly uint ValEscape;
        internal readonly uint RefEscape;

        internal Result(uint valEscape, uint refEscape, bool hasError = false)
        {
            ValEscape = valEscape;
            RefEscape = refEscape;
            HasError = hasError;
        }

        internal void Deconstruct(out bool hasError, out uint valEscape, out uint refEscape)
        {
            hasError = this.HasError;
            valEscape = this.ValEscape;
            refEscape = this.RefEscape;
        }

        internal Result Narrowest(Result other)
        {
            return this.Narrowest(other.HasError, other.ValEscape, other.RefEscape);
        }

        private Result Narrowest(bool hasError = false, uint valEscape = Binder.ExternalScope, uint refEscape = Binder.ExternalScope)
        {
            return new Result(
                NarrowestEscape(this.ValEscape, valEscape),
                NarrowestEscape(this.RefEscape, refEscape),
                this.HasError || hasError);
        }

        private static uint NarrowestEscape(uint one, uint other)
        {
            return Math.Max(one, other);
        }
    }

    private readonly bool _useUpdatedEscapeRules;
    private readonly BindingDiagnosticBag _diagnostics;

    private EscapeAnalysis(bool useUpdateEscapeRules, BindingDiagnosticBag diagnostics)
    {
        _useUpdatedEscapeRules = useUpdateEscapeRules;
        _diagnostics = diagnostics;
    }

    private void Free()
    {
        _variableEscapes.Free();
    }

    /// <summary>
    /// Record the val- and ref-escape scopes for local variables.
    /// </summary>
    private readonly PooledDictionary<LocalSymbol, (uint, uint)> _variableEscapes = SpecializedSymbolCollections.GetPooledSymbolDictionaryInstance<LocalSymbol, (uint, uint)>();

    private (uint, uint) GetVariableEscape(LocalSymbol local)
    {
        if (_variableEscapes.TryGetValue(local, out var result))
        {
            return result;
        }
        throw ExceptionUtilities.Unreachable;
    }

    private void SetVariableEscape(LocalSymbol local, (uint, uint) scopes)
    {
        _variableEscapes.Add(local, scopes);
    }

    internal static void Analyze(BoundNode node, bool useUpdatedEscapeRules, BindingDiagnosticBag diagnostics)
    {
        var analyzer = new EscapeAnalysis(useUpdatedEscapeRules, diagnostics);
        analyzer.Visit(node, Arguments.Default);
        analyzer.Free();
    }

    public override Result VisitAssignmentOperator(BoundAssignmentOperator node, Arguments arg)
    {
        var leftResult = Visit(node.Left, arg);
        bool isByRef = node.IsRef;
        var rightResult = Visit(node.Right, Arguments.MakeEscapeConstraint(isByRef, leftResult.ValEscape, leftResult.RefEscape));
        return rightResult;
    }

    public override Result VisitReturnStatement(BoundReturnStatement node, Arguments arg)
    {
        var expression = node.ExpressionOpt;
        if (expression is null)
        {
            return Result.NotApplicable;
        }

        Debug.Assert(arg.RefEscapeTo == Binder.ExternalScope);
        Debug.Assert(arg.ValEscapeTo == Binder.ExternalScope);
        bool isByRef = node.RefKind != RefKind.None;

        _ = Visit(expression, Arguments.MakeEscapeConstraint(isByRef, Binder.ExternalScope, Binder.ExternalScope));
        return Result.NotApplicable;
    }

    public override Result VisitCall(BoundCall call, Arguments arg)
    {
        throw new NotImplementedException("TODO2");
    }

    private Result VisitInvocation(
        Symbol symbol,
        BoundExpression? receiver,
        ImmutableArray<ParameterSymbol> parameters,
        ImmutableArray<BoundExpression> argsOpt,
        ImmutableArray<RefKind> argRefKindsOpt,
        ImmutableArray<int> argsToParamsOpt,
        Arguments arg)
    {
        // TODO2
        ArrayBuilder<bool>? inParametersMatchedWithArgs = null;

        if (!argsOpt.IsDefault)
        {
            //moreArguments: // TODO2
            for (var argIndex = 0; argIndex < argsOpt.Length; argIndex++)
            {
                var argument = argsOpt[argIndex];
                // TODO2
                //if (argument.Kind == BoundKind.ArgListOperator)
                //{
                //    Debug.Assert(argIndex == argsOpt.Length - 1, "vararg must be the last");
                //    var argList = (BoundArgListOperator)argument;

                //    // unwrap varargs and process as more arguments
                //    argsOpt = argList.Arguments;
                //    // ref kinds of varargs are not interesting here. 
                //    // __refvalue is not ref-returnable, so ref varargs can't come back from a call
                //    argRefKindsOpt = default;
                //    parameters = ImmutableArray<ParameterSymbol>.Empty;
                //    argsToParamsOpt = default;

                //    goto moreArguments;
                //}

                RefKind effectiveRefKind = Binder.GetEffectiveRefKindAndMarkMatchedInParameter(argIndex, argRefKindsOpt, parameters, argsToParamsOpt, ref inParametersMatchedWithArgs, out DeclarationScope scope);

                // ref escape scope is the narrowest of 
                // - ref escape of all byref arguments
                // - val escape of all byval arguments  (ref-like values can be unwrapped into refs, so treat val escape of values as possible ref escape of the result)
                //
                // val escape scope is the narrowest of 
                // - val escape of all byval arguments  (refs cannot be wrapped into values, so their ref escape is irrelevant, only use val escapes)

                //bool? useRefEscape = arg.Binder.UseRefEscapeOfInvocationArgument(symbol, effectiveRefKind, isRefEscape, scope);
                //if (useRefEscape == null)
                //{
                //    continue;
                //}

                //var argEscape = useRefEscape.GetValueOrDefault() ?
                //                    GetRefEscape(argument, scopeOfTheContainingExpression) :
                //                    GetValEscape(argument, scopeOfTheContainingExpression);

                //escapeScope = Math.Max(escapeScope, argEscape);

                //if (escapeScope >= scopeOfTheContainingExpression)
                //{
                //    // no longer needed
                //    inParametersMatchedWithArgs?.Free();

                //    // can't get any worse
                //    return escapeScope;
                //}
            }
        }

        //// handle omitted optional "in" parameters if there are any
        //ParameterSymbol? unmatchedInParameter = TryGetUnmatchedInParameterAndFreeMatchedArgs(parameters, ref inParametersMatchedWithArgs);

        //// unmatched "in" parameter is the same as a literal, its ref escape is scopeOfTheContainingExpression  (can't get any worse)
        ////                                                    its val escape is ExternalScope                   (does not affect overall result)
        //if (unmatchedInParameter != null && isRefEscape)
        //{
        //    return scopeOfTheContainingExpression;
        //}

        //if (!symbol.RequiresInstanceReceiver())
        //{
        //    // ignore receiver when symbol is static
        //    var receiverResult = Visit(call.ReceiverOpt, arg);
        //    result = result.Narrowest(receiverResult.HasError, receiverResult.ValEscape, receiverResult.ValEscape);
        //}

        //return result;
        return Result.NotApplicable;
    }

    public override Result VisitParameter(BoundParameter node, Arguments arg)
    {
        throw new NotImplementedException("TODO2");
    }

    public override Result VisitLocalDeclaration(BoundLocalDeclaration node, Arguments arg)
    {
        Debug.Assert(!arg.CheckValEscape && !arg.CheckRefEscape);
        bool isByRef = node.LocalSymbol.IsRef;
        var localSymbol = node.LocalSymbol;

        // TODO2 when there is an initializer but the scope of the local is determined by modifiers,
        // we should check the initializer when visiting it
        var (_, initializerValEscape, initializerRefEscape) = (node.InitializerOpt is { } initializer)
            ? Visit(initializer, arg)
            : Result.NotApplicable;

        uint valEscape = getLocalValEscape(node, localSymbol, initializerValEscape);
        uint refEscape = getLocalRefEscape(node, localSymbol, initializerRefEscape);
        SetVariableEscape(localSymbol, (valEscape, refEscape));

        return Result.NotApplicable;

        uint getLocalValEscape(BoundLocalDeclaration node, LocalSymbol localSymbol, uint initializerValEscape)
        {
            if (!localSymbol.Type.IsRefLikeType)
            {
                // a local whose type is not a ref struct type is safe-to-return from the entire enclosing method
                return Binder.ExternalScope;
            }

            if (_useUpdatedEscapeRules && localSymbol.Scope == DeclarationScope.ValueScoped)
            {
                // scoped only has impact when applied to values which are ref struct
                return localSymbol.ScopeDepth;
            }

            if (node.InitializerOpt is { })
            {
                // Otherwise the variable's type is a ref struct type, and the variable's declaration requires an initializer.
                // The variable's safe-to-escape scope is the same as the safe-to-escape of its initializer.
                return initializerValEscape;
            }

            // A local of ref struct type and uninitialized at the point of declaration is safe-to-return from the entire enclosing method.
            return Binder.ExternalScope;
        }

        uint getLocalRefEscape(BoundLocalDeclaration node, LocalSymbol localSymbol, uint initializerRefEscape)
        {
            if (_useUpdatedEscapeRules && localSymbol.Scope != DeclarationScope.Unscoped)
            {
                return (localSymbol.Scope == DeclarationScope.ValueScoped)
                    ? Binder.TopLevelScope // TODO2 should be localSymbol.ScopeDepth?
                    : Binder.ExternalScope; // w
            }

            if (isByRef && node.InitializerOpt is { })
            {
                // If the variable is a ref variable, then its ref-safe-to-escape is taken from the ref-safe-to-escape of its initializing expression
                return initializerRefEscape;
            }

            // The variable is ref-safe-to-escape the scope in which it was declared.
            return localSymbol.ScopeDepth;
        }
    }

    public override Result VisitLocal(BoundLocal node, Arguments arg)
    {
        bool hasError = false;
        var localSymbol = node.LocalSymbol;
        var (valEscape, refEscape) = GetVariableEscape(localSymbol);

        if (arg.CheckValEscape && valEscape.IsNarrowerThan(arg.ValEscapeTo))
        {
            _diagnostics.Add(ErrorCode.ERR_EscapeVariable, node.Syntax.Location, localSymbol);
            hasError = true;
        }

        if (arg.CheckRefEscape && refEscape.IsNarrowerThan(arg.RefEscapeTo))
        {
            //!Binder.CheckLocalRefEscape(node.Syntax, node, arg.RefEscapeTo, arg.CheckingReceiver, arg.Diagnostics))
            _diagnostics.Add(ErrorCode.ERR_EscapeVariable, node.Syntax.Location, localSymbol); // TODO2 wrong diagnostic (see CheckLocalRefEscape)
            hasError = true;
        }

        return new Result(valEscape, refEscape, hasError);
    }

    public override Result VisitThisReference(BoundThisReference node, Arguments arg)
    {
        // An lvalue designating a formal parameter is ref-safe-to-escape (by reference) as follows:
        // If the parameter is a ref, out, or in parameter, it is ref -safe - to - escape from the entire method(e.g. by a return ref statement); otherwise
        // If the parameter is the this parameter of a struct type, it is ref-safe-to-escape to the top-level scope of the method (but not from the entire method itself);
        // Otherwise the parameter is a value parameter, and it is ref -safe - to - escape to the top - level scope of the method(but not from the method itself).

        // An expression that is an rvalue designating the use of a formal parameter is safe-to-escape (by value) from the entire method (e.g. by a return statement). This applies to the this parameter as well.

        // The scoped annotation also means that the this parameter of a struct can now be defined as scoped ref T.
        return new Result(Binder.ExternalScope, Binder.TopLevelScope);
    }

    public override Result VisitLiteral(BoundLiteral node, Arguments arg)
    {
        return Result.ExternalScope;
    }

    public override Result VisitDefaultExpression(BoundDefaultExpression node, Arguments arg)
    {
        // A default expression is safe-to-escape from the entire enclosing method.
        return Result.ExternalScope;
    }

    public override Result VisitConvertedStackAllocExpression(BoundConvertedStackAllocExpression node, Arguments arg)
    {
        // A stackalloc expression is an rvalue that is safe-to-escape to the top-level scope of the method (but not from the entire method itself).
        return Result.TopLevelScope; // TODO2 should this be a local scope instead?
    }

    public override Result VisitStackAllocArrayCreation(BoundStackAllocArrayCreation node, Arguments arg)
    {
        // A stackalloc expression is an rvalue that is safe-to-escape to the top-level scope of the method (but not from the entire method itself).
        return Result.TopLevelScope; // TODO2 should this be a local scope instead?
    }

    public override Result VisitArrayCreation(BoundArrayCreation node, Arguments arg)
    {
        return Result.ExternalScope;
    }

    public override Result VisitBlock(BoundBlock node, Arguments arg)
    {
        foreach (var statements in node.Statements)
        {
            Visit(statements, arg);
        }

        return Result.NotApplicable;
    }

    public override Result VisitExpressionStatement(BoundExpressionStatement node, Arguments arg)
    {
        Visit(node.Expression, arg);
        return Result.NotApplicable;
    }

    public override Result VisitNonConstructorMethodBody(BoundNonConstructorMethodBody node, Arguments arg)
    {
        if (node.BlockBody is not null)
        {
            Visit(node.BlockBody, arg);
        }

        if (node.ExpressionBody is not null)
        {
            Visit(node.ExpressionBody, arg);
        }

        return Result.NotApplicable;
    }

    public override Result VisitConversion(BoundConversion node, Arguments arg)
    {
        return Visit(node.Operand, arg); // TODO2
    }

    public override Result VisitIfStatement(BoundIfStatement node, Arguments arg)
    {
        Debug.Assert(!arg.CheckValEscape && !arg.CheckRefEscape);
        _ = Visit(node.Condition, arg);
        _ = Visit(node.Consequence, arg);
        _ = Visit(node.AlternativeOpt, arg);
        return Result.NotApplicable;
    }

    public override Result VisitBadExpression(BoundBadExpression node, Arguments arg)
    {
        throw ExceptionUtilities.Unreachable;
    }

    public override Result DefaultVisit(BoundNode node, Arguments arg)
    {
        throw ExceptionUtilities.UnexpectedValue(node.Kind);
    }

    //private static bool IsIrrelevant(BoundExpression expr, Arguments arg)
    //{
    //    if (arg.CheckRefEscape)
    //    {
    //        return false;
    //    }

    //    return IsIrrelevant(expr);
    //}

    //private static bool IsIrrelevant(BoundExpression expr)
    //{
    //    if (expr.HasAnyErrors)
    //    {
    //        return true;
    //    }

    //    // constants/literals cannot refer to local state
    //    if (expr.ConstantValue != null)
    //    {
    //        return true;
    //    }

    //    // to have local-referring values an expression must have a ref-like type
    //    //if (expr.Type?.IsRefLikeType != true)
    //    //{
    //    //    return true;
    //    //}

    //    return false;
    //}
}

internal static class Extension
{
    internal static bool IsNarrowerThan(this uint exprScope, uint otherScope)
    {
        return exprScope > otherScope;
    }

    internal static bool IsWiderThan(this uint exprScope, uint otherScope)
    {
        return exprScope < otherScope;
    }
}
