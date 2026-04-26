using System.ComponentModel.Design;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace ClaudeCodeVS.Commands
{
    internal static class RejectDiffCommand
    {
        public static async Task InitializeAsync(AsyncPackage package)
        {
            var mcs = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (mcs == null) return;
            var id = new CommandID(PackageIds.CommandSet, PackageIds.RejectDiffCommandId);
            mcs.AddCommand(new MenuCommand((s, e) => VsixPackage.Instance?.DiffCoordinator?.RejectTopmost(), id));
        }
    }
}
