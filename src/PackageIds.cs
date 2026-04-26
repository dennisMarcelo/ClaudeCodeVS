using System;

namespace ClaudeCodeVS
{
    internal static class PackageIds
    {
        public const string PackageGuidString = "8b1e8a27-4a7a-4c9e-9c0a-4c7f9e3b1d01";
        public const string ToolWindowGuidString = "8b1e8a27-4a7a-4c9e-9c0a-4c7f9e3b1d02";
        public const string CommandSetGuidString = "8b1e8a27-4a7a-4c9e-9c0a-4c7f9e3b1d03";

        public static readonly Guid PackageGuid = new Guid(PackageGuidString);
        public static readonly Guid ToolWindowGuid = new Guid(ToolWindowGuidString);
        public static readonly Guid CommandSet = new Guid(CommandSetGuidString);

        public const int OpenToolWindowCommandId = 0x0100;
        public const int AddSelectionToClaudeCommandId = 0x0101;
        public const int OpenTerminalWithClaudeCommandId = 0x0102;
        public const int AcceptDiffCommandId = 0x0103;
        public const int RejectDiffCommandId = 0x0104;
    }
}
