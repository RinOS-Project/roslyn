// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.IO;
using System.Reflection;

namespace Microsoft.CodeAnalysis.Scripting.Hosting
{
    internal sealed class NotImplementedAnalyzerLoader : IAnalyzerAssemblyLoader
    {
        public void AddDependencyLocation(string fullPath)
        {
            RequireAbsolutePath(fullPath);
        }

        public Assembly LoadFromPath(string fullPath)
        {
            RequireAbsolutePath(fullPath);
            return Assembly.LoadFrom(fullPath);
        }

        private static void RequireAbsolutePath(string fullPath)
        {
            if (fullPath == null)
            {
                throw new ArgumentNullException(nameof(fullPath));
            }

            if (!Path.IsPathRooted(fullPath))
            {
                throw new ArgumentException("The analyzer path must be absolute.", nameof(fullPath));
            }
        }
    }
}
