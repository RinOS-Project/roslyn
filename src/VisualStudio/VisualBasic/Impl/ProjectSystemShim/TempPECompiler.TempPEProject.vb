' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.IO
Imports System.Runtime.InteropServices.ComTypes
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Host
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.VisualStudio.LanguageServices.VisualBasic.ProjectSystemShim.Interop
Imports Microsoft.VisualStudio.Shell.Interop

Namespace Microsoft.VisualStudio.LanguageServices.VisualBasic.ProjectSystemShim
    Partial Friend Class TempPECompiler
        Private Class TempPEProject
            Implements IVbCompilerProject

            Private ReadOnly _compilerHost As IVbCompilerHost
            Private ReadOnly _references As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Private ReadOnly _embeddedReferences As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Private ReadOnly _files As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

            Private _parseOptions As VisualBasicParseOptions
            Private _compilationOptions As VisualBasicCompilationOptions
            Private _globalImports As ImmutableArray(Of GlobalImport) = ImmutableArray(Of GlobalImport).Empty
            Private _outputPath As String
            Private _runtimeLibraries As ImmutableArray(Of String)

            Public Sub New(compilerHost As IVbCompilerHost)
                _compilerHost = compilerHost
            End Sub

            Public Function CompileAndGetErrorCount(metadataService As IMetadataService) As Integer
                Dim trees = _files.Select(Function(path)
                                              Using stream = FileUtilities.OpenRead(path)
                                                  Return SyntaxFactory.ParseSyntaxTree(SourceText.From(stream), options:=_parseOptions, path:=path)
                                              End Using
                                          End Function)

                Dim metadataReferences = _references.Concat(_runtimeLibraries) _
                                                      .Distinct(StringComparer.InvariantCultureIgnoreCase) _
                                                      .Select(Function(path)
                                                                  Dim properties = If(_embeddedReferences.Contains(path),
                                                                                      New MetadataReferenceProperties(embedInteropTypes:=True),
                                                                                      MetadataReferenceProperties.Assembly)
                                                                  Return metadataService.GetReference(path, properties)
                                                              End Function)

                Dim c = VisualBasicCompilation.Create(
                    Path.GetFileName(_outputPath),
                    trees,
                    metadataReferences,
                    _compilationOptions.WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default))

                Dim emitResult = c.Emit(_outputPath)

                Return emitResult.Diagnostics.Where(Function(d) d.Severity = DiagnosticSeverity.Error).Count()
            End Function

            Public Sub AddApplicationObjectVariable(wszClassName As String, wszMemberName As String) Implements IVbCompilerProject.AddApplicationObjectVariable
                Throw New NotSupportedException("VBA application object variables are not supported by the TempPE compiler.")
            End Sub

            Public Sub AddBuffer(wszBuffer As String, dwLen As Integer, wszMkr As String, itemid As UInteger, fAdvise As Boolean, fShowErrorsInTaskList As Boolean) Implements IVbCompilerProject.AddBuffer
                Throw New NotSupportedException("TempPE compilation accepts workspace files, not legacy in-memory buffers.")
            End Sub

            Public Function AddEmbeddedMetaDataReference(wszFileName As String) As Integer Implements IVbCompilerProject.AddEmbeddedMetaDataReference
                _references.Add(wszFileName)
                _embeddedReferences.Add(wszFileName)
                Return VSConstants.S_OK
            End Function

            Public Sub AddEmbeddedProjectReference(pReferencedCompilerProject As IVbCompilerProject) Implements IVbCompilerProject.AddEmbeddedProjectReference
                Throw New NotSupportedException("TempPE compilation does not consume legacy project references.")
            End Sub

            Public Sub AddFile(wszFileName As String, itemid As UInteger, fAddDuringOpen As Boolean) Implements IVbCompilerProject.AddFile
                ' We are only ever given VSITEMIDs that are Nil because a TempPE project isn't
                ' associated with a IVsHierarchy.
                Contract.ThrowIfFalse(itemid = VSConstants.VSITEMID.Nil)

                _files.Add(wszFileName)
            End Sub

            Public Sub AddImport(wszImport As String) Implements IVbCompilerProject.AddImport
                Try
                    _globalImports = _globalImports.Add(GlobalImport.Parse(wszImport))
                    If _compilationOptions IsNot Nothing Then
                        _compilationOptions = _compilationOptions.WithGlobalImports(_globalImports)
                    End If
                Catch ex As ArgumentException
                    ' Match the legacy project-system behavior: malformed imports are reported by
                    ' compilation diagnostics rather than escaping from the project callback.
                End Try
            End Sub

            Public Function AddMetaDataReference(wszFileName As String, bAssembly As Boolean) As Integer Implements IVbCompilerProject.AddMetaDataReference
                _references.Add(wszFileName)

                Return VSConstants.S_OK
            End Function

            Public Sub AddProjectReference(pReferencedCompilerProject As IVbCompilerProject) Implements IVbCompilerProject.AddProjectReference
                Throw New NotSupportedException("TempPE compilation does not consume legacy project references.")
            End Sub

            Public Sub AddResourceReference(wszFileName As String, wszName As String, fPublic As Boolean, fEmbed As Boolean) Implements IVbCompilerProject.AddResourceReference
                Throw New NotSupportedException("TempPE compilation does not embed legacy resource references.")
            End Sub

            Private _buildStatusCallback As IVbBuildStatusCallback

            Public Function AdviseBuildStatusCallback(pIVbBuildStatusCallback As IVbBuildStatusCallback) As UInteger Implements IVbCompilerProject.AdviseBuildStatusCallback
                _buildStatusCallback = pIVbBuildStatusCallback
                If pIVbBuildStatusCallback IsNot Nothing Then
                    pIVbBuildStatusCallback.ProjectBound()
                End If

                Return VSConstants.S_OK
            End Function

            Public Function CreateCodeModel(pProject As EnvDTE.Project, pProjectItem As EnvDTE.ProjectItem, ByRef pCodeModel As EnvDTE.CodeModel) As Integer Implements IVbCompilerProject.CreateCodeModel
                pCodeModel = Nothing
                Return VSConstants.E_NOTIMPL
            End Function

            Public Function CreateFileCodeModel(pProject As EnvDTE.Project, pProjectItem As EnvDTE.ProjectItem, ByRef pFileCodeModel As EnvDTE.FileCodeModel) As Integer Implements IVbCompilerProject.CreateFileCodeModel
                pFileCodeModel = Nothing
                Return VSConstants.E_NOTIMPL
            End Function

            Public Sub DeleteAllImports() Implements IVbCompilerProject.DeleteAllImports
                _globalImports = ImmutableArray(Of GlobalImport).Empty
                If _compilationOptions IsNot Nothing Then
                    _compilationOptions = _compilationOptions.WithGlobalImports(_globalImports)
                End If
            End Sub

            Public Sub DeleteAllResourceReferences() Implements IVbCompilerProject.DeleteAllResourceReferences
                Throw New NotSupportedException("TempPE compilation does not embed legacy resource references.")
            End Sub

            Public Sub DeleteImport(wszImport As String) Implements IVbCompilerProject.DeleteImport
                Dim index = -1
                For i = 0 To _globalImports.Length - 1
                    If _globalImports(i).Clause.ToFullString() = wszImport Then
                        index = i
                        Exit For
                    End If
                Next

                If index >= 0 Then
                    _globalImports = _globalImports.RemoveAt(index)
                    If _compilationOptions IsNot Nothing Then
                        _compilationOptions = _compilationOptions.WithGlobalImports(_globalImports)
                    End If
                End If
            End Sub

            Public Sub Disconnect() Implements IVbCompilerProject.Disconnect

            End Sub

            Public Function ENCRebuild(in_pProgram As Object, ByRef out_ppUpdate As Object) As Integer Implements IVbCompilerProject.ENCRebuild
                out_ppUpdate = Nothing
                Return VSConstants.S_FALSE
            End Function

            Public Sub FinishEdit() Implements IVbCompilerProject.FinishEdit
                ' The project system calls BeginEdit/FinishEdit so we can batch and avoid doing
                ' expensive things between each call to one of the Add* methods. But since we're not
                ' doing anything expensive, this can be a no-op.
            End Sub

            Public Function GetDefaultReferences(cElements As Integer, ByRef rgbstrReferences() As String, ByVal cActualReferences As IntPtr) As Integer Implements IVbCompilerProject.GetDefaultReferences
                Return VSConstants.E_NOTIMPL
            End Function

            Public Sub GetEntryPointsList(cItems As Integer, strList() As String, ByVal pcActualItems As IntPtr) Implements IVbCompilerProject.GetEntryPointsList
                Throw New NotSupportedException("TempPE entry-point enumeration is not part of the synchronous compiler contract.")
            End Sub

            Public Sub GetMethodFromLine(itemid As UInteger, iLine As Integer, ByRef pBstrProcName As String, ByRef pBstrClassName As String) Implements IVbCompilerProject.GetMethodFromLine
                Throw New NotSupportedException("TempPE source-line method lookup is not supported.")
            End Sub

            Public Sub GetPEImage(ByRef ppImage As IntPtr) Implements IVbCompilerProject.GetPEImage
                Throw New NotSupportedException("TempPE emits directly to its output path and does not expose an in-memory PE image.")
            End Sub

            Public Sub RemoveAllApplicationObjectVariables() Implements IVbCompilerProject.RemoveAllApplicationObjectVariables
                Throw New NotSupportedException("VBA application object variables are not supported by the TempPE compiler.")
            End Sub

            Public Sub RemoveAllReferences() Implements IVbCompilerProject.RemoveAllReferences
                _references.Clear()
                _embeddedReferences.Clear()
            End Sub

            Public Sub RemoveFile(wszFileName As String, itemid As UInteger) Implements IVbCompilerProject.RemoveFile
                Contract.ThrowIfFalse(itemid = VSConstants.VSITEMID.Nil)
                _files.Remove(wszFileName)
            End Sub

            Public Sub RemoveFileByName(wszPath As String) Implements IVbCompilerProject.RemoveFileByName
                _files.Remove(wszPath)
            End Sub

            Public Sub RemoveMetaDataReference(wszFileName As String) Implements IVbCompilerProject.RemoveMetaDataReference
                _references.Remove(wszFileName)
                _embeddedReferences.Remove(wszFileName)
            End Sub

            Public Sub RemoveProjectReference(pReferencedCompilerProject As IVbCompilerProject) Implements IVbCompilerProject.RemoveProjectReference
                Throw New NotSupportedException("TempPE compilation does not consume legacy project references.")
            End Sub

            Public Sub RenameDefaultNamespace(bstrDefaultNamespace As String) Implements IVbCompilerProject.RenameDefaultNamespace
                If _compilationOptions IsNot Nothing Then
                    _compilationOptions = _compilationOptions.WithRootNamespace(bstrDefaultNamespace)
                End If
            End Sub

            Public Sub RenameFile(wszOldFileName As String, wszNewFileName As String, itemid As UInteger) Implements IVbCompilerProject.RenameFile
                RemoveFile(wszOldFileName, itemid)
                AddFile(wszNewFileName, itemid, fAddDuringOpen:=False)
            End Sub

            Public Sub RenameProject(wszNewProjectName As String) Implements IVbCompilerProject.RenameProject
                Throw New NotSupportedException("TempPE projects do not expose a mutable project-system display name.")
            End Sub

            Public Sub ResumePostedNotifications() Implements IVbCompilerProject.ResumePostedNotifications
                ' TempPE compilation is synchronous and does not post compiler notifications.
            End Sub

            Public Sub SetBackgroundCompilerPriorityLow() Implements IVbCompilerProject.SetBackgroundCompilerPriorityLow
                ' TempPE compilation has no background compiler whose priority can be changed.
            End Sub

            Public Sub SetBackgroundCompilerPriorityNormal() Implements IVbCompilerProject.SetBackgroundCompilerPriorityNormal
                ' TempPE compilation has no background compiler whose priority can be changed.
            End Sub

            Public Sub SetCompilerOptions(ByRef pCompilerOptions As VBCompilerOptions) Implements IVbCompilerProject.SetCompilerOptions
                _runtimeLibraries = VisualBasicProject.OptionsProcessor.GetRuntimeLibraries(_compilerHost, pCompilerOptions)
                _outputPath = PathUtilities.CombinePathsUnchecked(pCompilerOptions.wszOutputPath, pCompilerOptions.wszExeName)
                _parseOptions = VisualBasicProject.OptionsProcessor.ApplyVisualBasicParseOptionsFromCompilerOptions(VisualBasicParseOptions.Default, pCompilerOptions)

                ' Note that we pass a "default" compilation options with DLL set as output kind; the Apply method will figure out what the right one is and fix it up
                _compilationOptions = VisualBasicProject.OptionsProcessor.ApplyCompilationOptionsFromVBCompilerOptions(
                    New VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary, parseOptions:=_parseOptions).WithGlobalImports(_globalImports), pCompilerOptions)
            End Sub

            Public Sub SetModuleAssemblyName(wszName As String) Implements IVbCompilerProject.SetModuleAssemblyName
                Throw New NotSupportedException("TempPE output naming is controlled by SetCompilerOptions.")
            End Sub

            Public Sub SetStreamForPDB(pStreamPDB As IStream) Implements IVbCompilerProject.SetStreamForPDB
                Throw New NotSupportedException("TempPE emits PDB data through compiler options and does not accept a legacy stream.")
            End Sub

            Public Sub StartBuild(pVsOutputWindowPane As IVsOutputWindowPane, fRebuildAll As Boolean) Implements IVbCompilerProject.StartBuild
                ' The owning TempPECompiler performs the synchronous build from Compile().
            End Sub

            Public Sub StartDebugging() Implements IVbCompilerProject.StartDebugging
                ' TempPE projects do not own a debugger session.
            End Sub

            Public Sub StartEdit() Implements IVbCompilerProject.StartEdit
                ' The project system calls BeginEdit/FinishEdit so we can batch and avoid doing
                ' expensive things between each call to one of the Add* methods. But since we're not
                ' doing anything expensive, this can be a no-op.
            End Sub

            Public Sub StopBuild() Implements IVbCompilerProject.StopBuild
                ' The owning TempPECompiler performs the synchronous build from Compile().
            End Sub

            Public Sub StopDebugging() Implements IVbCompilerProject.StopDebugging
                ' TempPE projects do not own a debugger session.
            End Sub

            Public Sub SuspendPostedNotifications() Implements IVbCompilerProject.SuspendPostedNotifications
                ' TempPE compilation is synchronous and does not post compiler notifications.
            End Sub

            Public Sub UnadviseBuildStatusCallback(dwCookie As UInteger) Implements IVbCompilerProject.UnadviseBuildStatusCallback
                Contract.ThrowIfFalse(dwCookie = 0)
                _buildStatusCallback = Nothing
            End Sub

            Public Sub WaitUntilBound() Implements IVbCompilerProject.WaitUntilBound
                ' TempPE compilation is synchronous, so there is no background bind to wait for.
            End Sub

        End Class
    End Class
End Namespace
