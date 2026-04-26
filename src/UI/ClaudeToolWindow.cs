using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace ClaudeCodeVS.UI
{
    [Guid(PackageIds.ToolWindowGuidString)]
    public sealed class ClaudeToolWindow : ToolWindowPane
    {
        public ClaudeToolWindow() : base(null)
        {
            Caption = "Claude Code";
            Content = new ChatPanel();
        }
    }
}
