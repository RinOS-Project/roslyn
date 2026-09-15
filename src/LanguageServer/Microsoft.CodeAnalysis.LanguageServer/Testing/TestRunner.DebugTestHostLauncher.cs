// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CodeAnalysis.LanguageServer.Handler.Testing;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client.Interfaces;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.LanguageServer.Testing;

internal sealed partial class TestRunner
{
    private sealed class DebugTestHostLauncher(BufferedProgress<RunTestsPartialResult> progress, IClientLanguageServerManager clientLanguageServerManager) : ITestHostLauncher2, ITestHostLauncher3
    {
        public bool IsDebug => true;

        public bool AttachDebuggerToProcess(int pid)
        {
            return AttachDebugger(pid, CancellationToken.None);
        }

        public bool AttachDebuggerToProcess(int pid, CancellationToken cancellationToken)
        {
            return AttachDebugger(pid, cancellationToken);
        }

        public bool AttachDebuggerToProcess(AttachDebuggerInfo attachDebuggerInfo, CancellationToken cancellationToken)
        {
            return AttachDebugger(attachDebuggerInfo.ProcessId, cancellationToken);
        }

        public int LaunchTestHost(TestProcessStartInfo defaultTestHostStartInfo)
        {
            return LaunchTestHost(defaultTestHostStartInfo, CancellationToken.None);
        }

        public int LaunchTestHost(TestProcessStartInfo defaultTestHostStartInfo, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var process = StartProcess(defaultTestHostStartInfo);
            if (!AttachDebugger(process.Id, cancellationToken))
            {
                TryTerminate(process);
                return -1;
            }

            return process.Id;
        }

        private static Process StartProcess(TestProcessStartInfo startInfo)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = startInfo.FileName,
                Arguments = startInfo.Arguments,
                UseShellExecute = false,
            };

            if (!string.IsNullOrEmpty(startInfo.WorkingDirectory))
            {
                processStartInfo.WorkingDirectory = startInfo.WorkingDirectory;
            }

            if (startInfo.EnvironmentVariables is not null)
            {
                foreach (var (name, value) in startInfo.EnvironmentVariables)
                {
                    processStartInfo.Environment[name] = value;
                }
            }

            return Process.Start(processStartInfo)
                ?? throw new InvalidOperationException($"Unable to start test host '{startInfo.FileName}'.");
        }

        private static void TryTerminate(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // The process exited before cleanup completed.
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // The process may have exited or become inaccessible during cleanup.
            }
        }

        private bool AttachDebugger(int processId, CancellationToken cancellationToken)
        {
            progress.Report(new RunTestsPartialResult(LanguageServerResources.Debugging_tests, string.Format(LanguageServerResources.Attaching_debugger_to_process_0, processId), Progress: null));

            // Send an explicit request to the client to tell it to attach to the debugger and wait for the response.
            // We want to wait for the attach to complete before we continue.
            var task = Task.Run(async () => await AttachDebuggerAsync(processId, cancellationToken), cancellationToken);
            return task.WaitAndGetResult_CanCallOnBackground(cancellationToken);
        }

        private async Task<bool> AttachDebuggerAsync(int processId, CancellationToken cancellationToken)
        {
            var request = new DebugAttachParams(processId);
            var result = await clientLanguageServerManager.SendRequestAsync<DebugAttachParams, DebugAttachResult>("workspace/attachDebugger", request, cancellationToken);
            if (!result.DidAttach)
            {
                progress.Report(new RunTestsPartialResult(LanguageServerResources.Debugging_tests, LanguageServerResources.Client_failed_to_attach_the_debugger, Progress: null));
            }

            return result.DidAttach;
        }
    }
}
