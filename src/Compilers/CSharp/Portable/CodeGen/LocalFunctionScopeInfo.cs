// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Pairs the original source name of a local function with the synthesized method
/// that implements it. Used in <see cref="BoundLocalFunctionsScope"/> to carry
/// local function information through lowering until IL scope ranges are resolved
/// during code generation (that produces <see cref="CodeAnalysis.CodeGen.LocalFunctionScope"/>).
/// </summary>
internal readonly struct LocalFunctionScopeInfo
{
    public readonly string Name;
    public readonly MethodSymbol Method;

    public LocalFunctionScopeInfo(string name, SynthesizedClosureMethod method)
    {
        Name = name;
        Method = method;
    }
}
