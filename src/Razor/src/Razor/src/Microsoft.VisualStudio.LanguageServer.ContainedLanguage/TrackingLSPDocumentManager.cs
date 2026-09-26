// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage;

internal abstract class TrackingLSPDocumentManager : LSPDocumentManager
{
    public abstract void TrackDocument(ITextBuffer buffer);

    public abstract void UntrackDocument(ITextBuffer buffer);

    public abstract void UpdateVirtualDocument<TVirtualDocument>(
        Uri hostDocumentUri,
        IReadOnlyList<ITextChange> changes,
        int hostDocumentVersion,
        object? state) where TVirtualDocument : VirtualDocument;

    public virtual void UpdateVirtualDocument<TVirtualDocument>(
        Uri hostDocumentUri,
        Uri virtualDocumentUri,
        IReadOnlyList<ITextChange> changes,
        int hostDocumentVersion,
        object? state) where TVirtualDocument : VirtualDocument
    {
        // This is only virtual to prevent a binary breaking change. The legacy overload cannot
        // identify the virtual document URI, so an implementation must override it explicitly.
        throw new NotSupportedException(
            "The legacy UpdateVirtualDocument overload must be overridden by the document manager.");
    }
}
