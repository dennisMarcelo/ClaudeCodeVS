using System;
using System.Threading;
using System.Threading.Tasks;
using ClaudeCodeVS.Ide;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json.Linq;

namespace ClaudeCodeVS.Protocol.Tools
{
    internal sealed class CheckDocumentDirtyTool : IMcpTool
    {
        private readonly IdeServices _ide;
        public CheckDocumentDirtyTool(IdeServices ide) { _ide = ide; }

        public string Name => "checkDocumentDirty";
        public string Description => "Returns whether a document has unsaved changes.";
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
            if (string.IsNullOrEmpty(path))
            {
                throw new McpErrorException(-32602, "filePath required");
            }

            await _ide.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
            try
            {
                foreach (Document doc in _ide.Dte.Documents)
                {
                    if (string.Equals(doc.FullName, path, StringComparison.OrdinalIgnoreCase))
                    {
                        return new JObject { ["isDirty"] = !doc.Saved, ["isOpen"] = true };
                    }
                }
            }
            catch { }
            return new JObject { ["isDirty"] = false, ["isOpen"] = false };
        }
    }
}
