// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
#pragma warning disable RS1024 // Use 'SymbolEqualityComparer' when comparing symbols (https://github.com/dotnet/roslyn/issues/78583)

#nullable disable

using System;
using System.Collections.Immutable;
using System.Linq;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Shared.Extensions;

internal static partial class ITypeSymbolExtensions
{
    private sealed class UnnamedErrorTypeRemover(Compilation compilation) : SymbolVisitor<ITypeSymbol>
    {
        public override ITypeSymbol DefaultVisit(ISymbol node)
            => throw ExceptionUtilities.UnexpectedValue(node.Kind);

        public override ITypeSymbol VisitDynamicType(IDynamicTypeSymbol symbol)
            => symbol;

        public override ITypeSymbol VisitArrayType(IArrayTypeSymbol symbol)
        {
            var elementType = symbol.ElementType.Accept(this);
            if (elementType != null && elementType.Equals(symbol.ElementType))
            {
                return symbol;
            }

            return compilation.CreateArrayTypeSymbol(elementType, symbol.Rank);
        }

        public override ITypeSymbol VisitFunctionPointerType(IFunctionPointerTypeSymbol symbol)
        {
            var returnType = symbol.Signature.ReturnType.Accept(this);
            var parameterTypes = symbol.Signature.Parameters
                .Select(parameter => parameter.Type.Accept(this))
                .ToImmutableArray();

            if (returnType.Equals(symbol.Signature.ReturnType) &&
                parameterTypes.SequenceEqual(symbol.Signature.Parameters.Select(parameter => parameter.Type)))
            {
                return symbol;
            }

            return compilation.CreateFunctionPointerTypeSymbol(
                returnType,
                symbol.Signature.RefKind,
                parameterTypes,
                symbol.Signature.Parameters.Select(parameter => parameter.RefKind).ToImmutableArray(),
                symbol.Signature.CallingConvention,
                symbol.Signature.UnmanagedCallingConventionTypes);
        }

        public override ITypeSymbol VisitNamedType(INamedTypeSymbol symbol)
        {
            if (symbol.IsErrorType() && symbol.Name == string.Empty)
            {
                return compilation.ObjectType;
            }

            var arguments = symbol.TypeArguments.Select(t => t.Accept(this)).ToArray();
            if (arguments.SequenceEqual(symbol.TypeArguments))
            {
                return symbol;
            }

            return symbol.ConstructedFrom.Construct([.. arguments]);
        }

        public override ITypeSymbol VisitPointerType(IPointerTypeSymbol symbol)
        {
            var elementType = symbol.PointedAtType.Accept(this);
            if (elementType != null && elementType.Equals(symbol.PointedAtType))
            {
                return symbol;
            }

            return compilation.CreatePointerTypeSymbol(elementType);
        }

        public override ITypeSymbol VisitTypeParameter(ITypeParameterSymbol symbol)
            => symbol;
    }
}
