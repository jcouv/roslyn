// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CodeAnalysis.ExpressionEvaluator;

/// <summary>
/// Describes a local function decoded from the <c>LocalFunctionScopes</c>
/// custom debug information blob in a Portable PDB.
/// </summary>
internal readonly struct LocalFunctionInfo
{
    /// <summary>
    /// The original source name of the local function (e.g., "G").
    /// </summary>
    public readonly string Name;

    /// <summary>
    /// The MethodDef row ID of the lowered method that implements this local function.
    /// </summary>
    public readonly int LoweredMethodToken;

    /// <summary>
    /// The IL offset of the start of the scope where this local function is visible.
    /// </summary>
    public readonly int StartOffset;

    /// <summary>
    /// The length in bytes of the IL scope where this local function is visible.
    /// </summary>
    public readonly int Length;

    public LocalFunctionInfo(string name, int loweredMethodToken, int startOffset, int length)
    {
        Name = name;
        LoweredMethodToken = loweredMethodToken;
        StartOffset = startOffset;
        Length = length;
    }
}
