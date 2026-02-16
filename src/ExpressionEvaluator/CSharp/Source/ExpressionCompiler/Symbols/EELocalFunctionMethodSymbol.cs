// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Emit;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Symbols.Metadata.PE;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Microsoft.CodeAnalysis.CSharp.ExpressionEvaluator;

/// <summary>
/// Wraps a <see cref="PEMethodSymbol"/> representing a lowered local function
/// and exposes the original source name and signature.
/// </summary>
internal sealed class EELocalFunctionMethodSymbol : WrappedMethodSymbol
{
    private readonly PEMethodSymbol _underlyingMethod;
    private readonly string _sourceName;
    private readonly ImmutableArray<TypeParameterSymbol> _typeParameters;
    private readonly TypeMap? _typeMap;
    private readonly ImmutableArray<ParameterSymbol> _visibleParameters;

    /// <summary>
    /// The parameters that were filtered out because they are display class
    /// instances injected by the compiler during closure conversion.
    /// The rewriter uses these to supply hidden arguments when emitting the call.
    /// When the underlying method is a non-static method, a synthetic parameter
    /// representing the implicit <c>this</c> is included as hidden parameter so that the
    /// rewriter can supply it as the receiver.
    /// </summary>
    internal readonly ImmutableArray<ParameterSymbol> HiddenParameters;

    public EELocalFunctionMethodSymbol(PEMethodSymbol underlyingMethod, string sourceName)
    {
        Debug.Assert(underlyingMethod != null);
        Debug.Assert(sourceName != null);
        _underlyingMethod = underlyingMethod!;
        _sourceName = sourceName!;

        var sourceTypeParameters = underlyingMethod!.TypeParameters;
        if (sourceTypeParameters.IsEmpty)
        {
            _typeParameters = [];
            _typeMap = null;
        }
        else
        {
            _typeParameters = sourceTypeParameters.SelectAsArray(
                (tp, i, arg) => (TypeParameterSymbol)new EETypeParameterSymbol(this, tp, i, arg),
                arg: () => _typeMap);

            _typeMap = new TypeMap(sourceTypeParameters, _typeParameters, allowAlpha: true);
        }

        EENamedTypeSymbol.VerifyTypeParameters(this, _typeParameters);

        (_visibleParameters, HiddenParameters) = makeParameters(underlyingMethod);

        return;

        // Partition parameters into visible (user-facing) and hidden (display class).
        (ImmutableArray<ParameterSymbol> visibleParameters, ImmutableArray<ParameterSymbol> hiddenParameters)
            makeParameters(MethodSymbol underlyingMethod)
        {
            var parameters = underlyingMethod.Parameters;
            int visibleCount = 0;
            while (visibleCount < parameters.Length && !parameters[visibleCount].Type.IsDisplayClassType())
            {
                visibleCount++;
            }

            // Assert no visible parameter follows a hidden one.
            for (int i = visibleCount; i < parameters.Length; i++)
            {
                Debug.Assert(parameters[i].Type.IsDisplayClassType(),
                    "Closure conversion should place all display class parameters after visible parameters");
            }

            var visible = ArrayBuilder<ParameterSymbol>.GetInstance(visibleCount);
            for (int i = 0; i < visibleCount; i++)
            {
                visible.Add(makeParameterSymbol(i, parameters[i].Name, parameters[i]));
            }

            var hidden = ArrayBuilder<ParameterSymbol>.GetInstance(parameters.Length - visibleCount);
            for (int i = visibleCount; i < parameters.Length; i++)
            {
                hidden.Add(parameters[i]);
            }

            // TODO2 review this
            // If the underlying method is an instance method (whether on a display class
            // or the containing user type for this-capturing), treat the implicit `this`
            // as a hidden parameter so the rewriter can supply it as the receiver.
            if (!underlyingMethod.IsStatic)
            {
                var containingType = underlyingMethod.ContainingType;
                var thisParam = SynthesizedParameterSymbol.Create(
                    this,
                    TypeWithAnnotations.Create(containingType),
                    ordinal: hidden.Count,
                    RefKind.None,
                    name: ThisParameterSymbolBase.SymbolName);
                hidden.Add(thisParam);
            }

            var visibleParameters = visible.ToImmutableAndFree();
            var hiddenParameters = hidden.ToImmutableAndFree();

            return (visibleParameters, hiddenParameters);
        }

        ParameterSymbol makeParameterSymbol(int ordinal, string name, ParameterSymbol sourceParameter)
        {
            var type = _typeMap != null ? _typeMap.SubstituteType(sourceParameter.TypeWithAnnotations) : sourceParameter.TypeWithAnnotations;
            return SynthesizedParameterSymbol.Create(
                this,
                type,
                ordinal,
                sourceParameter.RefKind,
                name,
                sourceParameter.EffectiveScope,
                defaultValue: sourceParameter.ExplicitDefaultConstantValue,
                refCustomModifiers: sourceParameter.RefCustomModifiers,
                isParams: sourceParameter.IsParams);
        }
    }

    public override MethodSymbol UnderlyingMethod => _underlyingMethod;

    public override string Name => _sourceName;

    public override MethodKind MethodKind => MethodKind.LocalFunction;

    // Local functions never require an instance receiver from the user's perspective
    // (matching source LocalFunctionSymbol). The rewriter handles supplying the
    // receiver for non-static underlying methods.
    public override bool RequiresInstanceReceiver => false;

    public override Symbol ContainingSymbol => _underlyingMethod.ContainingSymbol;

    public override ImmutableArray<TypeParameterSymbol> TypeParameters => _typeParameters;

    public override ImmutableArray<ParameterSymbol> Parameters => _visibleParameters;

    internal override int ParameterCount => _visibleParameters.Length;

    public override TypeWithAnnotations ReturnTypeWithAnnotations
        => _typeMap != null ? _typeMap.SubstituteType(_underlyingMethod.ReturnTypeWithAnnotations) : _underlyingMethod.ReturnTypeWithAnnotations;

    public override ImmutableArray<CustomModifier> RefCustomModifiers => _underlyingMethod.RefCustomModifiers;

    public override Symbol? AssociatedSymbol => null;

    public override ImmutableArray<MethodSymbol> ExplicitInterfaceImplementations => [];

    public override ImmutableArray<TypeWithAnnotations> TypeArgumentsWithAnnotations
        => _typeParameters.IsEmpty
            ? []
            : _typeParameters.SelectAsArray(static tp => TypeWithAnnotations.Create(tp));

    internal override int CalculateLocalSyntaxOffset(int localPosition, SyntaxTree localTree) => -1;

    internal override bool HasSpecialNameAttribute => _underlyingMethod.HasSpecialNameAttribute;

    internal override bool IsNullableAnalysisEnabled() => false;

    internal override int TryGetOverloadResolutionPriority() => 0;

    internal override UnmanagedCallersOnlyAttributeData? GetUnmanagedCallersOnlyAttributeData(bool forceComplete)
        => null;

    internal override bool HasAsyncMethodBuilderAttribute(out TypeSymbol? builderArgument)
    {
        builderArgument = null;
        return false;
    }

    internal override void AddSynthesizedAttributes(PEModuleBuilder moduleBuilder, ref ArrayBuilder<CSharpAttributeData> attributes)
    {
    }
}
