// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis.CodeGen;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.VisualStudio.Debugger.Evaluation;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.ExpressionEvaluator
{
    internal abstract class EvaluationContextBase
    {
        internal static readonly AssemblyIdentity SystemIdentity = new AssemblyIdentity("System");
        internal static readonly AssemblyIdentity SystemCoreIdentity = new AssemblyIdentity("System.Core");
        internal static readonly AssemblyIdentity SystemLinqIdentity = new AssemblyIdentity("System.Linq");
        internal static readonly AssemblyIdentity SystemXmlIdentity = new AssemblyIdentity("System.Xml");
        internal static readonly AssemblyIdentity SystemXmlLinqIdentity = new AssemblyIdentity("System.Xml.Linq");
        internal static readonly AssemblyIdentity MicrosoftVisualBasicIdentity = new AssemblyIdentity("Microsoft.VisualBasic");

        internal abstract CompileResult? CompileExpression(
            string expr,
            DkmEvaluationFlags compilationFlags,
            ImmutableArray<Alias> aliases,
            DiagnosticBag diagnostics,
            out ResultProperties resultProperties,
            CompilationTestData? testData);

        internal abstract CompileResult? CompileAssignment(
            string target,
            string expr,
            ImmutableArray<Alias> aliases,
            DiagnosticBag diagnostics,
            out ResultProperties resultProperties,
            CompilationTestData? testData);

        internal abstract ReadOnlyCollection<byte> CompileGetLocals(
            ArrayBuilder<LocalAndMethod> locals,
            bool argumentsOnly,
            ImmutableArray<Alias> aliases,
            DiagnosticBag diagnostics,
            out string typeName,
            CompilationTestData? testData);

        internal string GetErrorMessageAndMissingAssemblyIdentities(
            DiagnosticBag diagnostics,
            DiagnosticFormatter formatter,
            CultureInfo? preferredUICulture,
            AssemblyIdentity linqLibrary,
            out bool useReferencedModulesOnly,
            out ImmutableArray<AssemblyIdentity> missingAssemblyIdentities)
        {
            var errors = diagnostics.AsEnumerable().Where(d => d.Severity == DiagnosticSeverity.Error);
            missingAssemblyIdentities = default;
            foreach (var error in errors)
            {
                missingAssemblyIdentities = this.GetMissingAssemblyIdentities(error, linqLibrary);
                if (!missingAssemblyIdentities.IsDefault)
                {
                    break;
                }
            }

            if (missingAssemblyIdentities.IsDefault)
            {
                missingAssemblyIdentities = ImmutableArray<AssemblyIdentity>.Empty;
            }

            useReferencedModulesOnly = errors.All(HasDuplicateTypesOrAssemblies);

            return GetErrorMessage(errors.First(), formatter, preferredUICulture);
        }

        internal static string GetErrorMessage(
            Diagnostic error,
            DiagnosticFormatter formatter,
            CultureInfo? preferredUICulture)
        {
            return (error is SimpleMessageDiagnostic simpleMessage)
                ? simpleMessage.GetMessage()
                : formatter.Format(error, preferredUICulture ?? CultureInfo.CurrentUICulture);
        }

        internal abstract bool HasDuplicateTypesOrAssemblies(Diagnostic diagnostic);

        internal abstract ImmutableArray<AssemblyIdentity> GetMissingAssemblyIdentities(Diagnostic diagnostic, AssemblyIdentity linqLibrary);

        // ILOffset == 0xffffffff indicates an instruction outside of IL.
        // Treat such values as the beginning of the IL.
        internal static int NormalizeILOffset(uint ilOffset)
        {
            return (ilOffset == uint.MaxValue) ? 0 : (int)ilOffset;
        }

        protected sealed class SimpleMessageDiagnostic : Diagnostic
        {
            private readonly string _message;
            private readonly DiagnosticDescriptor _descriptor;
            private readonly DiagnosticSeverity _severity;
            private readonly Location _location;
            private readonly bool _isSuppressed;

            internal SimpleMessageDiagnostic(
                string message,
                DiagnosticSeverity severity = DiagnosticSeverity.Error,
                Location? location = null,
                bool isSuppressed = false)
            {
                _message = message ?? throw new ArgumentNullException(nameof(message));
                _descriptor = new DiagnosticDescriptor(
                    id: "EE0001",
                    title: "Expression evaluator diagnostic",
                    messageFormat: message,
                    category: "ExpressionEvaluator",
                    defaultSeverity: DiagnosticSeverity.Error,
                    isEnabledByDefault: true);
                _severity = severity;
                _location = location ?? Location.None;
                _isSuppressed = isSuppressed;
            }

            public override IReadOnlyList<Location> AdditionalLocations
            {
                get { return Array.Empty<Location>(); }
            }

            public override DiagnosticDescriptor Descriptor
            {
                get { return _descriptor; }
            }

            public override string Id
            {
                get { return _descriptor.Id; }
            }

            public override Location Location
            {
                get { return _location; }
            }

            public override DiagnosticSeverity Severity
            {
                get { return _severity; }
            }

            public override DiagnosticSeverity DefaultSeverity
            {
                get { return DiagnosticSeverity.Error; }
            }

            public override bool IsSuppressed
            {
                get { return _isSuppressed; }
            }

            public override int WarningLevel
            {
                get { return GetDefaultWarningLevel(_severity); }
            }

            public override bool Equals(Diagnostic? obj)
            {
                return obj is SimpleMessageDiagnostic other &&
                    _message == other._message &&
                    _severity == other._severity &&
                    _location == other._location &&
                    _isSuppressed == other._isSuppressed;
            }

            public override int GetHashCode()
            {
                return Hash.Combine(_message, Hash.Combine(_location, Hash.Combine((int)_severity, _isSuppressed ? 1 : 0)));
            }

            public override string GetMessage(IFormatProvider? formatProvider = null)
            {
                return _message;
            }

            internal override Diagnostic WithLocation(Location location)
            {
                if (location is null)
                {
                    throw new ArgumentNullException(nameof(location));
                }

                return location == _location
                    ? this
                    : new SimpleMessageDiagnostic(_message, _severity, location, _isSuppressed);
            }

            internal override Diagnostic WithSeverity(DiagnosticSeverity severity)
            {
                return severity == _severity
                    ? this
                    : new SimpleMessageDiagnostic(_message, severity, _location, _isSuppressed);
            }

            internal override Diagnostic WithIsSuppressed(bool isSuppressed)
            {
                return isSuppressed == _isSuppressed
                    ? this
                    : new SimpleMessageDiagnostic(_message, _severity, _location, isSuppressed);
            }
        }
    }
}
