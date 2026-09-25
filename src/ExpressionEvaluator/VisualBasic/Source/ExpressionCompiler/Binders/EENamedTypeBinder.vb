' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Runtime.InteropServices
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic.ExpressionEvaluator
    Friend NotInheritable Class EENamedTypeBinder
        Inherits Binder

        Private ReadOnly _sourceBinder As Binder

        Public Sub New(substitutedSourceType As NamedTypeSymbol, containingBinder As Binder)
            MyBase.New(containingBinder)

            _sourceBinder = New NamedTypeBinder(CompilationContext.BackstopBinder, substitutedSourceType)
        End Sub

        Public Overrides ReadOnly Property ContainingNamespaceOrType As NamespaceOrTypeSymbol
            Get
                Return _sourceBinder.ContainingNamespaceOrType
            End Get
        End Property

        Public Overrides ReadOnly Property ContainingType As NamedTypeSymbol
            Get
                Return _sourceBinder.ContainingType
            End Get
        End Property

        Public Overrides ReadOnly Property ContainingMember As Symbol
            Get
                Return _sourceBinder.ContainingMember
            End Get
        End Property

        Public Overrides ReadOnly Property AdditionalContainingMembers As ImmutableArray(Of Symbol)
            Get
                Return ImmutableArray(Of Symbol).Empty
            End Get
        End Property

        Friend Overrides Sub LookupInSingleBinder(
                lookupResult As LookupResult,
                name As String, arity As Integer,
                options As LookupOptions,
                originalBinder As Binder,
                <[In]> <Out> ByRef useSiteInfo As CompoundUseSiteInfo(Of AssemblySymbol))

            Debug.Assert(lookupResult.IsClear) ' We don't require this - it just indicates that we're not wasting effort re-mapping results from other binders.

            _sourceBinder.LookupInSingleBinder(lookupResult, name, arity, options, originalBinder, useSiteInfo)

            Dim substitutedSourceType = Me.ContainingType

            If substitutedSourceType.Arity > 0 Then
                Dim symbols = lookupResult.Symbols
                For i As Integer = 0 To symbols.Count - 1
                    Dim symbol = symbols(i)
                    If symbol.Kind = SymbolKind.TypeParameter Then
                        Debug.Assert(TypeSymbol.Equals(symbol.OriginalDefinition.ContainingType, substitutedSourceType.OriginalDefinition, TypeCompareKind.ConsiderEverything))
                        Dim ordinal = DirectCast(symbol, TypeParameterSymbol).Ordinal
                        symbols(i) = substitutedSourceType.TypeArgumentsNoUseSiteDiagnostics(ordinal)
                        Debug.Assert(symbols(i).Kind = SymbolKind.TypeParameter)
                    End If
                Next
            End If
        End Sub

        Friend Overrides Sub AddLookupSymbolsInfoInSingleBinder(
                nameSet As LookupSymbolsInfo,
                options As LookupOptions,
                originalBinder As Binder)

            Dim substitutedSourceType = Me.ContainingType

            ' LookupInSingleBinder maps source type parameters to the type arguments
            ' of the substituted containing type. Keep the same mapping for lookup
            ' completion, while preserving the source type-parameter names.
            If substitutedSourceType.Arity > 0 Then
                Dim typeParameters = substitutedSourceType.TypeParameters
                Dim typeArguments = substitutedSourceType.TypeArgumentsNoUseSiteDiagnostics

                For i As Integer = 0 To typeParameters.Length - 1
                    Dim typeParameter = typeParameters(i)
                    If originalBinder.CanAddLookupSymbolInfo(typeParameter, options, nameSet, Nothing) Then
                        nameSet.AddSymbol(typeArguments(i), typeParameter.Name, 0)
                    End If
                Next
            End If

            originalBinder.AddMemberLookupSymbolsInfo(nameSet, substitutedSourceType, options)
        End Sub

        Protected Overrides Sub CollectProbableExtensionMethodsInSingleBinder(
                name As String,
                methods As ArrayBuilder(Of MethodSymbol),
                originalBinder As Binder)

            Debug.Assert(methods.Count = 0)
            _sourceBinder.ContainingType.AppendProbableExtensionMethods(name, methods)
        End Sub
    End Class
End Namespace
