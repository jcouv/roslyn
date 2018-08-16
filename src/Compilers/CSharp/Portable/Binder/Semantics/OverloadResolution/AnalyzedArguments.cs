// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal interface IAnalyzedArguments
    {
        BoundExpression Argument(int i);
        int ArgumentsCount { get; }
        ImmutableArray<BoundExpression> ArgumentsImmutable { get; }

        IdentifierNameSyntax Name(int i);
        int NamesCount { get; }
        string NameText(int i);

        RefKind RefKind(int i);
        int RefKindsCount { get; }
        ImmutableArray<RefKind> RefKindsImmutable { get; }

        bool IsExtensionMethodInvocation { get; }
        bool HasErrors { get; }
        ImmutableArray<string> GetNames();
        bool IsExtensionMethodThisArgument(int i);
        bool HasDynamicArgument { get; }
    }

    internal abstract class AnalyzedArgumentsBase
    {
        public abstract BoundExpression Argument(int i);
        public abstract int ArgumentsCount { get; }

        public abstract IdentifierNameSyntax Name(int i);
        public abstract int NamesCount { get; }

        public abstract RefKind RefKind(int i);
        public abstract int RefKindsCount { get; }

        public bool IsExtensionMethodInvocation { get; set; }
        protected ThreeState _lazyHasDynamicArgument;

        public string NameText(int i)
        {
            if (NamesCount == 0)
            {
                return null;
            }

            IdentifierNameSyntax syntax = Name(i);
            return syntax == null ? null : syntax.Identifier.ValueText;
        }

        public ImmutableArray<string> GetNames()
        {
            int count = this.NamesCount;

            if (count == 0)
            {
                return default;
            }

            var builder = ArrayBuilder<string>.GetInstance(count);
            for (int i = 0; i < count; ++i)
            {
                builder.Add(NameText(i));
            }

            return builder.ToImmutableAndFree();
        }

        public bool HasDynamicArgument
        {
            get
            {
                if (_lazyHasDynamicArgument.HasValue())
                {
                    return _lazyHasDynamicArgument.Value();
                }

                for (int i = 0; i < ArgumentsCount; i++)
                {
                    var argument = Argument(i);

                    // By-ref dynamic arguments don't make the invocation dynamic.
                    if ((object)argument.Type != null && argument.Type.IsDynamic() &&  RefKind(i) == CodeAnalysis.RefKind.None)
                    {
                        _lazyHasDynamicArgument = ThreeState.True;
                        return true;
                    }
                }

                _lazyHasDynamicArgument = ThreeState.False;
                return false;
            }
        }

        public bool IsExtensionMethodThisArgument(int i)
        {
            return (i == 0) && this.IsExtensionMethodInvocation;
        }

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < ArgumentsCount; i++)
                {
                    if (Argument(i).HasAnyErrors)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }

    // Note: instances of this object are pooled
    internal sealed class AnalyzedArguments : AnalyzedArgumentsBase, IAnalyzedArguments
    {
        internal readonly ArrayBuilder<BoundExpression> Arguments;
        internal readonly ArrayBuilder<IdentifierNameSyntax> Names;
        internal readonly ArrayBuilder<RefKind> _refKinds;

        internal AnalyzedArguments()
        {
            Arguments = new ArrayBuilder<BoundExpression>(32);
            Names = new ArrayBuilder<IdentifierNameSyntax>(32);
            _refKinds = new ArrayBuilder<RefKind>(32);
        }

        public override BoundExpression Argument(int i) => Arguments[i];
        public override int ArgumentsCount => Arguments.Count;
        public ImmutableArray<BoundExpression> ArgumentsImmutable => Arguments.ToImmutable();

        public override IdentifierNameSyntax Name(int i) => Names[i];
        public override int NamesCount => Names.Count;

        public override RefKind RefKind(int i)
        {
            return _refKinds.Count > 0 ? _refKinds[i] : CodeAnalysis.RefKind.None;
        }

        public override int RefKindsCount => _refKinds.Count;
        public ImmutableArray<RefKind> RefKindsImmutable => _refKinds.ToImmutable();

        public void Clear()
        {
            this.Arguments.Clear();
            this.Names.Clear();
            this._refKinds.Clear();
            this.IsExtensionMethodInvocation = false;
            _lazyHasDynamicArgument = ThreeState.Unknown;
        }

        #region "Poolable"

        public static AnalyzedArguments GetInstance()
        {
            return Pool.Allocate();
        }

        public void Free()
        {
            this.Clear();
            Pool.Free(this);
        }

        //2) Expose the pool or the way to create a pool or the way to get an instance.
        //       for now we will expose both and figure which way works better
        public static readonly ObjectPool<AnalyzedArguments> Pool = CreatePool();

        private static ObjectPool<AnalyzedArguments> CreatePool()
        {
            ObjectPool<AnalyzedArguments> pool = null;
            pool = new ObjectPool<AnalyzedArguments>(() => new AnalyzedArguments(), 10);
            return pool;
        }

        #endregion
    }

    /// <summary>
    /// Instances of this object are either held in <see cref="AnalyzedArguments"/> (which uses pooled ArrayBuilder instances),
    /// or <see cref="MethodGroupResolution"/> (which uses .
    /// </summary>
    internal class UnpooledAnalyzedArguments : AnalyzedArgumentsBase, IAnalyzedArguments
    {
        public ImmutableArray<BoundExpression> ArgumentsImmutable { get; }
        private ImmutableArray<IdentifierNameSyntax> Names;
        public ImmutableArray<RefKind> RefKindsImmutable { get; }

        internal UnpooledAnalyzedArguments(AnalyzedArguments analyzedArguments)
        {
            ArgumentsImmutable = analyzedArguments.Arguments.ToImmutable();
            Names = analyzedArguments.Names.ToImmutable();
            RefKindsImmutable = analyzedArguments._refKinds.ToImmutable();
        }

        public override BoundExpression Argument(int i) => ArgumentsImmutable[i];
        public override int ArgumentsCount => ArgumentsImmutable.Length;

        public override IdentifierNameSyntax Name(int i) => Names[i];
        public override int NamesCount => Names.Length;

        public override RefKind RefKind(int i)
        {
            return RefKindsImmutable.Length > 0 ? RefKindsImmutable[i] : Microsoft.CodeAnalysis.RefKind.None;
        }

        public override int RefKindsCount => RefKindsImmutable.Length;
    }
}
