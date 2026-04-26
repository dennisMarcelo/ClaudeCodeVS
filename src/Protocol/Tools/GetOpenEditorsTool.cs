using System.Threading;
using System.Threading.Tasks;
using ClaudeCodeVS.Ide;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json.Linq;

namespace ClaudeCodeVS.Protocol.Tools
{
    internal sealed class GetOpenEditorsTool : IMcpTool
    {
        private readonly IdeServices _ide;
        public GetOpenEditorsTool(IdeServices ide) { _ide = ide; }

        public string Name => "get_open_editors";
        public string Description => "Returns the list of currently open editor tabs in Visual Studio.";
        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject(),
        };

        public async Task<JToken> InvokeAsync(JObject arguments, CancellationToken ct)
        {
            await _ide.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
            var arr = new JArray();
            string active = null;
            try
            {
                active = _ide.Dte.ActiveDocument?.FullName;
            }
            catch { }

            try
            {
                foreach (Document doc in _ide.Dte.Documents)
                {
                    try
                    {
                        arr.Add(new JObject
                        {
                            ["filePath"] = doc.FullName,
                            ["fileUrl"] = new System.Uri(doc.FullName).AbsoluteUri,
                            ["languageId"] = doc.Language ?? "",
                            ["isDirty"] = !doc.Saved,
                            ["isActive"] = doc.FullName == active,
                        });
                    }
                    catch { }
                }
            }
            catch { }

            return new JObject { ["tabs"] = arr };
        }
    }
}
