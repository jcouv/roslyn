// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.ExpressionEvaluator;

/// <summary>
/// Binder that resolves local function source names to their
/// lowered method symbols. Inserted into the EE binder chain so that
/// expressions like <c>localFunc(1)</c> can be evaluated in the debugger.
/// </summary>
internal sealed class EELocalFunctionBinder : Binder
{
    private readonly ImmutableArray<(string Name, MethodSymbol Method)> _localFunctions;

    internal EELocalFunctionBinder(ImmutableArray<(string Name, MethodSymbol Method)> localFunctions, Binder next)
        : base(next)
    {
        _localFunctions = localFunctions;
    }

    internal override void LookupSymbolsInSingleBinder(
        LookupResult result, string name, int arity, ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options, Binder originalBinder, bool diagnose,
        ref CompoundUseSiteInfo<AssemblySymbol> useSiteInfo)
    {
        if (!options.CanConsiderLocals())
        {
            return;
        }

        foreach (var (sourceName, method) in _localFunctions)
        {
            if (sourceName == name)
            {
                result.MergeEqual(originalBinder.CheckViability(method, arity, options, null, diagnose, ref useSiteInfo, basesBeingResolved));
            }
        }
    }

    internal override void AddLookupSymbolsInfoInSingleBinder(LookupSymbolsInfo info, LookupOptions options, Binder originalBinder)
    {
        if (!options.CanConsiderLocals())
        {
            return;
        }

        foreach (var (sourceName, method) in _localFunctions)
        {
            if (originalBinder.CanAddLookupSymbolInfo(method, options, info, null))
            {
                info.AddSymbol(method, sourceName, 0);
            }
        }
    }
}
