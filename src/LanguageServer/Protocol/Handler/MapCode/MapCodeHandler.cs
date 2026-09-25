// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.MapCode;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Roslyn.LanguageServer.Protocol;
using Roslyn.Utilities;
using LSP = Roslyn.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.LanguageServer.Handler;

[ExportCSharpVisualBasicStatelessLspService(typeof(MapCodeHandler)), Shared]
[Method(VSInternalMethods.WorkspaceMapCodeName)]
internal sealed class MapCodeHandler : ILspServiceRequestHandler<VSInternalMapCodeParams, LSP.WorkspaceEdit?>
{
    [ImportingConstructor]
    [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
    public MapCodeHandler()
    {
    }

    public bool MutatesSolutionState => false;
    public bool RequiresLSPSolution => true;

    public async Task<WorkspaceEdit?> HandleRequestAsync(VSInternalMapCodeParams request, RequestContext context, CancellationToken cancellationToken)
    {
        Contract.ThrowIfNull(context.Solution);

        var solution = request.Updates is { } updates
            ? await ApplyWorkspaceTextEditsAsync(context.Solution, updates, context, cancellationToken).ConfigureAwait(false)
            : context.Solution;

        using var _ = PooledDictionary<DocumentUri, LSP.TextEdit[]>.GetInstance(out var uriToEditsMap);
        foreach (var codeMapping in request.Mappings)
        {
            var mappingResult = await MapCodeAsync(codeMapping).ConfigureAwait(false);

            if (mappingResult is not (DocumentUri uri, LSP.TextEdit[] textEdits))
            {
                // Failed the entire request if any of the sub-requests failed
                return null;
            }

            // multiple MapCodeMappings for the same document is not supported.
            uriToEditsMap.Add(uri, textEdits);
        }

        // return a combined WorkspaceEdit
        if (context.GetRequiredClientCapabilities().Workspace?.WorkspaceEdit?.DocumentChanges is true)
        {
            return new WorkspaceEdit
            {
                DocumentChanges = uriToEditsMap.Select(kvp => new TextDocumentEdit
                {
                    TextDocument = new OptionalVersionedTextDocumentIdentifier { DocumentUri = kvp.Key },
                    Edits = [.. kvp.Value.Select(v => new SumType<LSP.TextEdit, LSP.AnnotatedTextEdit>(v))],
                }).ToArray()
            };
        }
        else
        {
            return new WorkspaceEdit
            {
                Changes = uriToEditsMap.ToDictionary(kvp => kvp.Key.GetDocumentFilePathFromUri(), kvp => kvp.Value)
            };
        }

        async Task<(DocumentUri, LSP.TextEdit[])?> MapCodeAsync(LSP.VSInternalMapCodeMapping codeMapping)
        {
            var textDocument = codeMapping.TextDocument
                ?? throw new ArgumentException($"mapCode sub-request failed: MapCodeMapping.TextDocument not expected to be null.");

            var document = await solution.GetDocumentAsync(textDocument, cancellationToken).ConfigureAwait(false);
            if (document is null)
                throw new ArgumentException($"mapCode sub-request for {textDocument.DocumentUri} failed: can't find this document in the workspace.");

            var codeMapper = document.GetRequiredLanguageService<IMapCodeService>();

            var focusLocations = await ConvertFocusLocationsToDocumentAndSpansAsync(
                document,
                textDocument,
                codeMapping.FocusLocations,
                cancellationToken).ConfigureAwait(false);

            var textChanges = await codeMapper.MapCodeAsync(
                document,
                codeMapping.Contents.ToImmutableArrayOrEmpty(),
                focusLocations,
                cancellationToken).ConfigureAwait(false);

            if (textChanges is null)
            {
                context.TraceDebug($"mapCode sub-request for {textDocument.DocumentUri} failed: 'IMapCodeService.MapCodeAsync' returns null.");
                return null;
            }

            var oldText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var textEdits = textChanges.Value.Select(change => ProtocolConversions.TextChangeToTextEdit(change, oldText)).ToArray();

            return (textDocument.DocumentUri, textEdits);
        }

        async Task<ImmutableArray<(Document, TextSpan)>> ConvertFocusLocationsToDocumentAndSpansAsync(
            Document document, TextDocumentIdentifier textDocumentIdentifier, LSP.Location[][]? focusLocations, CancellationToken cancellationToken)
        {
            if (focusLocations is null)
                return [];

            var focusText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            using var _ = ArrayBuilder<(Document, TextSpan)>.GetInstance(out var builder);
            foreach (var locationsOfSamePriority in focusLocations)
            {
                foreach (var location in locationsOfSamePriority)
                {
                    // Ignore anything not in target document, which current code mapper doesn't handle anyway
                    if (!location.DocumentUri.Equals(textDocumentIdentifier.DocumentUri))
                    {
                        context.TraceDebug($"A focus location in '{textDocumentIdentifier.DocumentUri}' is skipped, only locations in corresponding MapCodeMapping.TextDocument is currently considered.");
                        continue;
                    }

                    builder.Add((document, ProtocolConversions.RangeToTextSpan(location.Range, focusText)));
                }
            }

            return builder.ToImmutableAndClear();
        }

        static async Task<Solution> ApplyWorkspaceTextEditsAsync(
            Solution solution,
            WorkspaceEdit updates,
            RequestContext context,
            CancellationToken cancellationToken)
        {
            if (updates.Changes is not null && updates.DocumentChanges is not null)
                throw new ArgumentException("mapCode workspace updates cannot contain both changes and documentChanges.");

            var workspaceEditCapabilities = context.GetRequiredClientCapabilities().Workspace?.WorkspaceEdit;
            if (updates.DocumentChanges is not null && workspaceEditCapabilities?.DocumentChanges is not true)
            {
                throw new NotSupportedException(
                    "mapCode workspace updates use documentChanges, but the client did not advertise workspaceEdit.documentChanges.");
            }

            if (updates.Changes is { } changes)
            {
                foreach (var (uriString, edits) in changes)
                    solution = await ApplyTextEditsAsync(solution, new DocumentUri(uriString), edits).ConfigureAwait(false);
            }
            else if (updates.DocumentChanges is { } documentChanges)
            {
                if (documentChanges.TryGetFirst(out var textDocumentEdits))
                {
                    foreach (var textDocumentEdit in textDocumentEdits)
                        solution = await ApplyTextDocumentEditAsync(solution, textDocumentEdit).ConfigureAwait(false);
                }
                else if (documentChanges.TryGetSecond(out var mixedDocumentChanges))
                {
                    foreach (var documentChange in mixedDocumentChanges)
                    {
                        if (documentChange.TryGetFirst(out var textDocumentEdit))
                        {
                            solution = await ApplyTextDocumentEditAsync(solution, textDocumentEdit).ConfigureAwait(false);
                        }
                        else
                        {
                            solution = ApplyResourceOperation(solution, documentChange.Value);
                        }
                    }
                }
            }

            return solution;

            async Task<Solution> ApplyTextDocumentEditAsync(Solution currentSolution, TextDocumentEdit textDocumentEdit)
            {
                var documentUri = textDocumentEdit.TextDocument.DocumentUri;
                if (textDocumentEdit.TextDocument.Version is { } version)
                {
                    if (!context.IsTracking(documentUri))
                    {
                        throw new NotSupportedException(
                            "versioned mapCode workspace updates require an LSP-tracked document.");
                    }

                    var trackedDocument = context.GetTrackedDocumentInfo(documentUri);
                    if (trackedDocument.LspVersion != version)
                    {
                        throw new InvalidOperationException(
                            $"mapCode workspace update for {documentUri} is stale: expected LSP version {trackedDocument.LspVersion}, received {version}.");
                    }
                }

                var edits = textDocumentEdit.Edits.Select(edit =>
                {
                    if (edit.TryGetFirst(out var textEdit))
                        return textEdit;

                    if (edit.TryGetSecond(out var annotatedTextEdit))
                    {
                        if (workspaceEditCapabilities?.ChangeAnnotationSupport is null)
                        {
                            throw new NotSupportedException(
                                "annotated mapCode workspace updates require client changeAnnotationSupport.");
                        }

                        return (LSP.TextEdit)annotatedTextEdit;
                    }

                    throw new InvalidOperationException("mapCode workspace update contained an invalid text edit.");
                });

                return await ApplyTextEditsAsync(currentSolution, documentUri, edits).ConfigureAwait(false);
            }

            async Task<Solution> ApplyTextEditsAsync(Solution currentSolution, DocumentUri documentUri, IEnumerable<LSP.TextEdit> edits)
            {
                var documentIds = currentSolution.GetDocumentIds(documentUri);
                if (documentIds.IsEmpty)
                    throw new ArgumentException($"mapCode workspace update targets an unknown document: {documentUri}");

                foreach (var documentId in documentIds)
                {
                    var document = currentSolution.GetRequiredDocument(documentId);
                    var oldText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
                    var textChanges = edits
                        .Select(edit => ProtocolConversions.TextEditToTextChange(edit, oldText))
                        .OrderBy(change => change.Span.Start)
                        .ToArray();
                    for (var index = 1; index < textChanges.Length; index++)
                    {
                        if (textChanges[index - 1].Span.End > textChanges[index].Span.Start)
                        {
                            throw new ArgumentException($"mapCode workspace updates contain overlapping edits for {documentUri}.");
                        }
                    }

                    currentSolution = currentSolution.WithDocumentText(documentId, oldText.WithChanges(textChanges));
                }

                return currentSolution;
            }

            Solution ApplyResourceOperation(Solution currentSolution, object resourceOperation)
            {
                if (resourceOperation is not IAnnotatedChange annotatedChange)
                    throw new InvalidOperationException("mapCode workspace update contained an unknown resource operation.");

                if (annotatedChange.AnnotationId is not null && workspaceEditCapabilities?.ChangeAnnotationSupport is null)
                {
                    throw new NotSupportedException(
                        "annotated mapCode workspace updates require client changeAnnotationSupport.");
                }

                var resourceKind = resourceOperation switch
                {
                    CreateFile => ResourceOperationKind.Create,
                    RenameFile => ResourceOperationKind.Rename,
                    DeleteFile => ResourceOperationKind.Delete,
                    _ => throw new InvalidOperationException("mapCode workspace update contained an unknown resource operation."),
                };

                if (workspaceEditCapabilities?.ResourceOperations is not { } supportedOperations ||
                    !supportedOperations.Contains(resourceKind))
                {
                    throw new NotSupportedException(
                        $"mapCode workspace updates require client support for the '{resourceKind.Value}' resource operation.");
                }

                return resourceOperation switch
                {
                    CreateFile createFile => ApplyCreateFile(currentSolution, createFile),
                    RenameFile renameFile => ApplyRenameFile(currentSolution, renameFile),
                    DeleteFile deleteFile => ApplyDeleteFile(currentSolution, deleteFile),
                    _ => throw new InvalidOperationException("mapCode workspace update contained an unknown resource operation."),
                };
            }

            static Solution ApplyCreateFile(Solution currentSolution, CreateFile createFile)
            {
                var documentUri = createFile.DocumentUri;
                var existingDocumentIds = currentSolution.GetDocumentIds(documentUri);
                if (!existingDocumentIds.IsEmpty)
                {
                    if (createFile.Options?.IgnoreIfExists is true)
                        return currentSolution;

                    if (createFile.Options?.Overwrite is not true)
                    {
                        throw new InvalidOperationException($"mapCode create file target already exists: {documentUri}");
                    }

                    currentSolution = currentSolution.RemoveDocuments(existingDocumentIds);
                }

                if (documentUri.ParsedUri?.IsFile is not true)
                {
                    throw new NotSupportedException(
                        $"mapCode create file requires a file URI that can be associated with a project: {documentUri}");
                }

                var filePath = documentUri.GetDocumentFilePathFromUri();
                var project = FindProjectForFilePath(currentSolution, filePath);
                var name = Path.GetFileName(filePath);
                if (string.IsNullOrEmpty(name))
                    throw new ArgumentException($"mapCode create file target has no file name: {documentUri}");

                return currentSolution.AddDocument(
                    DocumentId.CreateNewId(project.Id),
                    name,
                    SourceText.From(string.Empty),
                    GetFolders(project, filePath),
                    filePath);
            }

            static Solution ApplyRenameFile(Solution currentSolution, RenameFile renameFile)
            {
                if (renameFile.OldDocumentUri.Equals(renameFile.NewDocumentUri))
                    return currentSolution;

                var oldDocumentIds = currentSolution.GetDocumentIds(renameFile.OldDocumentUri);
                if (oldDocumentIds.IsEmpty)
                    throw new ArgumentException($"mapCode rename file source does not exist: {renameFile.OldDocumentUri}");

                var targetDocumentIds = currentSolution.GetDocumentIds(renameFile.NewDocumentUri);
                if (!targetDocumentIds.IsEmpty)
                {
                    if (renameFile.Options?.IgnoreIfExists is true)
                        return currentSolution;

                    if (renameFile.Options?.Overwrite is not true)
                    {
                        throw new InvalidOperationException($"mapCode rename file target already exists: {renameFile.NewDocumentUri}");
                    }

                    currentSolution = currentSolution.RemoveDocuments(targetDocumentIds);
                }

                if (renameFile.NewDocumentUri.ParsedUri?.IsFile is not true)
                {
                    throw new NotSupportedException(
                        $"mapCode rename file requires a file URI that can be associated with a project: {renameFile.NewDocumentUri}");
                }

                var newFilePath = renameFile.NewDocumentUri.GetDocumentFilePathFromUri();
                var newName = Path.GetFileName(newFilePath);
                if (string.IsNullOrEmpty(newName))
                    throw new ArgumentException($"mapCode rename file target has no file name: {renameFile.NewDocumentUri}");

                foreach (var documentId in oldDocumentIds)
                {
                    var document = currentSolution.GetRequiredDocument(documentId);
                    currentSolution = currentSolution
                        .WithDocumentName(documentId, newName)
                        .WithDocumentFolders(documentId, GetFolders(document.Project, newFilePath))
                        .WithDocumentFilePath(documentId, newFilePath);
                }

                return currentSolution;
            }

            static Solution ApplyDeleteFile(Solution currentSolution, DeleteFile deleteFile)
            {
                var documentIds = currentSolution.GetDocumentIds(deleteFile.DocumentUri);
                if (documentIds.IsEmpty)
                {
                    if (deleteFile.Options?.Recursive is true && deleteFile.DocumentUri.ParsedUri?.IsFile is true)
                    {
                        documentIds = GetDocumentIdsUnderDirectory(
                            currentSolution,
                            deleteFile.DocumentUri.GetDocumentFilePathFromUri());
                    }

                    if (!documentIds.IsEmpty)
                        return currentSolution.RemoveDocuments(documentIds);

                    if (deleteFile.Options?.IgnoreIfNotExists is true)
                        return currentSolution;

                    throw new ArgumentException($"mapCode delete file target does not exist: {deleteFile.DocumentUri}");
                }

                return currentSolution.RemoveDocuments(documentIds);
            }

            static ImmutableArray<DocumentId> GetDocumentIdsUnderDirectory(Solution currentSolution, string directoryPath)
            {
                using var _ = ArrayBuilder<DocumentId>.GetInstance(out var documentIds);
                foreach (var project in currentSolution.Projects)
                {
                    foreach (var documentId in project.DocumentIds)
                    {
                        var document = currentSolution.GetRequiredDocument(documentId);
                        if (document.FilePath is { } filePath && IsPathUnderDirectory(filePath, directoryPath))
                            documentIds.Add(documentId);
                    }
                }

                return documentIds.ToImmutableAndClear();
            }

            static Project FindProjectForFilePath(Solution currentSolution, string filePath)
            {
                var candidates = currentSolution.Projects
                    .Where(project => project.FilePath is { } projectFilePath &&
                        Path.GetDirectoryName(projectFilePath) is { } projectDirectory &&
                        IsPathUnderDirectory(filePath, projectDirectory))
                    .ToArray();

                if (candidates.Length == 1)
                    return candidates[0];

                if (candidates.Length == 0 && currentSolution.ProjectIds.Length == 1)
                    return currentSolution.GetRequiredProject(currentSolution.ProjectIds[0]);

                throw new NotSupportedException(
                    $"mapCode create file could not identify a unique project for '{filePath}'.");
            }

            static bool IsPathUnderDirectory(string filePath, string directoryPath)
            {
                var normalizedDirectory = Path.GetFullPath(directoryPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                var normalizedFile = Path.GetFullPath(filePath);
                return normalizedFile.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
            }

            static string[] GetFolders(Project project, string filePath)
            {
                if (project.FilePath is null)
                    return [];

                var projectDirectory = Path.GetDirectoryName(project.FilePath);
                var documentDirectory = Path.GetDirectoryName(filePath);
                if (projectDirectory is null || documentDirectory is null || !IsPathUnderDirectory(filePath, projectDirectory))
                    return [];

                var relativeDirectory = Path.GetRelativePath(projectDirectory, documentDirectory);
                return relativeDirectory == "."
                    ? []
                    : relativeDirectory.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
            }
        }
    }
}
