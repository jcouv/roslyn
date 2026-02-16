// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Microsoft.CodeAnalysis.CSharp.ExpressionEvaluator;

/// <summary>
/// Rewrites calls to <see cref="EELocalFunctionMethodSymbol"/> to target
/// the underlying <see cref="Symbols.Metadata.PE.PEMethodSymbol"/>, supplying
/// the hidden display class arguments that were filtered from the user-facing signature.
/// </summary>
internal sealed class LocalFunctionCallRewriter : BoundTreeRewriterWithStackGuardWithoutRecursionOnTheLeftOfBinaryOperator
{
    private readonly EEMethodSymbol _eeMethod;
    private readonly DiagnosticBag _diagnostics;

    private LocalFunctionCallRewriter(EEMethodSymbol eeMethod, DiagnosticBag diagnostics)
    {
        _eeMethod = eeMethod;
        _diagnostics = diagnostics;
    }

    internal static BoundNode Rewrite(EEMethodSymbol eeMethod, DiagnosticBag diagnostics, BoundNode node)
    {
        var rewriter = new LocalFunctionCallRewriter(eeMethod, diagnostics);
        return rewriter.Visit(node);
    }

    public override BoundNode? VisitCall(BoundCall node)
    {
        var visited = (BoundCall)base.VisitCall(node)!;

        if (visited.Method is not EELocalFunctionMethodSymbol localFunc || localFunc.HiddenParameters.IsEmpty)
        {
            return visited;
        }

        MethodSymbol underlyingMethod = localFunc.UnderlyingMethod;
        BoundExpression? receiverOpt = visited.ReceiverOpt;

        // Build the hidden arguments by finding the display class instance
        // in the EE method's locals that matches each hidden parameter's type.
        var hiddenArgs = ArrayBuilder<BoundExpression>.GetInstance(localFunc.HiddenParameters.Length);
        foreach (var hiddenParam in localFunc.HiddenParameters)
        {
            BoundExpression? displayClassExpr = FindDisplayClassInstance(visited.Syntax, hiddenParam);
            if (displayClassExpr == null)
            {
                // If we can't find the display class instance, bail out and
                // return the original node — binding will report an error downstream.
                hiddenArgs.Free();
                return visited;
            }

            hiddenArgs.Add(displayClassExpr);
        }

        // For non-static underlying methods, the last hidden parameter
        // (the synthetic `this`) becomes the receiver, not an argument.
        if (!underlyingMethod.IsStatic)
        {
            Debug.Assert(hiddenArgs.Count >= 1);
            receiverOpt = hiddenArgs.Last();
            hiddenArgs.RemoveLast();
        }

        // Build new arguments: user-provided args first, then hidden display class args.
        var newArgs = ArrayBuilder<BoundExpression>.GetInstance(visited.Arguments.Length + hiddenArgs.Count);
        newArgs.AddRange(visited.Arguments);
        newArgs.AddRange(hiddenArgs);
        hiddenArgs.Free();

        var newRefKinds = buildRefKinds(underlyingMethod, newArgs);
        var newArgNames = buildArgNames(visited, localFunc, underlyingMethod, newArgs);

        return visited.Update(
            receiverOpt,
            initialBindingReceiverIsSubjectToCloning: ThreeState.Unknown,
            underlyingMethod,
            newArgs.ToImmutableAndFree(),
            newArgNames,
            newRefKinds,
            visited.IsDelegateCall,
            visited.Expanded,
            visited.InvokedAsExtensionMethod,
            visited.ArgsToParamsOpt,
            visited.DefaultArguments,
            visited.ResultKind,
            visited.Type);

        // Build new refkinds: match the underlying method's parameter ref kinds.
        static ImmutableArray<RefKind> buildRefKinds(MethodSymbol underlyingMethod, ArrayBuilder<BoundExpression> newArgs)
        {
            var newRefKindsBuilder = ArrayBuilder<RefKind>.GetInstance(newArgs.Count);
            foreach (var param in underlyingMethod.Parameters)
            {
                newRefKindsBuilder.Add(param.RefKind);
            }

            return newRefKindsBuilder.ToImmutableAndFree();
        }

        // Build new argument names (preserve user's names, then none for hidden args).
        static ImmutableArray<string?> buildArgNames(BoundCall visited, EELocalFunctionMethodSymbol localFunc, MethodSymbol underlyingMethod, ArrayBuilder<BoundExpression> newArgs)
        {
            if (visited.ArgumentNamesOpt.IsDefault)
            {
                return default;
            }

            int hiddenArgCount = !underlyingMethod.IsStatic
                ? localFunc.HiddenParameters.Length - 1
                : localFunc.HiddenParameters.Length;

            var names = ArrayBuilder<string?>.GetInstance(newArgs.Count);
            names.AddRange(visited.ArgumentNamesOpt);
            for (int i = 0; i < hiddenArgCount; i++)
            {
                names.Add(null);
            }

            return names.ToImmutableAndFree();
        }
    }

    public override BoundNode? VisitDelegateCreationExpression(BoundDelegateCreationExpression node)
    {
        var visited = (BoundDelegateCreationExpression)base.VisitDelegateCreationExpression(node)!;

        if (visited.MethodOpt is not EELocalFunctionMethodSymbol localFunc)
        {
            return visited;
        }

        // TODO2 review
        var underlyingMethod = localFunc.UnderlyingMethod;

        // Non-capturing local functions are static methods with a matching signature —
        // swap the method symbol to the underlying method and the delegate creation works.
        if (localFunc.HiddenParameters.IsEmpty && underlyingMethod.IsStatic)
        {
            return visited.Update(visited.Argument, underlyingMethod, visited.IsExtensionMethod, visited.WasTargetTyped, visited.Type);
        }

        // TODO2 review comment
        // Capturing local functions cannot be used as delegate targets in the debugger.
        // In normal compilation, ClosureConversion transforms them into instance methods
        // on the display class (or static methods with extra display class parameters).
        // Neither form can be used directly as a delegate target in the EE without
        // synthesizing a wrapper, which is not yet implemented.
        _diagnostics.Add(new CSDiagnostic(
            new CSDiagnosticInfo(ErrorCode.ERR_DelegateConversionOfLocalFunctionInDebugger, localFunc.Name),
            visited.Syntax.Location));
        return visited;
    }

    /// <summary>
    /// Finds the display class instance in the EE method's locals or parameters
    /// that matches the given hidden parameter's type.
    /// </summary>
    private BoundExpression? FindDisplayClassInstance(SyntaxNode syntax, ParameterSymbol hiddenParam)
    {
        var targetType = hiddenParam.Type;

        // Search in locals (display class locals from the original frame).
        foreach (var local in _eeMethod.Locals)
        {
            if (TypeSymbol.Equals(local.Type, targetType, TypeCompareKind.ConsiderEverything))
            {
                return new BoundLocal(syntax, local, constantValueOpt: null, type: local.Type) { WasCompilerGenerated = true };
            }
        }

        // Search in parameters (display class passed as parameter to the frame method).
        foreach (var param in _eeMethod.Parameters)
        {
            if (TypeSymbol.Equals(param.Type, targetType, TypeCompareKind.ConsiderEverything))
            {
                return new BoundParameter(syntax, param) { WasCompilerGenerated = true };
            }
        }

        Debug.Fail($"Could not find display class instance for type {targetType}");
        return null;
    }
}

