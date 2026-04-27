using System;
using ClaudeCodeVS.Diagnostics;
using ClaudeCodeVS.Protocol;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace ClaudeCodeVS.Ide
{
    internal sealed class SolutionTracker : IVsSolutionEvents, IDisposable
    {
        private readonly IdeServices _ide;
        private readonly LockFileManager _lockFile;
        private uint _cookie;
        private bool _disposed;

        public SolutionTracker(IdeServices ide, LockFileManager lockFile)
        {
            _ide = ide;
            _lockFile = lockFile;

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await _ide.JoinableTaskFactory.SwitchToMainThreadAsync();
                try
                {
                    _ide.Solution.AdviseSolutionEvents(this, out _cookie);
                    Logger.Info("SolutionTracker", "Subscribed to IVsSolutionEvents (cookie=" + _cookie + ").");
                }
                catch (Exception ex)
                {
                    Logger.Error("SolutionTracker", "AdviseSolutionEvents failed", ex);
                }
            });
        }

        private void Refresh(string reason)
        {
            try
            {
                var folders = _ide.GetWorkspaceFolders();
                _lockFile.UpdateWorkspaceFolders(folders);
                Logger.Info("SolutionTracker", "Lock workspaceFolders refreshed (" + reason + "): " +
                    (folders == null || folders.Length == 0 ? "(empty)" : string.Join(" ; ", folders)));
            }
            catch (Exception ex)
            {
                Logger.Error("SolutionTracker", "Refresh failed (" + reason + ")", ex);
            }
        }

        public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            Refresh("OnAfterOpenSolution");
            return VSConstants.S_OK;
        }

        public int OnAfterCloseSolution(object pUnkReserved)
        {
            Refresh("OnAfterCloseSolution");
            return VSConstants.S_OK;
        }

        public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
        {
            Refresh("OnAfterOpenProject");
            return VSConstants.S_OK;
        }

        public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
        {
            Refresh("OnAfterLoadProject");
            return VSConstants.S_OK;
        }

        public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
        {
            Refresh("OnBeforeUnloadProject");
            return VSConstants.S_OK;
        }

        public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
        {
            Refresh("OnBeforeCloseProject");
            return VSConstants.S_OK;
        }

        public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) => VSConstants.S_OK;
        public int OnBeforeCloseSolution(object pUnkReserved) => VSConstants.S_OK;
        public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) => VSConstants.S_OK;
        public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) => VSConstants.S_OK;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            uint cookie = _cookie;
            _cookie = 0;
            if (cookie == 0) return;
            try
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await _ide.JoinableTaskFactory.SwitchToMainThreadAsync();
                    try { _ide.Solution.UnadviseSolutionEvents(cookie); } catch { }
                });
            }
            catch
            {
            }
        }
    }
}
