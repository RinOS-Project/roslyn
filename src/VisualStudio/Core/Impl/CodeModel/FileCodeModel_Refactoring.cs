// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.LanguageServices.Implementation.CodeModel.InternalElements;
using Microsoft.VisualStudio.LanguageServices.Implementation.CodeModel.Interop;
using Microsoft.VisualStudio.LanguageServices.Implementation.Interop;
using Microsoft.VisualStudio.LanguageServices.Implementation.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.CodeModel;

public sealed partial class FileCodeModel
{
    public void Rename(EnvDTE.CodeElement element)
        => throw Exceptions.ThrowENotImpl();

    public void RenameNoUI(EnvDTE.CodeElement element, string newName, bool fPreview, bool fSearchComments, bool fOverloads)
    {
        // TODO: Support options

        var codeElement = ComAggregate.TryGetManagedObject<AbstractCodeElement>(element);
        codeElement?.RenameSymbol(newName);
    }

    public void ReorderParameters(EnvDTE.CodeElement element)
        => throw Exceptions.ThrowENotImpl();

    public void ReorderParametersNoUI(EnvDTE.CodeElement element, long[] paramIndices, bool fPreview)
        => throw Exceptions.ThrowENotImpl();

    public void RemoveParameter(EnvDTE.CodeElement element)
        => throw Exceptions.ThrowENotImpl();

    public void RemoveParameterNoUI(EnvDTE.CodeElement element, object parameter, bool fPreview)
        => throw Exceptions.ThrowENotImpl();

    public void EncapsulateField(EnvDTE.CodeVariable variable)
        => throw Exceptions.ThrowENotImpl();

    public EnvDTE.CodeProperty EncapsulateFieldNoUI(EnvDTE.CodeVariable variable, string propertyName, EnvDTE.vsCMAccess accessibility, ReferenceSelectionEnum refSelection, PropertyTypeEnum propertyType, bool fPreview, bool fSearchComments)
        => throw Exceptions.ThrowENotImpl();

    public void ExtractInterface(EnvDTE.CodeType codeType)
        => throw Exceptions.ThrowENotImpl();

    public void ImplementInterface(EnvDTE.CodeType implementor, object @interface, bool fExplicit)
        => throw Exceptions.ThrowENotImpl();

    public void ImplementAbstractClass(EnvDTE.CodeType implementor, object abstractClass)
        => throw Exceptions.ThrowENotImpl();

    public EnvDTE.CodeElement ImplementOverride(EnvDTE.CodeElement member, EnvDTE.CodeType implementor)
        => throw Exceptions.ThrowENotImpl();
}
