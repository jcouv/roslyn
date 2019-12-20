// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    /// <summary>
    /// Information decoded from well-known custom attributes applied on a parameter.
    /// </summary>
    internal sealed class ParameterWellKnownAttributeData : CommonParameterWellKnownAttributeData
    {
        private bool _hasEnumeratorCancellationAttribute;
        public bool HasEnumeratorCancellationAttribute
        {
            get
            {
                VerifySealed(expected: true);
                return _hasEnumeratorCancellationAttribute;
            }
            set
            {
                VerifySealed(expected: false);
                _hasEnumeratorCancellationAttribute = value;
                SetDataStored();
            }
        }
    }
}
