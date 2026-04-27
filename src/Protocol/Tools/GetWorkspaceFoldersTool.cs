using System.Threading;
using System.Threading.Tasks;
using ClaudeCodeVS.Ide;
using Newtonsoft.Json.Linq;

namespace ClaudeCodeVS.Protocol.Tools
{
    internal sealed class GetWorkspaceFoldersTool : IMcpTool
    {
        private readonly IdeServices _ide;
        public GetWorkspaceFoldersTool(IdeServices ide) { _ide = ide; }

        public string Name => "getWorkspaceFolders";
        public string Description => "Returns workspace folders (solution directory and project directories) open in Visual Studio.";
        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject(),
        };

        public Task<JToken> InvokeAsync(JObject arguments, CancellationToken ct)
        {
            var folders = _ide.GetWorkspaceFolders();
            var arr = new JArray();
            foreach (var f in folders)
            {
                arr.Add(new JObject
                {
                    ["name"] = System.IO.Path.GetFileName(f.TrimEnd('\\', '/')),
                    ["uri"] = new System.Uri(f).AbsoluteUri,
                    ["path"] = f,
                });
            }
            return Task.FromResult<JToken>(new JObject
            {
                ["folders"] = arr,
                ["rootPath"] = folders.Length > 0 ? folders[0] : "",
            });
        }
    }
}
