using System.Threading;
using System.Threading.Tasks;
using ClaudeCodeVS.Ide;
using Newtonsoft.Json.Linq;

namespace ClaudeCodeVS.Protocol.Tools
{
    internal sealed class GetLatestSelectionTool : IMcpTool
    {
        private readonly SelectionTracker _tracker;
        public GetLatestSelectionTool(SelectionTracker tracker) { _tracker = tracker; }

        public string Name => "getLatestSelection";
        public string Description => "Returns the most recent non-empty selection observed by the selection tracker.";
        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject(),
        };

        public Task<JToken> InvokeAsync(JObject arguments, CancellationToken ct)
        {
            var snap = _tracker.Latest;
            if (snap == null)
            {
                return Task.FromResult<JToken>(new JObject { ["text"] = "", ["filePath"] = "" });
            }
            return Task.FromResult<JToken>(snap.ToJson());
        }
    }
}
