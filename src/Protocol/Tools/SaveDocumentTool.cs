using System;
using System.Threading;
using System.Threading.Tasks;
using ClaudeCodeVS.Ide;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json.Linq;

namespace ClaudeCodeVS.Protocol.Tools
{
    internal sealed class SaveDocumentTool : IMcpTool
    {
        private readonly IdeServices _ide;
        public SaveDocumentTool(IdeServices ide) { _ide = ide; }

        public string Name => "saveDocument";
        public string Description => "Saves an open document by file path.";
        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["filePath"] = new JObject { ["type"] = "string" },
            },
            ["required"] = new JArray("filePath"),
        };

        public async Task<JToken> InvokeAsync(JObject arguments, CancellationToken ct)
        {
            string path = (string)arguments?["filePath"];
            if (string.IsNullOrEmpty(path)) throw new McpErrorException(-32602, "filePath required");

            await _ide.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
            try
            {
                foreach (Document doc in _ide.Dte.Documents)
                {
                    if (string.Equals(doc.FullName, path, StringComparison.OrdinalIgnoreCase))
                    {
                        doc.Save();
                        return new JObject { ["ok"] = true };
                    }
                }
                return new JObject { ["ok"] = false, ["reason"] = "document not open" };
            }
            catch (Exception ex)
            {
                throw new McpErrorException(-32603, "save_document failed: " + ex.Message);
            }
        }
    }
}
