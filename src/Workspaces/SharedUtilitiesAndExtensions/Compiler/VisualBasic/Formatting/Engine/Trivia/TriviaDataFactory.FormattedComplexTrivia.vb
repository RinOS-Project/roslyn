' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Threading
Imports Microsoft.CodeAnalysis.Formatting
Imports Microsoft.CodeAnalysis.Text

Namespace Microsoft.CodeAnalysis.VisualBasic.Formatting
    Partial Friend Class TriviaDataFactory
        Private NotInheritable Class FormattedComplexTrivia
            Inherits TriviaDataWithList

            Private ReadOnly _formatter As VisualBasicTriviaFormatter
            Private ReadOnly _textChanges As IList(Of TextChange)
            Private ReadOnly _token1 As SyntaxToken
            Private ReadOnly _token2 As SyntaxToken
            Private ReadOnly _originalString As String

            Public Sub New(context As FormattingContext,
                           formattingRules As ChainedFormattingRules,
                           token1 As SyntaxToken,
                           token2 As SyntaxToken,
                           lineBreaks As Integer,
                           spaces As Integer,
                           originalString As String,
                           cancellationToken As CancellationToken)
                MyBase.New(context.Options.LineFormatting)

                Contract.ThrowIfNull(context)
                Contract.ThrowIfNull(formattingRules)
                Contract.ThrowIfNull(originalString)

                _token1 = token1
                _token2 = token2
                _originalString = originalString
                Me.LineBreaks = Math.Max(0, lineBreaks)
                Me.Spaces = Math.Max(0, spaces)

                Dim lines = Me.LineBreaks

                _formatter = New VisualBasicTriviaFormatter(context, formattingRules, token1, token2, originalString, Math.Max(0, lines), Me.Spaces)
                _textChanges = _formatter.FormatToTextChanges(cancellationToken)
            End Sub

            Public Overrides ReadOnly Property TreatAsElastic() As Boolean
                Get
                    Return False
                End Get
            End Property

            Public Overrides ReadOnly Property IsWhitespaceOnlyTrivia() As Boolean
                Get
                    Return False
                End Get
            End Property

            Public Overrides ReadOnly Property ContainsChanges() As Boolean
                Get
                    Return Me._textChanges.Count > 0
                End Get
            End Property

            Public Overrides Function GetTextChanges(span As TextSpan) As IEnumerable(Of TextChange)
                Return Me._textChanges
            End Function

            Public Overrides Function GetTriviaList(cancellationToken As CancellationToken) As SyntaxTriviaList
                Return _formatter.FormatToSyntaxTrivia(cancellationToken)
            End Function

            Public Overrides Sub Format(context As FormattingContext,
                                        formattingRules As ChainedFormattingRules,
                                        formattingResultApplier As Action(Of Integer, TokenStream, TriviaData),
                                        cancellationToken As CancellationToken,
                                        Optional tokenPairIndex As Integer = TokenPairIndexNotNeeded)
                formattingResultApplier(tokenPairIndex, context.TokenStream, Me)
            End Sub

            Public Overrides Function WithIndentation(indentation As Integer, context As FormattingContext, formattingRules As ChainedFormattingRules, cancellationToken As CancellationToken) As TriviaData
                Return Reformat(context, formattingRules, Me.LineBreaks, indentation, cancellationToken)
            End Function

            Public Overrides Function WithLine(line As Integer, indentation As Integer, context As FormattingContext, formattingRules As ChainedFormattingRules, cancellationToken As CancellationToken) As TriviaData
                Return Reformat(context, formattingRules, line, indentation, cancellationToken)
            End Function

            Public Overrides Function WithSpace(space As Integer, context As FormattingContext, formattingRules As ChainedFormattingRules) As TriviaData
                Return Reformat(context, formattingRules, Me.LineBreaks, space, CancellationToken.None)
            End Function

            Private Function Reformat(context As FormattingContext,
                                      formattingRules As ChainedFormattingRules,
                                      lineBreaks As Integer,
                                      spaces As Integer,
                                      cancellationToken As CancellationToken) As FormattedComplexTrivia
                Return New FormattedComplexTrivia(context, formattingRules, _token1, _token2, lineBreaks, spaces, _originalString, cancellationToken)
            End Function
        End Class
    End Class
End Namespace
