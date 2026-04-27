using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ClaudeCodeVS.Ide;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using Newtonsoft.Json.Linq;

namespace ClaudeCodeVS.Protocol.Tools
{
    internal sealed class GetDiagnosticsTool : IMcpTool
    {
        private readonly IdeServices _ide;
        public GetDiagnosticsTool(IdeServices ide) { _ide = ide; }

        public string Name => "getDiagnostics";
        public string Description => "Returns diagnostics (errors/warnings) from the Visual Studio Error List, optionally filtered to one file.";
        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["uri"] = new JObject { ["type"] = "string", ["description"] = "Optional file URI or absolute path to filter by." },
            },
        };

        public async Task<JToken> InvokeAsync(JObject arguments, CancellationToken ct)
        {
            await _ide.JoinableTaskFactory.SwitchToMainThreadAsync(ct);

            string filter = (string)arguments?["uri"];
            if (!string.IsNullOrEmpty(filter) && filter.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                try { filter = new Uri(filter).LocalPath; } catch { }
            }

            var items = new List<JObject>();

            var tableControl = _ide.ErrorList?.TableControl;
            if (tableControl != null)
            {
                foreach (var handle in tableControl.Entries)
                {
                    try
                    {
                        handle.TryGetValue(StandardTableKeyNames.DocumentName, out object docName);
                        string path = docName as string ?? "";
                        if (!string.IsNullOrEmpty(filter) && !string.Equals(path, filter, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        handle.TryGetValue(StandardTableKeyNames.Text, out object text);
                        handle.TryGetValue(StandardTableKeyNames.ErrorSeverity, out object sev);
                        handle.TryGetValue(StandardTableKeyNames.Line, out object line);
                        handle.TryGetValue(StandardTableKeyNames.Column, out object col);
                        handle.TryGetValue(StandardTableKeyNames.ErrorCode, out object code);

                        items.Add(new JObject
                        {
                            ["filePath"] = path,
                            ["message"] = text as string ?? "",
                            ["severity"] = SeverityToString(sev),
                            ["line"] = line is int l ? l : 0,
                            ["column"] = col is int c ? c : 0,
                            ["code"] = code as string ?? "",
                        });
                    }
                    catch { }
                }
            }

            return new JObject { ["diagnostics"] = new JArray(items) };
        }

        private static string SeverityToString(object sev)
        {
            if (sev is Microsoft.VisualStudio.Shell.Interop.__VSERRORCATEGORY cat)
            {
                switch (cat)
                {
                    case Microsoft.VisualStudio.Shell.Interop.__VSERRORCATEGORY.EC_ERROR: return "error";
                    case Microsoft.VisualStudio.Shell.Interop.__VSERRORCATEGORY.EC_WARNING: return "warning";
                    case Microsoft.VisualStudio.Shell.Interop.__VSERRORCATEGORY.EC_MESSAGE: return "info";
                }
            }
            return "info";
        }
    }
}
