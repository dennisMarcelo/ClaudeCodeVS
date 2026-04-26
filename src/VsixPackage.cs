using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using ClaudeCodeVS.Commands;
using ClaudeCodeVS.Ide;
using ClaudeCodeVS.Protocol;
using ClaudeCodeVS.Protocol.Tools;
using ClaudeCodeVS.UI;
using Task = System.Threading.Tasks.Task;

namespace ClaudeCodeVS
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("Claude Code for Visual Studio", "Connects VS to Claude Code CLI via MCP", "0.1.0")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(ClaudeToolWindow), Style = VsDockStyle.Tabbed, Window = EnvDTE.Constants.vsWindowKindOutput)]
    [ProvideAutoLoad(Microsoft.VisualStudio.VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(Microsoft.VisualStudio.VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(PackageIds.PackageGuidString)]
    public sealed class VsixPackage : AsyncPackage
    {
        private IdeServices _ide;
        private SelectionTracker _selectionTracker;
        private DiffCoordinator _diffCoordinator;
        private LockFileManager _lockFile;
        private McpWebSocketServer _server;
        private ToolRegistry _tools;
        private JsonRpcDispatcher _dispatcher;

        public static VsixPackage Instance { get; private set; }

        public McpWebSocketServer Server => _server;
        public IdeServices Ide => _ide;
        public SelectionTracker SelectionTracker => _selectionTracker;
        public DiffCoordinator DiffCoordinator => _diffCoordinator;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            Instance = this;

            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            _ide = await IdeServices.CreateAsync(this, cancellationToken);
            _selectionTracker = new SelectionTracker(_ide);
            _diffCoordinator = new DiffCoordinator(_ide);

            _tools = new ToolRegistry();
            _tools.RegisterDefaults(_ide, _selectionTracker, _diffCoordinator);

            _dispatcher = new JsonRpcDispatcher(_tools);

            var token = AuthToken.Generate();
            _server = new McpWebSocketServer(token, _dispatcher);
            int port = await _server.StartAsync(cancellationToken);

            _selectionTracker.SelectionChanged += OnSelectionChanged;

            _lockFile = new LockFileManager();
            await _lockFile.WriteAsync(port, token, _ide.GetWorkspaceFolders(), "Visual Studio 2022", cancellationToken);

            await OpenToolWindowCommand.InitializeAsync(this);
            await AddSelectionToClaudeCommand.InitializeAsync(this);
            await OpenTerminalWithClaudeCommand.InitializeAsync(this, port, token);
            await AcceptDiffCommand.InitializeAsync(this);
            await RejectDiffCommand.InitializeAsync(this);

            await base.InitializeAsync(cancellationToken, progress);
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _server?.BroadcastNotification("selection_changed", e.ToJson());
        }

        protected override int QueryClose(out bool canClose)
        {
            canClose = true;
            try
            {
                _server?.Stop();
                _lockFile?.Delete();
                _selectionTracker?.Dispose();
            }
            catch
            {
            }
            return Microsoft.VisualStudio.VSConstants.S_OK;
        }
    }
}
