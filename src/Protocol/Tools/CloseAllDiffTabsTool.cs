using System;
using System.Threading;
using System.Threading.Tasks;
using ClaudeCodeVS.Ide;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json.Linq;

namespace ClaudeCodeVS.Protocol.Tools
{
    internal sealed class CloseAllDiffTabsTool : IMcpTool
    {
        private readonly IdeServices _ide;
        public CloseAllDiffTabsTool(IdeServices ide) { _ide = ide; }

        public string Name => "close_all_diff_tabs";
        public string Description => "Closes all diff comparison tabs opened by Claude.";
        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject(),
        };

        public async Task<JToken> InvokeAsync(JObject arguments, CancellationToken ct)
        {
            await _ide.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
            int closed = 0;
            try
            {
                Window[] toClose;
                var list = new System.Collections.Generic.List<Window>();
                foreach (Window w in _ide.Dte.Windows)
                {
                    try
                    {
                        string caption = w.Caption ?? "";
                        if (caption.IndexOf("Compare", StringComparison.OrdinalIgnoreCase) >= 0
                            || caption.IndexOf(" ↔ ", StringComparison.Ordinal) >= 0
                            || caption.IndexOf(" <> ", StringComparison.Ordinal) >= 0)
                        {
                            list.Add(w);
                        }
                    }
                    catch { }
                }
                toClose = list.ToArray();
                foreach (var w in toClose)
                {
                    try { w.Close(); closed++; } catch { }
                }
            }
            catch { }
            return new JObject { ["closedCount"] = closed };
        }
    }
}
