// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis.LanguageServer.Handler.Testing;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.LanguageServer.Testing;

internal sealed class MtpProjectDetector(ILoggerFactory loggerFactory)
{
    private const string TestingPlatformServerCapability = "TestingPlatformServer";
    private const string AssemblyMetadataAttributeName = "AssemblyMetadataAttribute";
    private const string SystemReflectionNamespace = "System.Reflection";
    private const string TestingPlatformApplicationMetadataName = "Microsoft.Testing.Platform.Application";

    private readonly ILogger _logger = loggerFactory.CreateLogger<MtpProjectDetector>();

    public bool IsMtpProject(Project project, ProjectCapabilityManager projectCapabilityManager)
        => IsMtpProject(project.Id, project.CompilationOptions?.OutputKind, project.OutputFilePath, projectCapabilityManager);

    internal bool IsMtpProject(
        ProjectId projectId,
        OutputKind? outputKind,
        string? outputFilePath,
        ProjectCapabilityManager projectCapabilityManager)
    {
        if (projectCapabilityManager.TryHasCapability(projectId, TestingPlatformServerCapability, out var hasCapability))
            return hasCapability;

        if (outputKind is not (OutputKind.ConsoleApplication or OutputKind.WindowsApplication) ||
            outputFilePath is null)
        {
            return false;
        }

        return HasMtpApplicationMetadata(outputFilePath);
    }

    internal bool HasMtpApplicationMetadata(string outputFilePath)
    {
        if (!File.Exists(outputFilePath))
            return false;

        try
        {
            using var stream = File.OpenRead(outputFilePath);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
                return false;

            var metadataReader = peReader.GetMetadataReader();
            var assemblyDefinition = metadataReader.GetAssemblyDefinition();
            foreach (var customAttributeHandle in assemblyDefinition.GetCustomAttributes())
            {
                var customAttribute = metadataReader.GetCustomAttribute(customAttributeHandle);
                if (!IsAssemblyMetadataAttribute(metadataReader, customAttribute.Constructor))
                    continue;

                var valueReader = metadataReader.GetBlobReader(customAttribute.Value);
                if (valueReader.ReadUInt16() != 1)
                    continue;

                var name = valueReader.ReadSerializedString();
                var value = valueReader.ReadSerializedString();
                if (name == TestingPlatformApplicationMetadataName &&
                    bool.TryParse(value, out var isTestingPlatformApplication) &&
                    isTestingPlatformApplication)
                {
                    return true;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException)
        {
            _logger.LogWarning(ex, "Failed to inspect test project output {OutputFilePath}", outputFilePath);
        }

        return false;
    }

    private static bool IsAssemblyMetadataAttribute(MetadataReader metadataReader, EntityHandle constructor)
    {
        if (constructor.Kind != HandleKind.MemberReference)
            return false;

        var memberReference = metadataReader.GetMemberReference((MemberReferenceHandle)constructor);
        if (memberReference.Parent.Kind != HandleKind.TypeReference)
            return false;

        var typeReference = metadataReader.GetTypeReference((TypeReferenceHandle)memberReference.Parent);
        return metadataReader.StringComparer.Equals(typeReference.Name, AssemblyMetadataAttributeName) &&
            metadataReader.StringComparer.Equals(typeReference.Namespace, SystemReflectionNamespace);
    }
}
