using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ClaudeCodeVS.Protocol.Tools
{
    internal interface IMcpTool
    {
        string Name { get; }
        string Description { get; }
        JObject InputSchema { get; }
        Task<JToken> InvokeAsync(JObject arguments, CancellationToken ct);
    }
}
