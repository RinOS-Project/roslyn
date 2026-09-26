' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Threading
Imports Microsoft.CodeAnalysis.Rename.ConflictEngine
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Rename
    Friend Class LocalConflictVisitor
        Inherits VisualBasicSyntaxVisitor

        Private ReadOnly _tracker As ConflictingIdentifierTracker
        Private ReadOnly _semanticModel As SemanticModel
        Private ReadOnly _cancellationToken As CancellationToken

        Public Sub New(tokenBeingRenamed As SyntaxToken, semanticModel As SemanticModel, cancellationToken As CancellationToken)
            _tracker = New ConflictingIdentifierTracker(tokenBeingRenamed, CaseInsensitiveComparison.Comparer)
            _semanticModel = semanticModel
            _cancellationToken = cancellationToken
        End Sub

        Public Overrides Sub DefaultVisit(node As SyntaxNode)
            For Each child In node.ChildNodes()
                Visit(child)
            Next
        End Sub

        Private Sub VisitMethodBlockBase(node As MethodBlockBaseSyntax)
            Dim tokens As New List(Of SyntaxToken)

            If node.BlockStatement.ParameterList IsNot Nothing Then
                tokens.AddRange(From parameter In node.BlockStatement.ParameterList.Parameters
                                Select parameter.Identifier.Identifier)
            End If

            _tracker.AddIdentifiers(tokens)
            VisitBlock(node.Statements)
            _tracker.RemoveIdentifiers(tokens)
        End Sub

        Public Overrides Sub VisitMethodBlock(node As MethodBlockSyntax)
            VisitMethodBlockBase(node)
        End Sub

        Public Overrides Sub VisitConstructorBlock(node As ConstructorBlockSyntax)
            VisitMethodBlockBase(node)
        End Sub

        Public Overrides Sub VisitOperatorBlock(node As OperatorBlockSyntax)
            VisitMethodBlockBase(node)
        End Sub

        Public Overrides Sub VisitAccessorBlock(node As AccessorBlockSyntax)
            VisitMethodBlockBase(node)
        End Sub

        Public Overrides Sub VisitQueryExpression(node As QueryExpressionSyntax)
            Dim tokens As New List(Of SyntaxToken)

            For Each clause In node.Clauses
                AddQueryClauseIdentifiers(clause, tokens)
            Next

            _tracker.AddIdentifiers(tokens)
            For Each clause In node.Clauses
                Visit(clause)
            Next

            _tracker.RemoveIdentifiers(tokens)
        End Sub

        Private Shared Sub AddQueryClauseIdentifiers(clause As QueryClauseSyntax, tokens As List(Of SyntaxToken))
            Select Case clause.Kind
                Case SyntaxKind.FromClause
                    Dim fromClause = DirectCast(clause, FromClauseSyntax)
                    For Each variable In fromClause.Variables
                        tokens.Add(variable.Identifier.Identifier)
                    Next

                Case SyntaxKind.LetClause
                    Dim letClause = DirectCast(clause, LetClauseSyntax)
                    For Each variable In letClause.Variables
                        AddExpressionRangeVariableIdentifier(variable, tokens)
                    Next

                Case SyntaxKind.AggregateClause
                    Dim aggregateClause = DirectCast(clause, AggregateClauseSyntax)
                    For Each variable In aggregateClause.Variables
                        tokens.Add(variable.Identifier.Identifier)
                    Next

                    For Each variable In aggregateClause.AggregationVariables
                        AddAggregationRangeVariableIdentifier(variable, tokens)
                    Next

                    For Each additionalClause In aggregateClause.AdditionalQueryOperators
                        AddQueryClauseIdentifiers(additionalClause, tokens)
                    Next

                Case SyntaxKind.SimpleJoinClause, SyntaxKind.GroupJoinClause
                    Dim joinClause = DirectCast(clause, JoinClauseSyntax)
                    For Each variable In joinClause.JoinedVariables
                        tokens.Add(variable.Identifier.Identifier)
                    Next

                    If clause.Kind = SyntaxKind.GroupJoinClause Then
                        For Each variable In DirectCast(clause, GroupJoinClauseSyntax).AggregationVariables
                            AddAggregationRangeVariableIdentifier(variable, tokens)
                        Next
                    End If

                    For Each additionalJoin In joinClause.AdditionalJoins
                        AddQueryClauseIdentifiers(additionalJoin, tokens)
                    Next

                Case SyntaxKind.GroupByClause
                    Dim groupByClause = DirectCast(clause, GroupByClauseSyntax)
                    For Each variable In groupByClause.Items
                        AddExpressionRangeVariableIdentifier(variable, tokens)
                    Next

                    For Each variable In groupByClause.Keys
                        AddExpressionRangeVariableIdentifier(variable, tokens)
                    Next

                    For Each variable In groupByClause.AggregationVariables
                        AddAggregationRangeVariableIdentifier(variable, tokens)
                    Next

                Case SyntaxKind.SelectClause
                    Dim selectClause = DirectCast(clause, SelectClauseSyntax)
                    For Each variable In selectClause.Variables
                        AddExpressionRangeVariableIdentifier(variable, tokens)
                    Next
            End Select
        End Sub

        Private Shared Sub AddExpressionRangeVariableIdentifier(variable As ExpressionRangeVariableSyntax, tokens As List(Of SyntaxToken))
            If variable.NameEquals IsNot Nothing Then
                tokens.Add(variable.NameEquals.Identifier.Identifier)
            End If
        End Sub

        Private Shared Sub AddAggregationRangeVariableIdentifier(variable As AggregationRangeVariableSyntax, tokens As List(Of SyntaxToken))
            If variable.NameEquals IsNot Nothing Then
                tokens.Add(variable.NameEquals.Identifier.Identifier)
            End If
        End Sub

        Private Sub VisitBlock(block As SyntaxList(Of StatementSyntax))
            Dim tokens As New List(Of SyntaxToken)

            For Each statement In block
                If statement.Kind = SyntaxKind.LocalDeclarationStatement Then
                    Dim declarationStatement = DirectCast(statement, LocalDeclarationStatementSyntax)

                    For Each declarator In declarationStatement.Declarators
                        tokens.AddRange(From i In declarator.Names
                                        Select i.Identifier)
                    Next
                End If
            Next

            _tracker.AddIdentifiers(tokens)
            For Each statement In block
                Visit(statement)
            Next

            _tracker.RemoveIdentifiers(tokens)
        End Sub

        Public Overrides Sub VisitSingleLineLambdaExpression(node As SingleLineLambdaExpressionSyntax)
            Dim tokens As New List(Of SyntaxToken)

            If node.SubOrFunctionHeader.ParameterList IsNot Nothing Then
                tokens.AddRange(From parameter In node.SubOrFunctionHeader.ParameterList.Parameters
                                Select parameter.Identifier.Identifier)
            End If

            _tracker.AddIdentifiers(tokens)
            Visit(node.Body)
            _tracker.RemoveIdentifiers(tokens)
        End Sub

        Public Overrides Sub VisitMultiLineLambdaExpression(node As MultiLineLambdaExpressionSyntax)
            Dim tokens As New List(Of SyntaxToken)

            If node.SubOrFunctionHeader.ParameterList IsNot Nothing Then
                tokens.AddRange(From parameter In node.SubOrFunctionHeader.ParameterList.Parameters
                                Select parameter.Identifier.Identifier)
            End If

            _tracker.AddIdentifiers(tokens)
            VisitBlock(node.Statements)
            _tracker.RemoveIdentifiers(tokens)
        End Sub

        Public Overrides Sub VisitForBlock(node As ForBlockSyntax)
            VisitForOrForEachBlock(node)
        End Sub

        Public Overrides Sub VisitForEachBlock(node As ForEachBlockSyntax)
            VisitForOrForEachBlock(node)
        End Sub

        Private Sub VisitForOrForEachBlock(node As ForOrForEachBlockSyntax)
            Dim tokens As New List(Of SyntaxToken)

            Dim controlVariable As SyntaxNode
            If node.ForOrForEachStatement.Kind = SyntaxKind.ForEachStatement Then
                controlVariable = DirectCast(node.ForOrForEachStatement, ForEachStatementSyntax).ControlVariable
            Else
                controlVariable = DirectCast(node.ForOrForEachStatement, ForStatementSyntax).ControlVariable
            End If

            If controlVariable.Kind = SyntaxKind.VariableDeclarator Then
                ' it's only legal to have one name in the variable declarator for for and foreach loops.
                tokens.Add(DirectCast(controlVariable, VariableDeclaratorSyntax).Names.First().Identifier)
            Else
                Dim symbol = _semanticModel.GetSymbolInfo(controlVariable, _cancellationToken).Symbol

                ' if it is a field we don't care
                If symbol IsNot Nothing AndAlso symbol.IsKind(SymbolKind.Local) Then
                    Dim local = DirectCast(symbol, ILocalSymbol)

                    ' is this local declared in the for or for each loop?
                    ' if not it was already added to the tracker before.
                    If local.IsFor OrElse local.IsForEach Then
                        If controlVariable.Kind = SyntaxKind.IdentifierName Then
                            tokens.Add(DirectCast(controlVariable, IdentifierNameSyntax).Identifier)
                        Else
                            Debug.Fail($"Unexpected control variable kind '{controlVariable.Kind}'")
                        End If
                    End If
                End If
            End If

            _tracker.AddIdentifiers(tokens)
            VisitBlock(node.Statements)
            _tracker.RemoveIdentifiers(tokens)
        End Sub

        Public Overrides Sub VisitUsingBlock(node As UsingBlockSyntax)
            Dim tokens As New List(Of SyntaxToken)

            ' add all declared variable names to token list
            For Each usingVariableDeclarator In node.UsingStatement.Variables
                For Each name In usingVariableDeclarator.Names
                    tokens.Add(name.Identifier)
                Next
            Next

            _tracker.AddIdentifiers(tokens)
            VisitBlock(node.Statements)
            _tracker.RemoveIdentifiers(tokens)
        End Sub

        Public Overrides Sub VisitCatchBlock(node As CatchBlockSyntax)
            Dim tokens As New List(Of SyntaxToken)

            Dim identifierToken = node.CatchStatement.IdentifierName?.Identifier

            If identifierToken.HasValue Then
                Dim symbol = _semanticModel.GetSymbolInfo(identifierToken.Value, _cancellationToken).Symbol

                ' if it is a field we don't care
                If symbol IsNot Nothing AndAlso symbol.IsKind(SymbolKind.Local) Then
                    Dim local = DirectCast(symbol, ILocalSymbol)

                    ' is this local declared in the for or for each loop?
                    ' if not it was already added to the tracker before.
                    If local.IsCatch Then
                        tokens.Add(identifierToken.Value)
                    End If
                End If
            End If

            _tracker.AddIdentifiers(tokens)
            VisitBlock(node.Statements)
            _tracker.RemoveIdentifiers(tokens)
        End Sub

        Public ReadOnly Property ConflictingTokens As IEnumerable(Of SyntaxToken)
            Get
                Return _tracker.ConflictingTokens
            End Get
        End Property
    End Class
End Namespace
