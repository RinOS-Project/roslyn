' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Composition
Imports System.Diagnostics
Imports System.Linq
Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.CodeAnalysis.ChangeNamespace
Imports Microsoft.CodeAnalysis.Host.Mef
Imports Microsoft.CodeAnalysis.LanguageService
Imports Microsoft.CodeAnalysis.Simplification
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Simplification
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.ChangeNamespace
    <ExportLanguageService(GetType(IChangeNamespaceService), LanguageNames.VisualBasic), [Shared]>
    Friend NotInheritable Class VisualBasicChangeNamespaceService
        Inherits AbstractChangeNamespaceService(Of
            CompilationUnitSyntax,
            StatementSyntax,
            NamespaceStatementSyntax,
            NameSyntax,
            SimpleNameSyntax,
            CrefReferenceSyntax)

        <ImportingConstructor>
        <Obsolete(MefConstruction.ImportingConstructorMessage, True)>
        Public Sub New()
        End Sub

        Public Overrides ReadOnly Property NameReducer As AbstractReducer = New VisualBasicNameReducer()

        Public Overrides Function TryGetReplacementReferenceSyntax(reference As SyntaxNode, newNamespaceParts As ImmutableArray(Of String), syntaxFacts As ISyntaxFactsService, ByRef old As SyntaxNode, ByRef [new] As SyntaxNode) As Boolean
            Dim nameRef = TryCast(reference, SimpleNameSyntax)
            old = nameRef
            [new] = nameRef

            If nameRef Is Nothing Or newNamespaceParts.IsDefaultOrEmpty Then
                Return False
            End If

            If syntaxFacts.IsRightOfQualifiedName(nameRef) Then
                old = nameRef.Parent
                If IsGlobalNamespace(newNamespaceParts) Then
                    [new] = SyntaxFactory.QualifiedName(SyntaxFactory.GlobalName(), nameRef.WithoutTrivia())
                Else
                    Dim qualifiedNamespaceName = CreateNamespaceAsQualifiedName(newNamespaceParts, newNamespaceParts.Length - 1)
                    [new] = SyntaxFactory.QualifiedName(qualifiedNamespaceName, nameRef.WithoutTrivia())
                End If

                [new] = [new].WithTriviaFrom(old)

            ElseIf syntaxFacts.IsNameOfSimpleMemberAccessExpression(nameRef) Then
                old = nameRef.Parent
                If IsGlobalNamespace(newNamespaceParts) Then
                    [new] = SyntaxFactory.SimpleMemberAccessExpression(SyntaxFactory.GlobalName(), nameRef.WithoutTrivia())
                Else
                    Dim memberAccessNamespaceName = CreateNamespaceAsMemberAccess(newNamespaceParts, newNamespaceParts.Length - 1)
                    [new] = SyntaxFactory.SimpleMemberAccessExpression(memberAccessNamespaceName, nameRef.WithoutTrivia())
                End If

                [new] = [new].WithTriviaFrom(old)
            End If

            Return True
        End Function

        Protected Overrides Async Function GetValidContainersFromAllLinkedDocumentsAsync(document As Document, container As SyntaxNode, cancellationToken As CancellationToken) As Task(Of ImmutableArray(Of (DocumentId, SyntaxNode)))
            If document.Project.Solution.WorkspaceKind = WorkspaceKind.MiscellaneousFiles OrElse document.IsGeneratedCode(cancellationToken) Then
                Return Nothing
            End If

            Dim containerSpan As TextSpan
            If TypeOf container Is NamespaceBlockSyntax Then
                containerSpan = container.Span
            ElseIf TypeOf container Is CompilationUnitSyntax Then
                ' An empty span represents moving all global members into a namespace.
                containerSpan = Nothing
            Else
                Throw ExceptionUtilities.Unreachable
            End If

            Dim allDocumentIds As ImmutableArray(Of DocumentId) = Nothing
            If Not IsSupportedLinkedDocument(document, allDocumentIds) Then
                Return Nothing
            End If

            Return Await TryGetApplicableContainersFromAllDocumentsAsync(
                document.Project.Solution, allDocumentIds, containerSpan, cancellationToken).ConfigureAwait(False)
        End Function

        Protected Overrides Function ChangeNamespaceDeclaration(root As CompilationUnitSyntax, declaredNamespaceParts As ImmutableArray(Of String), targetNamespaceParts As ImmutableArray(Of String)) As CompilationUnitSyntax
            Dim container = root.GetAnnotatedNodes(ContainerAnnotation).Single()

            If TypeOf container Is CompilationUnitSyntax Then
                Debug.Assert(IsGlobalNamespace(declaredNamespaceParts))

                Dim namespaceName = CreateNamespaceAsQualifiedName(targetNamespaceParts, targetNamespaceParts.Length - 1).
                    WithAdditionalAnnotations(WarningAnnotation)
                Dim namespaceBlock = SyntaxFactory.NamespaceBlock(
                    SyntaxFactory.NamespaceStatement(namespaceName), root.Members)

                Return root.WithMembers(SyntaxFactory.SingletonList(Of StatementSyntax)(namespaceBlock)).
                    WithoutAnnotations(ContainerAnnotation)
            End If

            If TypeOf container Is NamespaceBlockSyntax Then
                Dim namespaceBlock = DirectCast(container, NamespaceBlockSyntax)

                If IsGlobalNamespace(targetNamespaceParts) Then
                    Return MoveMembersFromNamespaceToGlobal(root, namespaceBlock)
                End If

                Dim namespaceName = CreateNamespaceAsQualifiedName(targetNamespaceParts, targetNamespaceParts.Length - 1).
                    WithTriviaFrom(namespaceBlock.NamespaceStatement.Name).
                    WithAdditionalAnnotations(WarningAnnotation)
                Dim changedNamespace = namespaceBlock.WithNamespaceStatement(
                    namespaceBlock.NamespaceStatement.WithName(namespaceName)).
                    WithoutAnnotations(ContainerAnnotation)

                Return root.ReplaceNode(namespaceBlock, changedNamespace)
            End If

            Throw ExceptionUtilities.Unreachable
        End Function

        Protected Overrides Function GetMemberDeclarationsInContainer(container As SyntaxNode) As SyntaxList(Of StatementSyntax)
            If TypeOf container Is CompilationUnitSyntax Then
                Return DirectCast(container, CompilationUnitSyntax).Members
            End If

            If TypeOf container Is NamespaceBlockSyntax Then
                Return DirectCast(container, NamespaceBlockSyntax).Members
            End If

            Throw ExceptionUtilities.Unreachable
        End Function

        Protected Overrides Async Function TryGetApplicableContainerFromSpanAsync(document As Document, span As TextSpan, cancellationToken As CancellationToken) As Task(Of SyntaxNode)
            Dim syntaxRoot = Await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(False)
            Dim compilationUnit = DirectCast(syntaxRoot, CompilationUnitSyntax)
            Dim container As SyntaxNode = Nothing

            If span.IsEmpty Then
                If compilationUnit.DescendantNodes().OfType(Of NamespaceBlockSyntax)().Any() Then
                    Return Nothing
                End If

                container = compilationUnit
            Else
                If Not compilationUnit.Span.Contains(span) Then
                    Return Nothing
                End If

                Dim node = compilationUnit.FindNode(span, getInnermostNodeForTie:=True)
                Dim namespaceBlocks = node.AncestorsAndSelf().OfType(Of NamespaceBlockSyntax)().ToImmutableArray()
                If namespaceBlocks.Length <> 1 Then
                    Return Nothing
                End If

                Dim namespaceBlock = namespaceBlocks(0)
                If namespaceBlock.NamespaceStatement.Name.GetDiagnostics().Any(
                        Function(diagnostic) diagnostic.DefaultSeverity = DiagnosticSeverity.Error) Then
                    Return Nothing
                End If

                If namespaceBlock.DescendantNodes().OfType(Of NamespaceBlockSyntax)().Any() Then
                    Return Nothing
                End If

                container = namespaceBlock
            End If

            If Await ContainsPartialTypeWithMultipleDeclarationsAsync(document, container, cancellationToken).ConfigureAwait(False) Then
                Return Nothing
            End If

            Return container
        End Function

        Protected Overrides Function GetDeclaredNamespace(container As SyntaxNode) As String
            If TypeOf container Is CompilationUnitSyntax Then
                Return String.Empty
            End If

            If TypeOf container Is NamespaceBlockSyntax Then
                Return DirectCast(container, NamespaceBlockSyntax).NamespaceStatement.Name.ToString()
            End If

            Throw ExceptionUtilities.Unreachable
        End Function

        Private Shared Function MoveMembersFromNamespaceToGlobal(root As CompilationUnitSyntax, namespaceBlock As NamespaceBlockSyntax) As CompilationUnitSyntax
            Dim namespaceImports = namespaceBlock.Members.OfType(Of ImportsStatementSyntax)()
            Dim members = SyntaxFactory.List(namespaceBlock.Members.Where(Function(member) Not TypeOf member Is ImportsStatementSyntax))
            Dim rootImports = SyntaxFactory.List(root.Imports.Concat(namespaceImports))

            Return root.WithImports(rootImports).
                WithMembers(root.Members.ReplaceRange(namespaceBlock, members)).
                WithoutAnnotations(ContainerAnnotation)
        End Function

        Private Shared Function CreateNamespaceAsQualifiedName(namespaceParts As ImmutableArray(Of String), index As Integer) As NameSyntax
            Dim part = namespaceParts(index).EscapeIdentifier()
            Dim namePiece = SyntaxFactory.IdentifierName(part)

            If index = 0 Then
                Return namePiece
            Else
                Return SyntaxFactory.QualifiedName(CreateNamespaceAsQualifiedName(namespaceParts, index - 1), namePiece)
            End If
        End Function

        Private Shared Function CreateNamespaceAsMemberAccess(namespaceParts As ImmutableArray(Of String), index As Integer) As ExpressionSyntax
            Dim part = namespaceParts(index).EscapeIdentifier()
            Dim namePiece = SyntaxFactory.IdentifierName(part)

            If index = 0 Then
                Return namePiece
            Else
                Return SyntaxFactory.SimpleMemberAccessExpression(CreateNamespaceAsMemberAccess(namespaceParts, index - 1), namePiece)
            End If
        End Function
    End Class
End Namespace
