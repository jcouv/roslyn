// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Microsoft.CodeAnalysis.CSharp.ExpressionEvaluator;

/// <summary>
/// Rewrites calls to <see cref="EELocalFunctionMethodSymbol"/> to target the underlying PE method,
/// supplying the hidden display class arguments that were filtered from the user-facing signature.
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

        if (visited.Method is not EELocalFunctionMethodSymbol localFunc
            || localFunc.HiddenParameters.IsEmpty)
        {
            return visited;
        }

        MethodSymbol underlyingMethod = localFunc.UnderlyingMethod;

        if (!buildArgs(visited, localFunc, _eeMethod, out BoundExpression? receiverOpt, out ImmutableArray<BoundExpression> newArgs))
        {
            return visited;
        }

        ImmutableArray<RefKind> newRefKinds = buildRefKinds(underlyingMethod, newArgs.Length);
        ImmutableArray<string?> newArgNames = buildArgNames(visited.ArgumentNamesOpt, newArgs.Length);

        return visited.Update(
            receiverOpt,
            initialBindingReceiverIsSubjectToCloning: ThreeState.Unknown,
            underlyingMethod,
            newArgs,
            newArgNames,
            newRefKinds,
            visited.IsDelegateCall,
            visited.Expanded,
            visited.InvokedAsExtensionMethod,
            visited.ArgsToParamsOpt,
            visited.DefaultArguments,
            visited.ResultKind,
            visited.Type);

        static bool buildArgs(
            BoundCall visited,
            EELocalFunctionMethodSymbol localFunc,
            EEMethodSymbol eeMethod,
            out BoundExpression? receiverOpt,
            out ImmutableArray<BoundExpression> newArgs)
        {
            MethodSymbol underlyingMethod = localFunc.UnderlyingMethod;
            receiverOpt = visited.ReceiverOpt;

            // Build the hidden arguments by finding the display class instance
            // in the EE method's locals that matches each hidden parameter's type.
            var hiddenArgs = ArrayBuilder<BoundExpression>.GetInstance(localFunc.HiddenParameters.Length);
            foreach (var hiddenParam in localFunc.HiddenParameters)
            {
                BoundExpression? displayClassInstance = findDisplayClassInstance(hiddenParam, eeMethod, visited.Syntax);
                if (displayClassInstance is null)
                {
                    // TODO2 can we hit this? should we produce an error?
                    // If we can't find the display class instance, bail out and
                    // return the original node — binding will report an error downstream.
                    hiddenArgs.Free();

                    newArgs = default;
                    return false;
                }

                hiddenArgs.Add(displayClassInstance);
            }

            // TODO2 review comment
            // For non-static underlying methods, the last hidden parameter
            // (the synthetic `this`) becomes the receiver, not an argument.
            if (!underlyingMethod.IsStatic)
            {
                Debug.Assert(hiddenArgs.Count >= 1);
                receiverOpt = hiddenArgs.Last();
                hiddenArgs.RemoveLast();
            }

            var newArgsBuilder = ArrayBuilder<BoundExpression>.GetInstance(visited.Arguments.Length + hiddenArgs.Count);
            newArgsBuilder.AddRange(visited.Arguments);
            newArgsBuilder.AddRange(hiddenArgs);
            hiddenArgs.Free();

            newArgs = newArgsBuilder.ToImmutableAndFree();
            return true;
        }

        static ImmutableArray<RefKind> buildRefKinds(MethodSymbol underlyingMethod, int newArgCount)
        {
            var newRefKindsBuilder = ArrayBuilder<RefKind>.GetInstance(newArgCount);
            foreach (var param in underlyingMethod.Parameters)
            {
                newRefKindsBuilder.Add(param.RefKind);
            }

            return newRefKindsBuilder.ToImmutableAndFree();
        }

        static ImmutableArray<string?> buildArgNames(ImmutableArray<string?> argNames, int newArgCount)
        {
            if (argNames.IsDefault)
            {
                return default;
            }

            var names = ArrayBuilder<string?>.GetInstance(newArgCount);
            names.AddRange(argNames);
            names.AddMany(null, newArgCount - argNames.Length);

            return names.ToImmutableAndFree();
        }

        // Finds the display class instance in the EE method's locals or parameters
        // that matches the given hidden parameter's type.
        static BoundExpression? findDisplayClassInstance(ParameterSymbol hiddenParam, EEMethodSymbol eeMethod, SyntaxNode syntax)
        {
            var targetType = hiddenParam.Type;

            foreach (var local in eeMethod.Locals)
            {
                if (TypeSymbol.Equals(local.Type, targetType, TypeCompareKind.ConsiderEverything))
                {
                    return new BoundLocal(syntax, local, constantValueOpt: null, type: local.Type) { WasCompilerGenerated = true };
                }
            }

            foreach (var param in eeMethod.Parameters)
            {
                if (TypeSymbol.Equals(param.Type, targetType, TypeCompareKind.ConsiderEverything))
                {
                    return new BoundParameter(syntax, param) { WasCompilerGenerated = true };
                }
            }

            return null;
        }
    }

    public override BoundNode? VisitDelegateCreationExpression(BoundDelegateCreationExpression node)
    {
        var visited = (BoundDelegateCreationExpression)base.VisitDelegateCreationExpression(node)!;

        if (visited.MethodOpt is not EELocalFunctionMethodSymbol localFunc)
        {
            return visited;
        }

        // Non-capturing local functions are static methods with a matching signature
        var underlyingMethod = localFunc.UnderlyingMethod;
        if (localFunc.HiddenParameters.IsEmpty && underlyingMethod.IsStatic)
        {
            return visited.Update(visited.Argument, underlyingMethod, visited.IsExtensionMethod, visited.WasTargetTyped, visited.Type);
        }

        // Capturing local functions cannot be used as delegate targets in the debugger yet.
        // They are transformed into either instance methods on a display class
        // or static methods with hidden display class parameters.
        // Neither form can be used directly as a delegate target.
        _diagnostics.Add(new CSDiagnostic(
            new CSDiagnosticInfo(ErrorCode.ERR_DelegateConversionOfLocalFunctionInDebugger, localFunc.Name),
            visited.Syntax.Location));

        return visited;
    }
}

