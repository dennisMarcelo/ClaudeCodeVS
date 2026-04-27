using System;
using System.Threading;
using System.Threading.Tasks;
using ClaudeCodeVS.Diagnostics;
using ClaudeCodeVS.Protocol.Tools;
using Newtonsoft.Json.Linq;

namespace ClaudeCodeVS.Protocol
{
    internal sealed class JsonRpcDispatcher
    {
        private const string ProtocolVersion = "2024-11-05";
        private const string ServerName = "claude-code-visualstudio";
        private const string ServerVersion = "0.1.0";

        private readonly ToolRegistry _tools;

        public JsonRpcDispatcher(ToolRegistry tools)
        {
            _tools = tools;
        }

        public async Task<string> HandleAsync(string raw, ClientSession session)
        {
            JObject req;
            try { req = JObject.Parse(raw); }
            catch (Exception ex)
            {
                Logger.Warn("Dispatcher", "Failed to parse incoming JSON: " + ex.Message);
                return null;
            }

            string method = (string)req["method"];
            JToken id = req["id"];
            JObject p = req["params"] as JObject ?? new JObject();

            if (id == null)
            {
                // Notification — no response expected.
                return null;
            }

            try
            {
                JToken result = await HandleMethodAsync(method, p, CancellationToken.None).ConfigureAwait(false);
                return BuildResponse(id, result, null);
            }
            catch (McpErrorException mex)
            {
                Logger.Warn("Dispatcher", "MCP error in " + method + " (code=" + mex.Code + "): " + mex.Message);
                return BuildResponse(id, null, new JObject { ["code"] = mex.Code, ["message"] = mex.Message });
            }
            catch (Exception ex)
            {
                Logger.Error("Dispatcher", "Method " + method + " threw", ex);
                return BuildResponse(id, null, new JObject { ["code"] = -32603, ["message"] = ex.Message });
            }
        }

        private async Task<JToken> HandleMethodAsync(string method, JObject p, CancellationToken ct)
        {
            switch (method)
            {
                case "initialize":
                    return new JObject
                    {
                        ["protocolVersion"] = ProtocolVersion,
                        ["capabilities"] = new JObject { ["tools"] = new JObject() },
                        ["serverInfo"] = new JObject { ["name"] = ServerName, ["version"] = ServerVersion },
                    };

                case "notifications/initialized":
                    return new JObject();

                case "tools/list":
                    return new JObject { ["tools"] = _tools.BuildToolsList() };

                case "tools/call":
                {
                    string name = (string)p["name"];
                    JObject args = p["arguments"] as JObject ?? new JObject();
                    if (!_tools.TryGet(name, out var tool))
                    {
                        throw new McpErrorException(-32601, "Unknown tool: " + name);
                    }
                    JToken raw = await tool.InvokeAsync(args, ct).ConfigureAwait(false);
                    string text = raw is JValue v && v.Type == JTokenType.String
                        ? (string)v
                        : raw?.ToString(Newtonsoft.Json.Formatting.None);
                    return new JObject
                    {
                        ["content"] = new JArray
                        {
                            new JObject
                            {
                                ["type"] = "text",
                                ["text"] = text ?? "",
                            },
                        },
                        ["isError"] = false,
                    };
                }

                case "ping":
                    return new JObject();

                default:
                    throw new McpErrorException(-32601, "Method not found: " + method);
            }
        }

        private static string BuildResponse(JToken id, JToken result, JObject error)
        {
            var obj = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
            };
            if (error != null) obj["error"] = error;
            else obj["result"] = result ?? new JObject();
            return obj.ToString(Newtonsoft.Json.Formatting.None);
        }
    }

    internal sealed class McpErrorException : Exception
    {
        public int Code { get; }
        public McpErrorException(int code, string message) : base(message) { Code = code; }
    }
}
