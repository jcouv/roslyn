// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal sealed class SymbolEqualityComparer : EqualityComparer<Symbol>
    {
        internal static readonly EqualityComparer<Symbol> ConsiderEverything = new(TypeCompareKind.ConsiderEverything);

        internal static readonly EqualityComparer<Symbol> IgnoringTupleNamesAndNullability = new(TypeCompareKind.IgnoreTupleNames | TypeCompareKind.IgnoreNullableModifiersForReferenceTypes);

        internal static EqualityComparer<Symbol> IncludeNullability => ConsiderEverything;

        /// <summary>
        /// A comparer that treats dynamic and object as "the same" types, and also ignores tuple element names differences.
        /// </summary>
        internal static readonly EqualityComparer<Symbol> IgnoringDynamicTupleNamesAndNullability = new(TypeCompareKind.IgnoreDynamicAndTupleNames | TypeCompareKind.IgnoreNullableModifiersForReferenceTypes);

        internal static readonly EqualityComparer<Symbol> IgnoringNullable = new(TypeCompareKind.IgnoreNullableModifiersForReferenceTypes);

        internal static readonly EqualityComparer<Symbol> ObliviousNullableModifierMatchesAny = new(TypeCompareKind.ObliviousNullableModifierMatchesAny);

        internal static readonly EqualityComparer<Symbol> AllIgnoreOptionsPlusNullableWithUnknownMatchesAny =
                                                                  new SymbolEqualityComparer(TypeCompareKind.AllIgnoreOptions & ~(TypeCompareKind.IgnoreNullableModifiersForReferenceTypes));

        internal static readonly EqualityComparer<Symbol> CLRSignature = new(TypeCompareKind.CLRSignatureCompareOptions);

        private readonly TypeCompareKind _comparison;

        private SymbolEqualityComparer(TypeCompareKind comparison)
        {
            _comparison = comparison;
        }

        public override int GetHashCode(Symbol obj)
        {
            return obj is null ? 0 : obj.GetHashCode();
        }

        public override bool Equals(Symbol x, Symbol y)
        {
            return x is null ? y is null : x.Equals(y, _comparison);
        }
    }
}
