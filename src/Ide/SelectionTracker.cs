using System;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using Newtonsoft.Json.Linq;

namespace ClaudeCodeVS.Ide
{
    internal sealed class SelectionTracker : IVsTextManagerEvents, IDisposable
    {
        private readonly IdeServices _ide;
        private uint _cookie;
        private readonly SynchronizationContext _uiContext;
        private Timer _debounce;
        private SelectionSnapshot _latest;

        public event EventHandler<SelectionChangedEventArgs> SelectionChanged;

        public SelectionSnapshot Latest => _latest;

        public SelectionTracker(IdeServices ide)
        {
            _ide = ide;
            _uiContext = SynchronizationContext.Current;
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await _ide.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (_ide.TextManager is IConnectionPointContainer cpc)
                {
                    var iid = typeof(IVsTextManagerEvents).GUID;
                    cpc.FindConnectionPoint(ref iid, out var cp);
                    cp?.Advise(this, out _cookie);
                }
            });
        }

        public void OnRegisterMarkerType(int markerType) { }
        public void OnRegisterView(IVsTextView view) { }
        public void OnUnregisterView(IVsTextView view) { }
        public void OnUserPreferencesChanged(VIEWPREFERENCES[] viewPrefs, FRAMEPREFERENCES[] framePrefs, LANGPREFERENCES[] langPrefs, FONTCOLORPREFERENCES[] colorPrefs) { }

        private void Debounced(object state)
        {
            try
            {
                SelectionSnapshot snap = null;
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await _ide.JoinableTaskFactory.SwitchToMainThreadAsync();
                    snap = Capture();
                });
                if (snap == null) return;
                _latest = snap;
                SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(snap));
            }
            catch { }
        }

        public void Poke()
        {
            if (_debounce == null)
            {
                _debounce = new Timer(Debounced, null, 100, Timeout.Infinite);
            }
            else
            {
                _debounce.Change(100, Timeout.Infinite);
            }
        }

        public SelectionSnapshot Capture()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var view = _ide.GetActiveTextView();
            if (view == null) return null;
            string path = _ide.GetFilePathFromTextView(view);
            view.GetSelection(out int startLine, out int startCol, out int endLine, out int endCol);
            view.GetSelectedText(out string text);
            return new SelectionSnapshot
            {
                FilePath = path ?? "",
                Text = text ?? "",
                StartLine = startLine,
                StartColumn = startCol,
                EndLine = endLine,
                EndColumn = endCol,
            };
        }

        public void Dispose()
        {
            try { _debounce?.Dispose(); } catch { }
        }
    }

    internal sealed class SelectionSnapshot
    {
        public string FilePath { get; set; }
        public string Text { get; set; }
        public int StartLine { get; set; }
        public int StartColumn { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }

        public JObject ToJson() => new JObject
        {
            ["text"] = Text ?? "",
            ["filePath"] = FilePath ?? "",
            ["fileUrl"] = string.IsNullOrEmpty(FilePath) ? "" : new Uri(FilePath).AbsoluteUri,
            ["selection"] = new JObject
            {
                ["start"] = new JObject { ["line"] = StartLine, ["character"] = StartColumn },
                ["end"] = new JObject { ["line"] = EndLine, ["character"] = EndColumn },
                ["isEmpty"] = string.IsNullOrEmpty(Text),
            },
        };
    }

    internal sealed class SelectionChangedEventArgs : EventArgs
    {
        public SelectionSnapshot Snapshot { get; }
        public SelectionChangedEventArgs(SelectionSnapshot s) { Snapshot = s; }
        public JObject ToJson() => Snapshot.ToJson();
    }
}
