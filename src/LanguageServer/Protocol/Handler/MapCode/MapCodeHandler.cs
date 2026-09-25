// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
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
            ? await ApplyWorkspaceTextEditsAsync(context.Solution, updates, cancellationToken).ConfigureAwait(false)
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

        static async Task<Solution> ApplyWorkspaceTextEditsAsync(Solution solution, WorkspaceEdit updates, CancellationToken cancellationToken)
        {
            if (updates.Changes is not null && updates.DocumentChanges is not null)
                throw new ArgumentException("mapCode workspace updates cannot contain both changes and documentChanges.");

            var editsByUri = new Dictionary<DocumentUri, List<LSP.TextEdit>>();

            if (updates.Changes is { } changes)
            {
                foreach (var (uriString, edits) in changes)
                    AddEdits(new DocumentUri(uriString), edits);
            }
            else if (updates.DocumentChanges is { } documentChanges)
            {
                if (documentChanges.TryGetFirst(out var textDocumentEdits))
                {
                    foreach (var textDocumentEdit in textDocumentEdits)
                        AddTextDocumentEdit(textDocumentEdit);
                }
                else if (documentChanges.TryGetSecond(out var mixedDocumentChanges))
                {
                    foreach (var documentChange in mixedDocumentChanges)
                    {
                        if (!documentChange.TryGetFirst(out var textDocumentEdit))
                        {
                            throw new NotSupportedException("mapCode workspace updates containing resource operations are not supported.");
                        }

                        AddTextDocumentEdit(textDocumentEdit);
                    }
                }
            }

            foreach (var (documentUri, edits) in editsByUri)
            {
                var documentIds = solution.GetDocumentIds(documentUri);
                if (documentIds.IsEmpty)
                    throw new ArgumentException($"mapCode workspace update targets an unknown document: {documentUri}");

                foreach (var documentId in documentIds)
                {
                    var document = solution.GetRequiredDocument(documentId);
                    var oldText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
                    var textChanges = edits.Select(edit => ProtocolConversions.TextEditToTextChange(edit, oldText));
                    solution = solution.WithDocumentText(documentId, oldText.WithChanges(textChanges));
                }
            }

            return solution;

            void AddTextDocumentEdit(TextDocumentEdit textDocumentEdit)
            {
                if (textDocumentEdit.TextDocument.Version is not null)
                    throw new NotSupportedException("versioned mapCode workspace updates are not supported.");

                var edits = textDocumentEdit.Edits.Select(edit => edit.TryGetFirst(out var textEdit)
                    ? textEdit
                    : edit.TryGetSecond(out var annotatedTextEdit)
                        ? annotatedTextEdit
                        : throw new InvalidOperationException("mapCode workspace update contained an invalid text edit."));
                AddEdits(textDocumentEdit.TextDocument.DocumentUri, edits);
            }

            void AddEdits(DocumentUri documentUri, IEnumerable<LSP.TextEdit> edits)
            {
                if (!editsByUri.TryGetValue(documentUri, out var documentEdits))
                {
                    documentEdits = [];
                    editsByUri.Add(documentUri, documentEdits);
                }

                documentEdits.AddRange(edits);
            }
        }
    }
}
