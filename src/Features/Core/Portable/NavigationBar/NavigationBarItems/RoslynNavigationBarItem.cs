// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.NavigationBar;

/// <summary>
/// Base type of all C#/VB navigation bar items.  Only for use internally to roslyn.
/// </summary>
internal abstract partial class RoslynNavigationBarItem : IEquatable<RoslynNavigationBarItem>
{
    public readonly RoslynNavigationBarItemKind Kind;

    public readonly string Text;
    public readonly Glyph Glyph;
    public readonly bool Bolded;
    public readonly bool Grayed;
    public readonly int Indent;
    public readonly ImmutableArray<RoslynNavigationBarItem> ChildItems;

    protected RoslynNavigationBarItem(
        RoslynNavigationBarItemKind kind,
        string text,
        Glyph glyph,
        bool bolded,
        bool grayed,
        int indent,
        ImmutableArray<RoslynNavigationBarItem> childItems)
    {
        Kind = kind;
        Text = text;
        Glyph = glyph;
        ChildItems = childItems.NullToEmpty();
        Indent = indent;
        Bolded = bolded;
        Grayed = grayed;
    }

    protected internal abstract SerializableNavigationBarItem Dehydrate();

        public abstract override bool Equals(object? obj);
        public abstract override int GetHashCode();

        protected int GetHashCodeCore()
        {
            var hashCode = Hash.CombineValues(ChildItems);
            hashCode = Hash.Combine(Indent, hashCode);
            hashCode = Hash.Combine(Grayed, hashCode);
            hashCode = Hash.Combine(Bolded, hashCode);
            hashCode = Hash.Combine((int)Glyph, hashCode);
            hashCode = Hash.Combine(Text, hashCode);
            return Hash.Combine((int)Kind, hashCode);
        }

    public bool Equals(RoslynNavigationBarItem? other)
    {
        return other != null &&
               Kind == other.Kind &&
               Text == other.Text &&
               Glyph == other.Glyph &&
               Bolded == other.Bolded &&
               Grayed == other.Grayed &&
               Indent == other.Indent &&
               ChildItems.SequenceEqual(other.ChildItems);
    }
}
