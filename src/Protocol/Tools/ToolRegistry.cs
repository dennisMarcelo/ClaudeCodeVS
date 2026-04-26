using System.Collections.Generic;
using ClaudeCodeVS.Ide;
using Newtonsoft.Json.Linq;

namespace ClaudeCodeVS.Protocol.Tools
{
    internal sealed class ToolRegistry
    {
        private readonly Dictionary<string, IMcpTool> _tools = new Dictionary<string, IMcpTool>();

        public IEnumerable<IMcpTool> Tools => _tools.Values;

        public void Register(IMcpTool tool) => _tools[tool.Name] = tool;

        public bool TryGet(string name, out IMcpTool tool) => _tools.TryGetValue(name, out tool);

        public void RegisterDefaults(IdeServices ide, SelectionTracker selection, DiffCoordinator diff)
        {
            Register(new GetWorkspaceFoldersTool(ide));
            Register(new GetOpenEditorsTool(ide));
            Register(new GetCurrentSelectionTool(ide));
            Register(new GetLatestSelectionTool(selection));
            Register(new GetDiagnosticsTool(ide));
            Register(new OpenFileTool(ide));
            Register(new SaveDocumentTool(ide));
            Register(new CheckDocumentDirtyTool(ide));
            Register(new CloseTabTool(ide));
            Register(new CloseAllDiffTabsTool(ide));
            Register(new OpenDiffTool(diff));
        }

        public JArray BuildToolsList()
        {
            var arr = new JArray();
            foreach (var t in _tools.Values)
            {
                arr.Add(new JObject
                {
                    ["name"] = t.Name,
                    ["description"] = t.Description,
                    ["inputSchema"] = t.InputSchema,
                });
            }
            return arr;
        }
    }
}
