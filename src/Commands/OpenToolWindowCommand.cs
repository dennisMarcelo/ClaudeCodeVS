using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using ClaudeCodeVS.UI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace ClaudeCodeVS.Commands
{
    internal static class OpenToolWindowCommand
    {
        public static async Task InitializeAsync(AsyncPackage package)
        {
            var mcs = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (mcs == null) return;
            var id = new CommandID(PackageIds.CommandSet, PackageIds.OpenToolWindowCommandId);
            var cmd = new MenuCommand(async (s, e) => await ExecuteAsync(package), id);
            mcs.AddCommand(cmd);
        }

        private static async Task ExecuteAsync(AsyncPackage package)
        {
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();
            var window = await package.ShowToolWindowAsync(typeof(ClaudeToolWindow), 0, true, package.DisposalToken);
            if (window?.Frame is IVsWindowFrame frame)
            {
                Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.Show());
            }
        }
    }
}
