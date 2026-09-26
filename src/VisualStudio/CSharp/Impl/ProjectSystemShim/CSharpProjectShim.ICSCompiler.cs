// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using Microsoft.VisualStudio.LanguageServices.CSharp.ProjectSystemShim.Interop;

namespace Microsoft.VisualStudio.LanguageServices.CSharp.ProjectSystemShim;

internal partial class CSharpProjectShim : ICSCompiler
{
    public ICSSourceModule CreateSourceModule(ICSSourceText text)
        => throw new NotSupportedException("The Roslyn C# project shim does not expose legacy source modules.");

    public ICSNameTable GetNameTable()
        => throw new NotSupportedException("The Roslyn C# project shim does not expose the legacy compiler name table.");

    public void Shutdown()
    {
        // Project lifetime is owned by ICSharpProjectSite/Disconnect. There is no separate
        // in-proc compiler state to release here.
    }

    public ICSCompilerConfig GetConfiguration()
        => this;

    public ICSInputSet AddInputSet()
        => this;

    public void RemoveInputSet(ICSInputSet inputSet)
    {
        if (!ReferenceEquals(inputSet, this))
        {
            throw new ArgumentException("The C# project shim only owns its project input set.", nameof(inputSet));
        }
    }

    public void Compile(ICSCompileProgress progress)
        => throw new NotSupportedException("Legacy in-proc C# compilation is not used; compilation is owned by the workspace build host.");

    public void BuildForEnc(ICSCompileProgress progress, ICSEncProjectServices encService, object pe)
        => throw new NotSupportedException("Legacy C# Edit-and-Continue compilation is not exposed by the project shim.");

    public object CreateParser()
        => throw new NotSupportedException("Legacy C# parser objects are not exposed; use the Roslyn syntax APIs.");

    public object CreateLanguageAnalysisEngine()
        => throw new NotSupportedException("Legacy C# language analysis engines are not exposed by the project shim.");

    public void ReleaseReservedMemory()
    {
        // Roslyn's managed workspace/compiler path does not reserve native compiler memory.
    }
}
