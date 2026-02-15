// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CodeGen;

/// <summary>
/// Debug information maintained for each local function in a method.
/// Emitted to Portable PDB as a <c>LocalFunctionMap</c> custom debug information record
/// attached to the containing method's <c>MethodDefinitionHandle</c>.
/// </summary>
[DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
internal readonly struct LocalFunctionScope
{
    /// <summary>
    /// The original source name of the local function (e.g., "G").
    /// </summary>
    public readonly string Name;

    /// <summary>
    /// The lowered (synthesized) method that implements this local function.
    /// </summary>
    public readonly Cci.IMethodDefinition LoweredMethod;

    /// <summary>
    /// The IL offset of the start of the scope where this local function is visible.
    /// </summary>
    public readonly int StartOffset;

    /// <summary>
    /// The length in bytes of the IL scope where this local function is visible.
    /// </summary>
    public readonly int Length;

    public LocalFunctionScope(string name, Cci.IMethodDefinition loweredMethod, int startOffset, int length)
    {
        Debug.Assert(startOffset >= 0);
        Debug.Assert(length >= 0);
        Name = name;
        LoweredMethod = loweredMethod;
        StartOffset = startOffset;
        Length = length;
    }

    private string GetDebuggerDisplay()
        => $"{Name} -> {LoweredMethod}";
}
