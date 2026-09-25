// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.CodeAnalysis.InlineHints;
using Microsoft.CodeAnalysis.Text.Shared.Extensions;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.InlineHints;

/// <summary>
/// The simple tag that only holds information regarding the associated parameter name
/// for the argument
/// </summary>
internal sealed class InlineHintDataTag<TAdditionalInformation>(
    InlineHintsDataTaggerProvider<TAdditionalInformation> provider, ITextSnapshot snapshot, InlineHint hint)
    : ITag, IEquatable<InlineHintDataTag<TAdditionalInformation>>
    where TAdditionalInformation : class
{
    private readonly InlineHintsDataTaggerProvider<TAdditionalInformation> _provider = provider;

    /// <summary>
    /// The snapshot this tag was created against.
    /// </summary>
    private readonly ITextSnapshot _snapshot = snapshot;

    public readonly InlineHint Hint = hint;

    /// <summary>
    /// Additional data that can be attached to the tag.  For example, the view tagger uses this to attach the adornment
    /// tag information so it can be created and cached on demand.
    /// </summary>
    public TAdditionalInformation? AdditionalData;

    // Spans are tracking spans and equality can map them across snapshots, so there is no stable position value
    // that can be used here. A constant hash preserves the equality contract without making tag collections throw.
    public override int GetHashCode()
        => 0;

    public override bool Equals(object? obj)
        => obj is InlineHintDataTag<TAdditionalInformation> tag && Equals(tag);

    public bool Equals(InlineHintDataTag<TAdditionalInformation>? other)
    {
        if (other is null)
            return false;

        // they have to match if they're going to change text.
        if (this.Hint.ReplacementTextChange is null != other.Hint.ReplacementTextChange is null)
            return false;

        // the text change text has to match.
        if (this.Hint.ReplacementTextChange?.NewText != other.Hint.ReplacementTextChange?.NewText)
            return false;

        // Ensure both hints are talking about the same snapshot.
        if (!_provider.SpanEquals(this.Hint.Span.ToSnapshotSpan(_snapshot), other.Hint.Span.ToSnapshotSpan(other._snapshot)))
            return false;

        if (this.Hint.ReplacementTextChange != null &&
            other.Hint.ReplacementTextChange != null &&
            !_provider.SpanEquals(this.Hint.ReplacementTextChange.Value.Span.ToSnapshotSpan(_snapshot), other.Hint.ReplacementTextChange.Value.Span.ToSnapshotSpan(other._snapshot)))
        {
            return false;
        }

        // ensure all the display parts are the same.
        return this.Hint.DisplayParts.SequenceEqual(other.Hint.DisplayParts);
    }
}
