// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage;

internal class VisualStudioTextChange : ITextChange
{
    private readonly string _oldText;

    public VisualStudioTextChange(int oldStart, int oldLength, string newText)
    {
        OldSpan = new Span(oldStart, oldLength);
        _oldText = string.Empty;
        NewText = newText;
    }

    public VisualStudioTextChange(TextEdit textEdit, ITextSnapshot textSnapshot)
        : this(
            textEdit.Range.Start.Line,
            textEdit.Range.Start.Character,
            textEdit.Range.End.Line,
            textEdit.Range.End.Character,
            textSnapshot,
            textEdit.NewText)
    {
    }

    public VisualStudioTextChange(int startLineNumber, int startCharacter, int endLineNumber, int endCharacter, ITextSnapshot textSnapshot, string newText)
    {
        var startLine = textSnapshot.GetLineFromLineNumber(startLineNumber);
        var startAbsoluteIndex = startLine.Start + startCharacter;
        var endLine = textSnapshot.GetLineFromLineNumber(endLineNumber);
        var endAbsoluteIndex = endLine.Start + endCharacter;
        var length = endAbsoluteIndex - startAbsoluteIndex;
        OldSpan = new Span(startAbsoluteIndex, length);
        _oldText = textSnapshot.GetText(OldSpan);
        NewText = newText;
    }

    public Span OldSpan { get; }
    public int OldPosition => OldSpan.Start;
    public int OldEnd => OldSpan.End;
    public int OldLength => OldSpan.Length;
    public string NewText { get; }
    public int NewLength => NewText.Length;

    public Span NewSpan => new(NewPosition, NewLength);

    public int NewPosition => OldPosition;
    public int Delta => NewLength - OldLength;
    public int NewEnd => NewPosition + NewLength;
    public string OldText => _oldText;
    public int LineCountDelta => CountLines(NewText) - CountLines(OldText);

    private static int CountLines(string text)
    {
        var count = 0;
        foreach (var character in text)
        {
            if (character == '\n')
            {
                count++;
            }
        }

        return count;
    }

    public override string ToString()
    {
        return OldSpan.ToString() + "->" + NewText;
    }
}
