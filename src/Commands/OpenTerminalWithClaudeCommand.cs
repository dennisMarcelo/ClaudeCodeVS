using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ClaudeCodeVS.Protocol;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace ClaudeCodeVS.Commands
{
    internal static class OpenTerminalWithClaudeCommand
    {
        private static int _port;
        private static string _token;

        public static async Task InitializeAsync(AsyncPackage package, int port, string token)
        {
            _port = port;
            _token = token;
            var mcs = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (mcs == null) return;
            var id = new CommandID(PackageIds.CommandSet, PackageIds.OpenTerminalWithClaudeCommandId);
            var cmd = new MenuCommand((s, e) => Execute(package), id);
            mcs.AddCommand(cmd);
        }

        public static void Execute(AsyncPackage package)
        {
            try
            {
                string workdir = VsixPackage.Instance?.Ide?.GetWorkspaceFolders() is string[] ws && ws.Length > 0
                    ? ws[0]
                    : Environment.CurrentDirectory;

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/K claude",
                    WorkingDirectory = workdir,
                    UseShellExecute = true,
                };
                psi.EnvironmentVariables["CLAUDE_CODE_SSE_PORT"] = _port.ToString();
                psi.EnvironmentVariables["CLAUDECODE"] = "1";
                psi.EnvironmentVariables["ENABLE_IDE_INTEGRATION"] = "true";

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ClaudeCodeVS] OpenTerminalWithClaude failed: " + ex);
            }
        }
    }
}
