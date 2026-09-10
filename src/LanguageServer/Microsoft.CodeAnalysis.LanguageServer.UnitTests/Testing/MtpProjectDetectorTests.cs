// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.LanguageServer.Handler.Testing;
using Microsoft.CodeAnalysis.LanguageServer.Testing;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.CodeAnalysis.LanguageServer.UnitTests.Testing;

public sealed class MtpProjectDetectorTests : IDisposable
{
    private readonly TempRoot _tempRoot = new();

    public void Dispose()
        => _tempRoot.Dispose();

    [Fact]
    public void HasMtpApplicationMetadata()
    {
        var outputPath = EmitAssembly("""[assembly: AssemblyMetadata("Microsoft.Testing.Platform.Application", "true")]""");

        Assert.True(CreateDetector().HasMtpApplicationMetadata(outputPath));
    }

    [Fact]
    public void FalseMtpApplicationMetadataIsNotMtp()
    {
        var outputPath = EmitAssembly("""[assembly: AssemblyMetadata("Microsoft.Testing.Platform.Application", "false")]""");

        Assert.False(CreateDetector().HasMtpApplicationMetadata(outputPath));
    }

    [Fact]
    public void MissingOutputFileIsNotMtp()
    {
        var outputPath = Path.Combine(_tempRoot.CreateDirectory().Path, "TestProject.dll");

        Assert.False(CreateDetector().HasMtpApplicationMetadata(outputPath));
    }

    [Fact]
    public void KnownCapabilityValueTakesPrecedenceOverOutputMetadata()
    {
        var outputPath = EmitAssembly("""[assembly: AssemblyMetadata("Microsoft.Testing.Platform.Application", "true")]""");
        var projectId = ProjectId.CreateNewId();
        var projectCapabilityManager = new ProjectCapabilityManager();
        projectCapabilityManager.UpdateCapabilities(projectId, ["Unrelated"]);

        Assert.False(CreateDetector().IsMtpProject(
            projectId,
            OutputKind.ConsoleApplication,
            outputPath,
            projectCapabilityManager));
    }

    [Fact]
    public void OutputMetadataIsUsedWhenCapabilitiesAreUnavailable()
    {
        var outputPath = EmitAssembly("""[assembly: AssemblyMetadata("Microsoft.Testing.Platform.Application", "true")]""");

        Assert.True(CreateDetector().IsMtpProject(
            ProjectId.CreateNewId(),
            OutputKind.ConsoleApplication,
            outputPath,
            new ProjectCapabilityManager()));
    }

    private string EmitAssembly(string source)
    {
        var directory = _tempRoot.CreateDirectory();
        var outputPath = Path.Combine(directory.Path, "TestProject.dll");
        var compilation = CSharpCompilation.Create(
            "TestProject",
            [CSharpSyntaxTree.ParseText($"using System.Reflection;{Environment.NewLine}{source}")],
            [MetadataReference.CreateFromFile(typeof(AssemblyMetadataAttribute).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var emitResult = compilation.Emit(outputPath);
        Assert.True(emitResult.Success, string.Join(Environment.NewLine, emitResult.Diagnostics));
        return outputPath;
    }

    private static MtpProjectDetector CreateDetector()
        => new(NullLoggerFactory.Instance);
}
