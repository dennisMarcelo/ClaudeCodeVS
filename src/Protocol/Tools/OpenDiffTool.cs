using System.Threading;
using System.Threading.Tasks;
using ClaudeCodeVS.Ide;
using Newtonsoft.Json.Linq;

namespace ClaudeCodeVS.Protocol.Tools
{
    internal sealed class OpenDiffTool : IMcpTool
    {
        private readonly DiffCoordinator _diff;
        public OpenDiffTool(DiffCoordinator diff) { _diff = diff; }

        public string Name => "openDiff";
        public string Description => "Opens a side-by-side diff in Visual Studio and blocks until the user accepts or rejects the change.";
        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["old_file_path"] = new JObject { ["type"] = "string" },
                ["new_file_path"] = new JObject { ["type"] = "string" },
                ["new_file_contents"] = new JObject { ["type"] = "string" },
                ["tab_name"] = new JObject { ["type"] = "string" },
            },
            ["required"] = new JArray("old_file_path", "new_file_contents"),
        };

        public async Task<JToken> InvokeAsync(JObject arguments, CancellationToken ct)
        {
            string oldPath = (string)arguments["old_file_path"];
            string newPath = (string)arguments["new_file_path"];
            string contents = (string)arguments["new_file_contents"] ?? "";
            string tabName = (string)arguments["tab_name"];

            var result = await _diff.OpenAsync(oldPath, newPath, contents, tabName, ct).ConfigureAwait(false);

            if (result.Status == "FILE_SAVED")
            {
                return new JObject
                {
                    ["status"] = "FILE_SAVED",
                    ["savedPath"] = result.SavedPath ?? "",
                    ["content"] = result.Contents ?? "",
                };
            }
            return new JObject { ["status"] = "DIFF_REJECTED" };
        }
    }
}
