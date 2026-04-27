using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using ClaudeCodeVS.Commands;
using ClaudeCodeVS.Diagnostics;
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
        private SolutionTracker _solutionTracker;
        private DiffCoordinator _diffCoordinator;
        private LockFileManager _lockFile;
        private McpWebSocketServer _server;
        private ToolRegistry _tools;
        private JsonRpcDispatcher _dispatcher;

        public static VsixPackage Instance { get; private set; }

        internal McpWebSocketServer Server => _server;
        internal IdeServices Ide => _ide;
        internal SelectionTracker SelectionTracker => _selectionTracker;
        internal DiffCoordinator DiffCoordinator => _diffCoordinator;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            Instance = this;

            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            await Logger.InitAsync(this, cancellationToken);
            Logger.Info("Package", "Initializing ClaudeCodeVS extension.");

            try
            {
                _ide = await IdeServices.CreateAsync(this, cancellationToken);
                _selectionTracker = new SelectionTracker(_ide);
                _diffCoordinator = new DiffCoordinator(_ide);

                _tools = new ToolRegistry();
                _tools.RegisterDefaults(_ide, _selectionTracker, _diffCoordinator);

                _dispatcher = new JsonRpcDispatcher(_tools);

                var token = AuthToken.Generate();
                _server = new McpWebSocketServer(token, _dispatcher);
                int port = await _server.StartAsync(cancellationToken);
                Logger.Info("Package", "MCP WebSocket server bound on 127.0.0.1:" + port + ".");

                _selectionTracker.SelectionChanged += OnSelectionChanged;

                LockFileManager.SweepStale();

                _lockFile = new LockFileManager();
                await _lockFile.WriteAsync(port, token, _ide.GetWorkspaceFolders(), "Visual Studio 2022", cancellationToken);
                Logger.Info("Package", "Lock file written: " + _lockFile.Path);

                _solutionTracker = new SolutionTracker(_ide, _lockFile);

                await OpenToolWindowCommand.InitializeAsync(this);
                await AddSelectionToClaudeCommand.InitializeAsync(this);
                await OpenTerminalWithClaudeCommand.InitializeAsync(this, port, token);
                await AcceptDiffCommand.InitializeAsync(this);
                await RejectDiffCommand.InitializeAsync(this);

                await base.InitializeAsync(cancellationToken, progress);
                Logger.Info("Package", "Extension initialization complete.");
            }
            catch (Exception ex)
            {
                Logger.Error("Package", "InitializeAsync failed", ex);
                throw;
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _server?.BroadcastNotification("selection_changed", e.ToJson());
        }

        protected override int QueryClose(out bool canClose)
        {
            canClose = true;
            Logger.Info("Package", "Shutting down extension.");
            try { _solutionTracker?.Dispose(); }
            catch (Exception ex) { Logger.Error("Package", "SolutionTracker dispose failed", ex); }
            try { _server?.Stop(); }
            catch (Exception ex) { Logger.Error("Package", "Server stop failed", ex); }
            try { _lockFile?.Delete(); }
            catch (Exception ex) { Logger.Error("Package", "Lock file delete failed", ex); }
            try { _selectionTracker?.Dispose(); }
            catch (Exception ex) { Logger.Error("Package", "SelectionTracker dispose failed", ex); }
            return Microsoft.VisualStudio.VSConstants.S_OK;
        }
    }
}
