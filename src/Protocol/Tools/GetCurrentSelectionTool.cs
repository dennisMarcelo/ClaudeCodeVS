using System.Threading;
using System.Threading.Tasks;
using ClaudeCodeVS.Ide;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json.Linq;

namespace ClaudeCodeVS.Protocol.Tools
{
    internal sealed class GetCurrentSelectionTool : IMcpTool
    {
        private readonly IdeServices _ide;
        public GetCurrentSelectionTool(IdeServices ide) { _ide = ide; }

        public string Name => "getCurrentSelection";
        public string Description => "Returns the text currently selected in the active editor, with file path and line/column range.";
        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject(),
        };

        public async Task<JToken> InvokeAsync(JObject arguments, CancellationToken ct)
        {
            await _ide.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
            var view = _ide.GetActiveTextView();
            if (view == null)
            {
                return new JObject { ["text"] = "", ["filePath"] = "", ["selection"] = new JObject() };
            }
            string path = _ide.GetFilePathFromTextView(view) ?? "";
            view.GetSelection(out int startLine, out int startCol, out int endLine, out int endCol);
            view.GetSelectedText(out string text);
            return new JObject
            {
                ["text"] = text ?? "",
                ["filePath"] = path,
                ["fileUrl"] = string.IsNullOrEmpty(path) ? "" : new System.Uri(path).AbsoluteUri,
                ["selection"] = new JObject
                {
                    ["start"] = new JObject { ["line"] = startLine, ["character"] = startCol },
                    ["end"] = new JObject { ["line"] = endLine, ["character"] = endCol },
                    ["isEmpty"] = string.IsNullOrEmpty(text),
                },
            };
        }
    }
}
