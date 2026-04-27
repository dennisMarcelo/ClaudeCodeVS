using System;
using System.Threading;
using System.Threading.Tasks;
using ClaudeCodeVS.Ide;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Newtonsoft.Json.Linq;

namespace ClaudeCodeVS.Protocol.Tools
{
    internal sealed class OpenFileTool : IMcpTool
    {
        private readonly IdeServices _ide;
        public OpenFileTool(IdeServices ide) { _ide = ide; }

        public string Name => "openFile";
        public string Description => "Opens a file in Visual Studio. Optionally selects a line range and preview-opens the file.";
        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["filePath"] = new JObject { ["type"] = "string" },
                ["preview"] = new JObject { ["type"] = "boolean" },
                ["startLine"] = new JObject { ["type"] = "integer" },
                ["endLine"] = new JObject { ["type"] = "integer" },
                ["startText"] = new JObject { ["type"] = "string" },
                ["endText"] = new JObject { ["type"] = "string" },
                ["makeFrontmost"] = new JObject { ["type"] = "boolean" },
            },
            ["required"] = new JArray("filePath"),
        };

        public async Task<JToken> InvokeAsync(JObject arguments, CancellationToken ct)
        {
            string path = (string)arguments?["filePath"];
            if (string.IsNullOrEmpty(path)) throw new McpErrorException(-32602, "filePath required");

            int? startLine = arguments["startLine"]?.Value<int?>();
            int? endLine = arguments["endLine"]?.Value<int?>();
            bool front = arguments["makeFrontmost"]?.Value<bool?>() ?? true;

            await _ide.JoinableTaskFactory.SwitchToMainThreadAsync(ct);

            try
            {
                VsShellUtilities.OpenDocument(ServiceProvider.GlobalProvider, path, Guid.Empty,
                    out _, out _, out IVsWindowFrame frame, out IVsTextView view);

                if (front && frame != null)
                {
                    frame.Show();
                }

                if (view != null && startLine.HasValue)
                {
                    int s = Math.Max(0, startLine.Value - 1);
                    int e = endLine.HasValue ? Math.Max(s, endLine.Value - 1) : s;
                    view.SetSelection(s, 0, e, 0);
                    view.CenterLines(s, 1);
                }

                return new JObject { ["ok"] = true, ["filePath"] = path };
            }
            catch (Exception ex)
            {
                throw new McpErrorException(-32603, "open_file failed: " + ex.Message);
            }
        }
    }
}
