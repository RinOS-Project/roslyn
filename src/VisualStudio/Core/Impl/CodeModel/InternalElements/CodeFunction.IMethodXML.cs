// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.IO;
using System.Threading;
using Microsoft.CodeAnalysis.Internal.Log;
using Microsoft.VisualStudio.LanguageServices.Implementation.CodeModel.Interop;
using Microsoft.VisualStudio.LanguageServices.Implementation.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.CodeModel.InternalElements;

public partial class CodeFunction
{
    public string GetXML()
    {
        using (Logger.LogBlock(FunctionId.WinformDesigner_GenerateXML, CancellationToken.None))
        {
            return CodeModelService.GetMethodXml(LookupNode(), GetSemanticModel());
        }
    }

    public int SetXML(string bstrXML)
    {
        throw Exceptions.ThrowENotImpl();
    }

    public int GetBodyPoint(out object ppUnk)
        => throw Exceptions.ThrowENotImpl();

    object IMethodXML2.GetXML()
        => new StringReader(GetXML());
}
