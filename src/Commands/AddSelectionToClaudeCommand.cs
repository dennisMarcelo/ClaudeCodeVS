using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace ClaudeCodeVS.Commands
{
    internal static class AddSelectionToClaudeCommand
    {
        public static async Task InitializeAsync(AsyncPackage package)
        {
            var mcs = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (mcs == null) return;
            var id = new CommandID(PackageIds.CommandSet, PackageIds.AddSelectionToClaudeCommandId);
            var cmd = new MenuCommand(async (s, e) => await ExecuteAsync(package), id);
            mcs.AddCommand(cmd);
        }

        private static async Task ExecuteAsync(AsyncPackage package)
        {
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();
            var pkg = VsixPackage.Instance;
            if (pkg == null) return;
            var snap = pkg.SelectionTracker.Capture();
            if (snap == null) return;
            pkg.Server?.BroadcastNotification("at_mentioned", snap.ToJson());
        }
    }
}
