using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.TextManager.Interop;

namespace ClaudeCodeVS.Ide
{
    internal sealed class IdeServices
    {
        private readonly AsyncPackage _package;
        private readonly DTE2 _dte;
        private readonly IVsTextManager _textManager;
        private readonly IVsSolution _solution;
        private readonly IVsDifferenceService _diffService;
        private readonly IErrorList _errorList;
        private readonly IVsUIShellOpenDocument _openDoc;
        private readonly JoinableTaskFactory _jtf;

        public DTE2 Dte => _dte;
        public IVsTextManager TextManager => _textManager;
        public IVsSolution Solution => _solution;
        public IVsDifferenceService DiffService => _diffService;
        public IErrorList ErrorList => _errorList;
        public IVsUIShellOpenDocument UIShellOpenDocument => _openDoc;
        public JoinableTaskFactory JoinableTaskFactory => _jtf;

        private IdeServices(AsyncPackage package, DTE2 dte, IVsTextManager tm, IVsSolution sln,
            IVsDifferenceService diff, IErrorList errors, IVsUIShellOpenDocument openDoc, JoinableTaskFactory jtf)
        {
            _package = package;
            _dte = dte;
            _textManager = tm;
            _solution = sln;
            _diffService = diff;
            _errorList = errors;
            _openDoc = openDoc;
            _jtf = jtf;
        }

        public static async Task<IdeServices> CreateAsync(AsyncPackage package, CancellationToken ct)
        {
            await package.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
            var dte = (DTE2)await package.GetServiceAsync(typeof(SDTE));
            var tm = (IVsTextManager)await package.GetServiceAsync(typeof(SVsTextManager));
            var sln = (IVsSolution)await package.GetServiceAsync(typeof(SVsSolution));
            var diff = (IVsDifferenceService)await package.GetServiceAsync(typeof(SVsDifferenceService));
            var errors = (IErrorList)await package.GetServiceAsync(typeof(SVsErrorList));
            var openDoc = (IVsUIShellOpenDocument)await package.GetServiceAsync(typeof(SVsUIShellOpenDocument));
            return new IdeServices(package, dte, tm, sln, diff, errors, openDoc, package.JoinableTaskFactory);
        }

        public async Task RunOnMainAsync(Action action, CancellationToken ct = default)
        {
            await _jtf.SwitchToMainThreadAsync(ct);
            action();
        }

        public async Task<T> RunOnMainAsync<T>(Func<T> func, CancellationToken ct = default)
        {
            await _jtf.SwitchToMainThreadAsync(ct);
            return func();
        }

        public string[] GetWorkspaceFolders()
        {
            var list = new List<string>();
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await _jtf.SwitchToMainThreadAsync();
                try
                {
                    string slnPath = _dte.Solution?.FullName;
                    if (!string.IsNullOrEmpty(slnPath))
                    {
                        list.Add(Path.GetDirectoryName(slnPath));
                    }
                    foreach (EnvDTE.Project p in _dte.Solution.Projects)
                    {
                        try
                        {
                            string full = p.FullName;
                            if (!string.IsNullOrEmpty(full))
                            {
                                var dir = Path.GetDirectoryName(full);
                                if (!string.IsNullOrEmpty(dir) && !list.Contains(dir)) list.Add(dir);
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            });
            return list.ToArray();
        }

        public IVsTextView GetActiveTextView()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            IVsTextView view;
            int hr = _textManager.GetActiveView(1, null, out view);
            return ErrorHandler.Succeeded(hr) ? view : null;
        }

        public string GetFilePathFromTextView(IVsTextView view)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (view == null) return null;
            if (view.GetBuffer(out IVsTextLines lines) != VSConstants.S_OK || lines == null) return null;
            if (lines is IPersistFileFormat pff)
            {
                if (pff.GetCurFile(out string path, out _) == VSConstants.S_OK) return path;
            }
            return null;
        }
    }
}
