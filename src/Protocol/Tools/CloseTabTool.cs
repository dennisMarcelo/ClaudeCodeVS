using System;
using System.Threading;
using System.Threading.Tasks;
using ClaudeCodeVS.Ide;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json.Linq;

namespace ClaudeCodeVS.Protocol.Tools
{
    internal sealed class CloseTabTool : IMcpTool
    {
        private readonly IdeServices _ide;
        public CloseTabTool(IdeServices ide) { _ide = ide; }

        public string Name => "close_tab";
        public string Description => "Closes an open editor tab by file path.";
        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["filePath"] = new JObject { ["type"] = "string" },
                ["tab_name"] = new JObject { ["type"] = "string" },
                ["save"] = new JObject { ["type"] = "boolean" },
            },
        };

        public async Task<JToken> InvokeAsync(JObject arguments, CancellationToken ct)
        {
            string path = (string)(arguments?["filePath"] ?? arguments?["tab_name"]);
            bool save = arguments?["save"]?.Value<bool?>() ?? false;
            if (string.IsNullOrEmpty(path)) throw new McpErrorException(-32602, "filePath or tab_name required");

            await _ide.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
            try
            {
                foreach (Document doc in _ide.Dte.Documents)
                {
                    if (string.Equals(doc.FullName, path, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(doc.Name, path, StringComparison.OrdinalIgnoreCase))
                    {
                        doc.Close(save ? vsSaveChanges.vsSaveChangesYes : vsSaveChanges.vsSaveChangesPrompt);
                        return new JObject { ["ok"] = true };
                    }
                }
                return new JObject { ["ok"] = false, ["reason"] = "tab not found" };
            }
            catch (Exception ex)
            {
                throw new McpErrorException(-32603, "close_tab failed: " + ex.Message);
            }
        }
    }
}
